using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using NLog;
using OptimizationXY.Charger.Models.Calibration;
using OptimizationXY.Charger.Models.Core;
using OptimizationXY.Charger.Models.Operator;
using OptimizationXY.Charger.Services.Configuration;
using OptimizationXY.Charger.Services.Core;
using OptimizationXY.Charger.Services.Operator;
using OptimizationXY.Charger.Services.Scan;

namespace OptimizationXY.Charger.Services.Replay;

/// <summary>
/// Re-processes the saved frames of a previous scan on a computer that has no
/// cameras and no PLC, so changes to the processing can be checked against real
/// production scans before they are published.
///
/// Start the program with:   OptimizationXY.Charger.exe --replay
/// or:                       OptimizationXY.Charger.exe --replay "C:\path\to\Scan_0001"
///
/// The folder is a saved scan folder (containing a "Frames" subfolder and
/// "scanner-calibration.json") or any folder of frame images. Nothing is sent
/// to the PLC, nothing is written to SQL, and the cameras are never opened.
/// The report is written next to the frames as ReplayDiagnosticsReport_*.txt.
/// </summary>
internal static class OfflineReplayRunner
{
    public const string CommandLineSwitch = "--replay";

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool IsRequested(string[] args)
    {
        return args.Any(argument =>
            string.Equals(
                argument,
                CommandLineSwitch,
                StringComparison.OrdinalIgnoreCase));
    }

