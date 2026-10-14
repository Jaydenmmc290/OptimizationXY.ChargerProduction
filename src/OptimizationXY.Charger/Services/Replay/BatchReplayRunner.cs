using System.Diagnostics;
using System.Globalization;
using System.Text;
using NLog;
using OptimizationXY.Charger.Infrastructure;
using OptimizationXY.Charger.Models.Calibration;
using OptimizationXY.Charger.Models.Core;
using OptimizationXY.Charger.Services.Configuration;
using OptimizationXY.Charger.Services.Core;
using OptimizationXY.Charger.Services.Operator;
using OptimizationXY.Charger.Services.Scan;

namespace OptimizationXY.Charger.Services.Replay;

/// <summary>
/// Replays every saved scan under a folder and writes one line per scan to a
/// CSV, so a change to the processing can be checked against many real scans
/// at once instead of one at a time.
///
///   OptimizationXY.Charger.exe --replay-batch "\\fileserver\...\SavedScans"
///       [--baseline "...\ReplayBatch_...\summary.csv"]   compare with an earlier run
///       [--tolerance-mm 0.5]                            command change that counts as "changed"
///       [--set Section.Name=value] ...                  try a setting without saving it
///       [--out "C:\ReplayBatch"]                        where results go
///
/// A scan folder is any folder that contains a "Frames" folder. Its calibration
/// is the scanner-calibration.json saved with it. Nothing is sent to the PLC,
/// nothing is written to SQL, cameras are never opened, and nothing is written
/// into the scan folders.
///
/// Results (in the output folder):
///   summary.csv  one line per scan: commands, inscribed diameter, sheets, ...
///   summary.txt  totals, scans that changed against the baseline, and the
///                spread of the commands between repeat scans of the same block
///   reports\     the full diagnostics report of every scan
/// </summary>
internal static class BatchReplayRunner
{
    public const string CommandLineSwitch = "--replay-batch";

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static bool IsRequested(string[] args) =>
        args.Any(argument => string.Equals(argument, CommandLineSwitch, StringComparison.OrdinalIgnoreCase));

    internal sealed record Row(
        string ScanFolder,
        string Block,
        string Status,
        int Frames,
        int FramesUsed,
        bool RevolutionVerified,
        int FramesPerRevolution,
        string Strategy,
        double TailXMM,
        double TailYMM,
        double HeadXMM,
        double HeadYMM,
        double InscribedDiameterMM,
        int UsableSheets,
        int TotalSheets,
        double UsableVeneerLengthMM,
        string ScanQuality,
        double ProcessingSeconds,
        string Error);

    public static void Run(string[] args)
    {
        string? root = ArgumentAfter(args, CommandLineSwitch);

        if (root is null && OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select a folder of saved scans (for example SavedScans or one day/block folder).",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false
            };

            root = dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
        }

        if (root is null)
            return;

        string? baselinePath = ArgumentAfter(args, "--baseline");
        double toleranceMM = double.TryParse(
            ArgumentAfter(args, "--tolerance-mm"),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double tolerance) ? tolerance : 0.5;

        string outputFolder = ArgumentAfter(args, "--out") ??
            Path.Combine(ApplicationPaths.RootDirectory, "ReplayBatch");
        outputFolder = Path.Combine(outputFolder, $"ReplayBatch_{DateTime.Now:yyyyMMdd_HHmmss}");

