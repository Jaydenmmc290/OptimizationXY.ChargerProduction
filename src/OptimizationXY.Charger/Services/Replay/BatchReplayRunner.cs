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
///       [--same-block "B01,B02,B03"] ...                scans of one block (repeatable switch);
///                                                       default: scans in the same parent folder
///
/// A scan folder is any folder that contains a "Frames" folder. Its calibration
/// is the scanner-calibration.json saved with it. Nothing is sent to the PLC,
/// nothing is written to SQL, cameras are never opened, and nothing is written
/// into the scan folders.
///
/// Results (in the output folder):
///   summary.csv  one line per scan: commands, inscribed diameter, sheets, ...
///   summary.txt  totals, scans that changed against the baseline, and how
///                well repeat scans of the same block agree. The block is
///                re-chucked between scans, so repeat scans are compared inside
///                the block (axis distance from the block's own centreline,
///                inscribed diameter, sheets), not by machine commands.
///   reports\     the full diagnostics report of every scan
/// </summary>
internal static class BatchReplayRunner
{
    public const string CommandLineSwitch = "--replay-batch";

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>True when the command line contains the --replay-batch switch.</summary>
    public static bool IsRequested(string[] args) =>
        args.Any(argument => string.Equals(argument, CommandLineSwitch, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// One line of summary.csv: the result of replaying one saved scan.
    /// Command values (TailXMM … HeadYMM) are the machine moves the PLC would have
    /// been sent, in mm. AxisFromCenterlineMM is how far the chosen spindle axis
    /// is from the block's own centreline (NaN when the centreline pull was not
    /// used); it is the value used to compare repeat scans of one block.
    /// Status is "OK", "NoResult" (pipeline ran but gave no recommendation) or
    /// "Failed" (see Error).
    /// </summary>
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
        string Error,
        double RevolutionFrames = double.NaN,
        double AxisFromCenterlineMM = double.NaN);

    /// <summary>
    /// Entry point for --replay-batch: reads the switches (or asks for the root
    /// folder), runs <see cref="RunBatch"/> and opens/shows the summary.
    /// </summary>
    /// <param name="args">The program's command-line arguments.</param>
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
                toleranceMM,
                ParseSameBlockGroups(args));

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
        double toleranceMM,
        IReadOnlyList<IReadOnlyList<string>>? sameBlockGroups = null)
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
            BuildSummary(root, applied, rows, baseline, baselinePath, toleranceMM, sameBlockGroups),
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

    /// <summary>
    /// Replays one scan folder with the calibration saved in it, writes its full
    /// diagnostics report into reports\ and returns its summary line. Any error is
    /// caught and returned as a "Failed" row so one bad scan does not stop the batch.
    /// The shared pipeline state is cleared before and after.
    /// </summary>
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
            var selectedGeometry =
                state.GeometryPeelComparison?.SelectedGeometry ?? state.LargestInscribedCylinder;
            double axisFromCenterline =
                selectedGeometry is { CenterlineAnchorUsed: true }
                    ? selectedGeometry.CenterlineDeviationMM
                    : double.NaN;

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
                string.Empty,
                state.RevolutionAngles?.RevolutionFrames ?? double.NaN,
                axisFromCenterline);
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

    /// <summary>
    /// Saves a scan's diagnostics report as reports\&lt;relative path with '_'&gt;.txt.
    /// </summary>
    private static void WriteReport(string reportsFolder, string relative, string report)
    {
        string name = string.Join("_", relative.Split(Path.GetInvalidFileNameChars().Concat([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]).ToArray(), StringSplitOptions.RemoveEmptyEntries));
        File.WriteAllText(Path.Combine(reportsFolder, (name.Length == 0 ? "scan" : name) + ".txt"), report, Encoding.UTF8);
    }

    // ------------------------------------------------------------
    // CSV
    // ------------------------------------------------------------

    // Column order of summary.csv. Keep it in step with WriteCsv and ReadCsv.
    private static readonly string[] Header =
    [
        "ScanFolder", "Block", "Status", "Frames", "FramesUsed", "RevolutionVerified", "FramesPerRevolution",
        "Strategy", "TailXMM", "TailYMM", "HeadXMM", "HeadYMM", "InscribedDiameterMM",
        "UsableSheets", "TotalSheets", "UsableVeneerLengthMM", "ScanQuality", "ProcessingSeconds", "Error",
        "RevolutionFrames", "AxisFromCenterlineMM"
    ];
    internal static readonly char[] separator = new[] { ',', ';' };

    /// <summary>Writes the rows as summary.csv (invariant culture, quoted text).</summary>
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
                Quote(r.ScanQuality), Num(r.ProcessingSeconds), Quote(r.Error),
                Num(r.RevolutionFrames), Num(r.AxisFromCenterlineMM)));
        }

        File.WriteAllText(path, csv.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// Reads a summary.csv written earlier (used as the --baseline). Columns are
    /// matched by name; missing numbers read as NaN or 0.
    /// </summary>
    internal static List<Row> ReadCsv(string path)
    {
        var rows = new List<Row>();
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);

        if (lines.Length == 0)
            return rows;

        // Columns are found by name, so summaries written by an older version
        // (without the newer columns) still work as a baseline.
        List<string> names = SplitCsv(lines[0]);
        var column = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < names.Count; i++)
            column.TryAdd(names[i].Trim().Trim('\uFEFF'), i);

        foreach (string line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            List<string> f = SplitCsv(line);
            string Get(string name) =>
                column.TryGetValue(name, out int i) && i < f.Count ? f[i] : string.Empty;

            if (Get("ScanFolder").Length == 0)
                continue;

            rows.Add(new Row(
                Get("ScanFolder"), Get("Block"), Get("Status"), ParseInt(Get("Frames")), ParseInt(Get("FramesUsed")),
                Get("RevolutionVerified") == "true", ParseInt(Get("FramesPerRevolution")), Get("Strategy"),
                ParseDouble(Get("TailXMM")), ParseDouble(Get("TailYMM")), ParseDouble(Get("HeadXMM")), ParseDouble(Get("HeadYMM")),
                ParseDouble(Get("InscribedDiameterMM")), ParseInt(Get("UsableSheets")), ParseInt(Get("TotalSheets")),
                ParseDouble(Get("UsableVeneerLengthMM")), Get("ScanQuality"), ParseDouble(Get("ProcessingSeconds")), Get("Error"),
                ParseDouble(Get("RevolutionFrames")), ParseDouble(Get("AxisFromCenterlineMM"))));
        }

        return rows;
    }

    /// <summary>CSV text field: quoted, inner quotes doubled, line breaks replaced by spaces.</summary>
    private static string Quote(string value) =>
        "\"" + (value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal) + "\"";

    /// <summary>Number for the CSV (up to 3 decimals); empty when NaN or infinite.</summary>
    private static string Num(double value) =>
        double.IsFinite(value) ? value.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>Whole number for the CSV.</summary>
    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Reads a CSV whole number; 0 when it cannot be read.</summary>
    private static int ParseInt(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;

    /// <summary>Reads a CSV number; NaN when it cannot be read (for example an empty cell).</summary>
    private static double ParseDouble(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : double.NaN;

    /// <summary>Splits one CSV line into fields, honouring quotes and doubled quotes.</summary>
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

    /// <summary>
    /// Largest difference, in mm, between two runs' four commands
    /// (tail X/Y and head X/Y). Compared with --tolerance-mm to decide "changed".
    /// </summary>
    internal static double LargestCommandChangeMM(Row a, Row b) =>
        new[]
        {
            Math.Abs(a.TailXMM - b.TailXMM),
            Math.Abs(a.TailYMM - b.TailYMM),
            Math.Abs(a.HeadXMM - b.HeadXMM),
            Math.Abs(a.HeadYMM - b.HeadYMM)
        }.Max();

    /// <summary>
    /// Builds summary.txt: settings tried, totals, scans whose commands moved
    /// more than the tolerance against the baseline, and how well repeat scans of
    /// the same block agree.
    /// </summary>
    internal static string BuildSummary(
        string root,
        IReadOnlyList<string> applied,
        IReadOnlyList<Row> rows,
        IReadOnlyList<Row>? baseline,
        string? baselinePath,
        double toleranceMM,
        IReadOnlyList<IReadOnlyList<string>>? sameBlockGroups = null)
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

        // Repeat scans of the same block. The block is dropped and re-chucked
        // between scans, so its position on the spindles (and therefore the
        // machine commands) is expected to change. What should repeat is the
        // result inside the block: where the peel axis sits relative to the
        // block's own centreline, the inscribed diameter and the sheets.
        List<(string Name, List<Row> Scans)> repeats = GroupRepeatScans(ok, sameBlockGroups);

        text.AppendLine();
        text.AppendLine("REPEAT SCANS OF THE SAME BLOCK (compared inside the block, because it is re-chucked between scans)");
        text.AppendLine("  axis = distance of the chosen peel axis from the block's own fitted centreline (mm, largest of tail/head)");

        if (repeats.Count == 0)
        {
            text.AppendLine(sameBlockGroups is { Count: > 0 }
                ? "  None found. No --same-block list matched two or more scans."
                : "  None found. Put repeat scans of one block in the same folder, or list them with --same-block \"B01,B02,B03\".");
        }
        else
        {
            foreach (var (name, scans) in repeats)
            {
                double Spread(Func<Row, double> pick)
                {
                    var values = scans.Select(pick).Where(double.IsFinite).ToList();
                    return values.Count >= 2 ? values.Max() - values.Min() : double.NaN;
                }

                string Range(Func<Row, double> pick, string format)
                {
                    var values = scans.Select(pick).Where(double.IsFinite).ToList();
                    return values.Count == 0
                        ? "-"
                        : values.Min().ToString(format, CultureInfo.InvariantCulture) + " to " +
                          values.Max().ToString(format, CultureInfo.InvariantCulture);
                }

                int cylinders = scans.Count(r => r.Strategy.Contains("Cylinder", StringComparison.OrdinalIgnoreCase));
                int ovals = scans.Count(r => r.Strategy.Contains("Oval", StringComparison.OrdinalIgnoreCase));
                int unverified = scans.Count(r => !r.RevolutionVerified);

                string unverifiedText = unverified > 0 ? $" ({unverified} not verified)" : string.Empty;

                text.AppendLine(CultureInfo.InvariantCulture,
                    $"  {name} ({scans.Count} scans): axis {Range(r => r.AxisFromCenterlineMM, "0.0")} mm, " +
                    $"inscribed diameter spread {F(Spread(r => r.InscribedDiameterMM))} mm, " +
                    $"usable sheets {Range(r => r.UsableSheets, "0")}, " +
                    $"revolution {Range(r => double.IsFinite(r.RevolutionFrames) ? r.RevolutionFrames : r.FramesPerRevolution, "0.#")} frames" +
                    $"{unverifiedText}, cylinder/oval {cylinders}/{ovals}");
                text.AppendLine(CultureInfo.InvariantCulture,
                    $"      machine command spread (only meaningful if the block was not re-chucked): " +
                    $"tail X {F(Spread(r => r.TailXMM))}, tail Y {F(Spread(r => r.TailYMM))}, " +
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

    /// <summary>
    /// Groups repeat scans of one block. With --same-block lists, each list is a
    /// group; a name matches a scan's block folder, its own folder name, or its
    /// relative path. Without lists, scans in the same parent folder are a group.
    /// </summary>
    internal static List<(string Name, List<Row> Scans)> GroupRepeatScans(
        IReadOnlyList<Row> rows,
        IReadOnlyList<IReadOnlyList<string>>? sameBlockGroups)
    {
        var groups = new List<(string Name, List<Row> Scans)>();

        if (sameBlockGroups is { Count: > 0 })
        {
            foreach (IReadOnlyList<string> names in sameBlockGroups)
            {
                static string Normal(string path) => path.Replace('/', '\\').Trim('\\');

                // "09\11\Block_0001" matches every scan under that folder.
                bool Matches(Row row, string name)
                {
                    string scan = Normal(row.ScanFolder);
                    string wanted = Normal(name);

                    return string.Equals(row.Block, name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetFileName(row.ScanFolder), name, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(scan, wanted, StringComparison.OrdinalIgnoreCase) ||
                        scan.StartsWith(wanted + "\\", StringComparison.OrdinalIgnoreCase);
                }

                List<Row> scans = [.. rows.Where(row => names.Any(name => Matches(row, name)))];

                if (scans.Count >= 2)
                    groups.Add((string.Join(",", names), scans));
            }

            return groups;
        }

        foreach (var group in rows
            .GroupBy(r => Path.GetDirectoryName(r.ScanFolder) is { Length: > 0 } parent ? parent : r.Block, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() >= 2))
        {
            groups.Add((group.Key, group.ToList()));
        }

        return groups;
    }

    /// <summary>
    /// Reads every --same-block "A,B,C" switch (commas or semicolons) into a list
    /// of block-name groups.
    /// </summary>
    internal static List<IReadOnlyList<string>> ParseSameBlockGroups(string[] args)
    {
        var groups = new List<IReadOnlyList<string>>();

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], "--same-block", StringComparison.OrdinalIgnoreCase))
                continue;

            string[] names = args[i + 1]
                .Trim('"')
                .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (names.Length > 0)
                groups.Add(names);
        }

        return groups;
    }

    /// <summary>
    /// The value that follows a switch, for example the folder after --out.
    /// Null when the switch is missing or is followed by another switch.
    /// </summary>
    private static string? ArgumentAfter(string[] args, string name)
    {
        int index = Array.FindIndex(args, argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));

        if (index < 0 || index + 1 >= args.Length)
            return null;

        string value = args[index + 1].Trim('"');
        return value.StartsWith("--", StringComparison.Ordinal) ? null : value;
    }
}