    public static void Run(string[] args)
    {
        try
        {
            string? scanFolder = ResolveScanFolder(args);
            if (scanFolder is null)
                return;

            string framesFolder = ResolveFramesFolder(scanFolder);

            ScannerLookupTable? lookupTable =
                ResolveCalibration(scanFolder);
            if (lookupTable is null)
                return;


            List<ScanFrame> frames =
                ScanFrameLoader.LoadFromFolder(framesFolder);

            Logger.Info(
                $"[REPLAY] Processing {frames.Count} frames from '{framesFolder}' " +
                $"with calibration {lookupTable.CalibrationId}.");

            PipelineState state = PipelineState.Instance;
            state.Clear();

            var header = new StringBuilder();
            header.AppendLine("=== OFFLINE REPLAY ===");
            header.AppendLine(CultureInfo.InvariantCulture, $"Replayed: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            header.AppendLine(CultureInfo.InvariantCulture, $"Frames Folder: {framesFolder}");
            header.AppendLine(CultureInfo.InvariantCulture, $"Frames Loaded: {frames.Count}");
            header.AppendLine(CultureInfo.InvariantCulture, $"Calibration: {lookupTable.CalibrationId}");
            header.AppendLine("PLC: not contacted. SQL: not written. Cameras: not opened.");
            header.AppendLine(CultureInfo.InvariantCulture,
                $"Settings: the appsettings.json on THIS computer was used. Copy the production " +
                $"appsettings.json here first if the result must match production settings.");
            header.AppendLine();

            if (state.Settings.Diagnostics.IncludeSettingsSummary)
            {
                state.DiagnosticsReport +=
                    AppSettingsDiagnosticsBuilder.BuildSummary(state.Settings) +
                    Environment.NewLine;
            }

            state.ScanFrames = frames;

            if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                Cursor.Current = Cursors.WaitCursor;

            LogProcessingPipeline.Instance.RunCurrentFramesPipeline(
                lookupTable,
                CancellationToken.None,
                message => Logger.Info($"[REPLAY] {message}"));

            var summary = new StringBuilder();
            summary.AppendLine("=== REPLAY RESULT ===");

            var recommendation = state.FinalSpindleRecommendation;
            if (recommendation is null)
            {
                summary.AppendLine("No final spindle recommendation was produced.");
            }
            else
            {
                summary.AppendLine(CultureInfo.InvariantCulture, $"Selected Strategy: {recommendation.SelectedStrategy}");
                summary.AppendLine(CultureInfo.InvariantCulture,
                    $"Command Tail X / Y: {recommendation.CommandTailXMM / 25.4:F3} / {recommendation.CommandTailYMM / 25.4:F3} in   " +
                    $"({recommendation.CommandTailXMM:F2} / {recommendation.CommandTailYMM:F2} mm)");
                summary.AppendLine(CultureInfo.InvariantCulture,
                    $"Command Head X / Y: {recommendation.CommandHeadXMM / 25.4:F3} / {recommendation.CommandHeadYMM / 25.4:F3} in   " +
                    $"({recommendation.CommandHeadXMM:F2} / {recommendation.CommandHeadYMM:F2} mm)");
            }

            try
            {
                OperatorScanQualityResult quality =
                    OperatorScanQualityAnalyzer.Analyze(state);
                state.OperatorScanQuality = quality;

                summary.AppendLine(CultureInfo.InvariantCulture,
                    $"Scan Quality: {quality.Status} | frames used {quality.FramesUsed} | " +
                    $"coverage {quality.ProfileCoveragePercent:F1}% | valid slices {quality.ValidSlicePercent:F1}%");
                summary.AppendLine(quality.OperatorMessage);
            }
            catch (Exception qualityException)
            {
                summary.AppendLine(CultureInfo.InvariantCulture,
                    $"Scan quality could not be evaluated: {qualityException.Message}");
            }

            summary.AppendLine();

            string reportPath = Path.Combine(
                scanFolder,
                $"ReplayDiagnosticsReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            File.WriteAllText(
                reportPath,
                header.ToString() + summary + state.DiagnosticsReport,
                Encoding.UTF8);

            Logger.Info($"[REPLAY] Report written to '{reportPath}'.");
            Logger.Info(summary.ToString());

            if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                Cursor.Current = Cursors.Default;

                MessageBox.Show(
                    $"Replay complete.\r\n\r\n{summary}\r\nFull report:\r\n{reportPath}",
                    "Optimization XY Charger - Offline Replay",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            try
            {
                Process.Start(new ProcessStartInfo(reportPath)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception openException)
            {
                Logger.Warn(openException, "[REPLAY] The report could not be opened automatically.");
            }
        }
        catch (Exception ex)
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
                Cursor.Current = Cursors.Default;

            Logger.Error(ex, "[REPLAY] Offline replay failed.");

            if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                MessageBox.Show(
                    $"Offline replay failed:\r\n\r\n{ex.Message}",
                    "Optimization XY Charger - Offline Replay",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        finally
        {
            PipelineState.Instance.Clear();
        }
    }

    private static string? ResolveScanFolder(string[] args)
    {
        int switchIndex = Array.FindIndex(
            args,
            argument => string.Equals(
                argument,
                CommandLineSwitch,
                StringComparison.OrdinalIgnoreCase));

        if (switchIndex >= 0 && switchIndex + 1 < args.Length)
        {
            string candidate = args[switchIndex + 1].Trim('"');

            if (Directory.Exists(candidate))
                return Path.GetFullPath(candidate);

            throw new DirectoryNotFoundException(
                $"Replay folder was not found: {candidate}");
        }

        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            return null;

        using var dialog = new FolderBrowserDialog
        {
            Description =
                "Select a saved scan folder (for example Scan_0001) or a folder of frame images.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        return dialog.ShowDialog() == DialogResult.OK &&
               Directory.Exists(dialog.SelectedPath)
            ? dialog.SelectedPath
            : null;
    }

    private static string ResolveFramesFolder(string scanFolder)
    {
        string framesFolder = Path.Combine(scanFolder, "Frames");

        return Directory.Exists(framesFolder)
            ? framesFolder
            : scanFolder;
    }

    private static ScannerLookupTable? ResolveCalibration(string scanFolder)
    {
        string?[] candidates =
        [
            Path.Combine(scanFolder, "scanner-calibration.json"),
            Path.Combine(scanFolder, "scanner-lookup-table.json"),
            Path.Combine(Path.GetDirectoryName(scanFolder) ?? scanFolder, "scanner-calibration.json"),
            Path.Combine(Path.GetDirectoryName(scanFolder) ?? scanFolder, "scanner-lookup-table.json")
        ];

        foreach (string? candidate in candidates)
        {
            if (candidate is null || !File.Exists(candidate))
                continue;

            ScannerLookupTable? table = TryLoadCalibration(candidate);
            if (table is not null)
                return table;
        }

        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            return null;

        using var dialog = new OpenFileDialog
        {
            Title = "Select the scanner calibration used for this scan (scanner-lookup-table.json)",
            Filter = "Calibration JSON (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return null;

        ScannerLookupTable? selected = TryLoadCalibration(dialog.FileName);

        return selected ?? throw new InvalidDataException(
            $"'{dialog.FileName}' is not a valid scanner lookup table with calibration points.");
    }

    private static ScannerLookupTable? TryLoadCalibration(string path)
    {
        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);

            ScannerLookupTable? table =
                JsonSerializer.Deserialize<ScannerLookupTable>(json, JsonOptions);

            if (table is not null && table.IsValid)
            {
                Logger.Info($"[REPLAY] Calibration loaded from '{path}'.");
                return table;
            }

            Logger.Warn($"[REPLAY] '{path}' is not a complete lookup table (no usable calibration points).");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, $"[REPLAY] Could not read calibration file '{path}'.");
            return null;
        }
    }
}