        try
        {
            string summaryPath = RunBatch(
                root,
                outputFolder,
                SettingsOverride.FromCommandLine(args),
                baselinePath,
                toleranceMM);

            if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                MessageBox.Show(
                    $"Batch replay complete.\r\n\r\nResults:\r\n{outputFolder}",
                    "Optimization XY Charger - Batch Replay",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                try
                {
                    Process.Start(new ProcessStartInfo(summaryPath) { UseShellExecute = true });
                }
                catch (Exception openException)
                {
                    Logger.Warn(openException, "[BATCH] The summary could not be opened automatically.");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[BATCH] Batch replay failed.");

            if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                MessageBox.Show(
                    $"Batch replay failed:\r\n\r\n{ex.Message}",
                    "Optimization XY Charger - Batch Replay",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        finally
        {
            PipelineState.Instance.Clear();
        }
    }

    /// <summary>Runs the batch and returns the path of summary.txt.</summary>
    internal static string RunBatch(
        string root,
        string outputFolder,
        IReadOnlyList<string> overrides,
        string? baselinePath,
        double toleranceMM)
    {
        PipelineState state = PipelineState.Instance;

        var applied = new List<string>();
        foreach (string assignment in overrides)
            applied.Add(SettingsOverride.Apply(state.Settings, assignment));

        if (applied.Count > 0)
            AppSettingsValidator.Validate(state.Settings);

        string reportsFolder = Path.Combine(outputFolder, "reports");
        Directory.CreateDirectory(reportsFolder);

        List<string> scanFolders = FindScanFolders(root);
        Logger.Info($"[BATCH] {scanFolders.Count} scan folders found under '{root}'.");

        var rows = new List<Row>();

        for (int index = 0; index < scanFolders.Count; index++)
        {
            string scanFolder = scanFolders[index];
            Logger.Info($"[BATCH] {index + 1}/{scanFolders.Count}: {scanFolder}");

            Row row = ReplayOne(scanFolder, root, reportsFolder);
            rows.Add(row);
        }

        string csvPath = Path.Combine(outputFolder, "summary.csv");
        WriteCsv(csvPath, rows);

        List<Row>? baseline = baselinePath is not null && File.Exists(baselinePath)
            ? ReadCsv(baselinePath)
            : null;

        string summaryPath = Path.Combine(outputFolder, "summary.txt");
        File.WriteAllText(
            summaryPath,
            BuildSummary(root, applied, rows, baseline, baselinePath, toleranceMM),
            Encoding.UTF8);

        Logger.Info($"[BATCH] Results written to '{outputFolder}'.");
        return summaryPath;
    }

    /// <summary>Folders under root (including root) that contain a "Frames" folder.</summary>
    internal static List<string> FindScanFolders(string root)
    {
        var folders = new List<string>();

        if (Directory.Exists(Path.Combine(root, "Frames")))
            folders.Add(Path.GetFullPath(root));

        foreach (string frames in Directory.EnumerateDirectories(root, "Frames", SearchOption.AllDirectories))
        {
            string? parent = Path.GetDirectoryName(frames);
            if (parent is not null && !folders.Contains(Path.GetFullPath(parent), StringComparer.OrdinalIgnoreCase))
                folders.Add(Path.GetFullPath(parent));
        }

        folders.Sort(StringComparer.OrdinalIgnoreCase);
        return folders;
    }

    private static Row ReplayOne(string scanFolder, string root, string reportsFolder)
    {
        PipelineState state = PipelineState.Instance;
        string relative = Path.GetRelativePath(root, scanFolder);
        string block = Path.GetFileName(Path.GetDirectoryName(scanFolder) ?? string.Empty) ?? string.Empty;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            state.Clear();

            ScannerLookupTable lookupTable =
                OfflineReplayRunner.LoadSavedCalibration(scanFolder)
                ?? throw new InvalidDataException("No valid scanner-calibration.json saved with this scan.");

            List<ScanFrame> frames = ScanFrameLoader.LoadFromFolder(Path.Combine(scanFolder, "Frames"));
            state.ScanFrames = frames;

            LogProcessingPipeline.Instance.RunCurrentFramesPipeline(
                lookupTable,
                CancellationToken.None,
                progress: null);

            stopwatch.Stop();

            string quality;
            int framesUsed = frames.Count(frame => frame.IsValid);

            try
            {
                var result = OperatorScanQualityAnalyzer.Analyze(state);
                quality = result.Status.ToString();
                framesUsed = result.FramesUsed;
            }
            catch (Exception qualityException)
            {
                quality = "Unknown: " + qualityException.Message;
            }

            WriteReport(reportsFolder, relative, state.DiagnosticsReport);

            var recommendation = state.FinalSpindleRecommendation;

            return new Row(
                relative,
                block,
                recommendation is null ? "NoResult" : "OK",
                frames.Count,
                framesUsed,
                state.RevolutionAngles?.RevolutionVerified ?? false,
                state.RevolutionAngles?.FramesPerRevolution ?? 0,
                recommendation?.SelectedStrategy ?? string.Empty,
                recommendation?.CommandTailXMM ?? double.NaN,
                recommendation?.CommandTailYMM ?? double.NaN,
                recommendation?.CommandHeadXMM ?? double.NaN,
                recommendation?.CommandHeadYMM ?? double.NaN,
                recommendation?.InscribedMinorDiameterMM ?? double.NaN,
                recommendation?.UsableFullSheets ?? 0,
                recommendation?.TotalFullSheets ?? 0,
                recommendation?.UsableVeneerLengthMM ?? 0.0,
                quality,
                state.ProcessingElapsedSeconds > 0 ? state.ProcessingElapsedSeconds : stopwatch.Elapsed.TotalSeconds,
                string.Empty);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.Warn(ex, $"[BATCH] {relative} failed.");
            WriteReport(reportsFolder, relative, state.DiagnosticsReport + "\r\n\r\nREPLAY FAILED: " + ex);

            return new Row(
                relative, block, "Failed", state.ScanFrames?.Count ?? 0, 0, false, 0, string.Empty,
                double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0, 0.0,
                string.Empty, stopwatch.Elapsed.TotalSeconds, ex.Message);
        }
        finally
        {
            state.Clear();
        }
    }

    private static void WriteReport(string reportsFolder, string relative, string report)
    {
        string name = string.Join("_", relative.Split(Path.GetInvalidFileNameChars().Concat([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]).ToArray(), StringSplitOptions.RemoveEmptyEntries));
        File.WriteAllText(Path.Combine(reportsFolder, (name.Length == 0 ? "scan" : name) + ".txt"), report, Encoding.UTF8);
    }

    // ------------------------------------------------------------
    // CSV
    // ------------------------------------------------------------

    private static readonly string[] Header =
    [
        "ScanFolder", "Block", "Status", "Frames", "FramesUsed", "RevolutionVerified", "FramesPerRevolution",
        "Strategy", "TailXMM", "TailYMM", "HeadXMM", "HeadYMM", "InscribedDiameterMM",
        "UsableSheets", "TotalSheets", "UsableVeneerLengthMM", "ScanQuality", "ProcessingSeconds", "Error"
    ];

    internal static void WriteCsv(string path, IEnumerable<Row> rows)
    {
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", Header));

        foreach (Row r in rows)
        {
            csv.AppendLine(string.Join(",",
                Quote(r.ScanFolder), Quote(r.Block), Quote(r.Status),
                Num(r.Frames), Num(r.FramesUsed), r.RevolutionVerified ? "true" : "false", Num(r.FramesPerRevolution),
                Quote(r.Strategy), Num(r.TailXMM), Num(r.TailYMM), Num(r.HeadXMM), Num(r.HeadYMM),
                Num(r.InscribedDiameterMM), Num(r.UsableSheets), Num(r.TotalSheets), Num(r.UsableVeneerLengthMM),
                Quote(r.ScanQuality), Num(r.ProcessingSeconds), Quote(r.Error)));
        }

        File.WriteAllText(path, csv.ToString(), Encoding.UTF8);
    }

    internal static List<Row> ReadCsv(string path)
    {
        var rows = new List<Row>();
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);

        foreach (string line in lines.Skip(1))
        {
            List<string> f = SplitCsv(line);
            if (f.Count < Header.Length)
                continue;

            rows.Add(new Row(
                f[0], f[1], f[2], ParseInt(f[3]), ParseInt(f[4]), f[5] == "true", ParseInt(f[6]), f[7],
                ParseDouble(f[8]), ParseDouble(f[9]), ParseDouble(f[10]), ParseDouble(f[11]), ParseDouble(f[12]),
                ParseInt(f[13]), ParseInt(f[14]), ParseDouble(f[15]), f[16], ParseDouble(f[17]), f[18]));
        }

        return rows;
    }

    private static string Quote(string value) =>
        "\"" + (value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal) + "\"";

    private static string Num(double value) =>
        double.IsFinite(value) ? value.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static int ParseInt(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;

    private static double ParseDouble(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : double.NaN;

    private static List<string> SplitCsv(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }

    // ------------------------------------------------------------
    // Summary
    // ------------------------------------------------------------

    internal static double LargestCommandChangeMM(Row a, Row b) =>
        new[]
        {
            Math.Abs(a.TailXMM - b.TailXMM),
            Math.Abs(a.TailYMM - b.TailYMM),
            Math.Abs(a.HeadXMM - b.HeadXMM),
            Math.Abs(a.HeadYMM - b.HeadYMM)
        }.Max();

    internal static string BuildSummary(
        string root,
        IReadOnlyList<string> applied,
        IReadOnlyList<Row> rows,
        IReadOnlyList<Row>? baseline,
        string? baselinePath,
        double toleranceMM)
    {
        var text = new StringBuilder();
        static string F(double v) => double.IsFinite(v) ? v.ToString("0.00", CultureInfo.InvariantCulture) : "-";

        text.AppendLine("=== BATCH REPLAY ===");
        text.AppendLine(CultureInfo.InvariantCulture, $"Run: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Scans folder: {root}");
        text.AppendLine("Settings: the appsettings.json on this computer" +
            (applied.Count > 0 ? ", with these changes for this run only:" : " (no changes)."));
        foreach (string change in applied)
            text.AppendLine("  " + change);

        List<Row> ok = [.. rows.Where(r => r.Status == "OK")];
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture,
            $"Scans: {rows.Count}   OK: {ok.Count}   No result: {rows.Count(r => r.Status == "NoResult")}   Failed: {rows.Count(r => r.Status == "Failed")}");

        if (ok.Count > 0)
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"Revolution verified: {ok.Count(r => r.RevolutionVerified)} of {ok.Count}");
            text.AppendLine(CultureInfo.InvariantCulture,
                $"Processing time per scan: average {F(ok.Average(r => r.ProcessingSeconds))} s, slowest {F(ok.Max(r => r.ProcessingSeconds))} s");
            text.AppendLine(CultureInfo.InvariantCulture,
                $"Usable full sheets, all scans: {ok.Sum(r => r.UsableSheets)} of {ok.Sum(r => r.TotalSheets)}");
        }

        foreach (Row failed in rows.Where(r => r.Status == "Failed"))
            text.AppendLine($"  FAILED {failed.ScanFolder}: {failed.Error}");

        // Repeat scans of the same block: how far apart are the commands?
        var repeats = ok
            .GroupBy(r => Path.GetDirectoryName(r.ScanFolder) ?? r.Block, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() >= 2)
            .ToList();

        text.AppendLine();
        text.AppendLine("REPEAT SCANS OF THE SAME BLOCK (spread = largest minus smallest command, mm)");

        if (repeats.Count == 0)
        {
            text.AppendLine("  None found. Scan the same block two or more times (Scan_0001, Scan_0002, ...) to measure repeatability.");
        }
        else
        {
            foreach (var group in repeats)
            {
                double Spread(Func<Row, double> pick) => group.Max(pick) - group.Min(pick);
                text.AppendLine(CultureInfo.InvariantCulture,
                    $"  {group.Key} ({group.Count()} scans): tail X {F(Spread(r => r.TailXMM))}, tail Y {F(Spread(r => r.TailYMM))}, " +
                    $"head X {F(Spread(r => r.HeadXMM))}, head Y {F(Spread(r => r.HeadYMM))}");
            }
        }

        if (baseline is not null)
        {
            text.AppendLine();
            text.AppendLine(CultureInfo.InvariantCulture,
                $"COMPARED WITH BASELINE {baselinePath} (changed = any command moved more than {F(toleranceMM)} mm, or usable sheets changed)");

            var baseByScan = baseline
                .GroupBy(r => r.ScanFolder, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            int compared = 0, changed = 0, sheetsGained = 0, sheetsLost = 0;

            foreach (Row row in ok)
            {
                if (!baseByScan.TryGetValue(row.ScanFolder, out Row? before) || before.Status != "OK")
                    continue;

                compared++;
                double move = LargestCommandChangeMM(row, before);
                int sheetDelta = row.UsableSheets - before.UsableSheets;

                if (sheetDelta > 0) sheetsGained += sheetDelta;
                if (sheetDelta < 0) sheetsLost -= sheetDelta;

                if (move > toleranceMM || sheetDelta != 0)
                {
                    changed++;
                    text.AppendLine(CultureInfo.InvariantCulture,
                        $"  {row.ScanFolder}: largest command change {F(move)} mm, usable sheets {before.UsableSheets} -> {row.UsableSheets}, " +
                        $"inscribed diameter {F(before.InscribedDiameterMM)} -> {F(row.InscribedDiameterMM)} mm");
                }
            }

            text.AppendLine(CultureInfo.InvariantCulture,
                $"Scans compared: {compared}   Changed: {changed}   Usable sheets gained: {sheetsGained}   lost: {sheetsLost}");
        }

        return text.ToString();
    }

    private static string? ArgumentAfter(string[] args, string name)
    {
        int index = Array.FindIndex(args, argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));

        if (index < 0 || index + 1 >= args.Length)
            return null;

        string value = args[index + 1].Trim('"');
        return value.StartsWith("--", StringComparison.Ordinal) ? null : value;
    }
}
