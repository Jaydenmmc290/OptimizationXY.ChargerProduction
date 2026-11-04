using System.Globalization;
using System.Text;
using System.Text.Json;
using NLog;
using OptimizationXY.Charger.Models.Calibration;
using OptimizationXY.Charger.Models.Core;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Configuration;
using OptimizationXY.Charger.Services.Core;
using OptimizationXY.Charger.Services.Scan;

namespace OptimizationXY.Charger.Services.Replay;

// =============================================================================
// GoldenScans - saved production scans whose answer must not change.
// -----------------------------------------------------------------------------
// A golden scan is a saved scan folder (Frames\ + scanner-calibration.json)
// with two extra files written once by
//
//   OptimizationXY.Charger.exe --golden-record "C:\path\to\golden-scans"
//
//   golden-settings.json  the settings the answer was recorded with
//   golden-expected.json  the answer: strategy, head/tail X/Y, inscribed
//                         diameter, usable / total sheets, usable veneer length
//
// The automated tests (GoldenScanTests) replay every golden scan under
// tests\golden-scans (or the folder in the OXY_GOLDEN_SCANS environment
// variable) with ITS settings and calibration and fail if the answer moved.
// That is what protects "the XY method and the virtual peel stay the same":
// any code change that moves a result fails the GitHub build. Re-record only
// on purpose, when a change of result is intended.
// Same idea as the Rebuild program's GoldenScanTests.
// =============================================================================

/// <summary>The recorded answer of one golden scan.</summary>
public sealed record GoldenExpectation(
    string Strategy,
    double TailXMM,
    double TailYMM,
    double HeadXMM,
    double HeadYMM,
    double InscribedDiameterMM,
    int UsableSheets,
    int TotalSheets,
    double UsableVeneerLengthMM);

/// <summary>Records and replays golden scans. See the file header.</summary>
public static class GoldenScans
{
    public const string RecordSwitch = "--golden-record";
    public const string SettingsFile = "golden-settings.json";
    public const string ExpectedFile = "golden-expected.json";
    public const string EnvironmentVariable = "OXY_GOLDEN_SCANS";

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>True when the command line contains --golden-record.</summary>
    public static bool IsRecordRequested(string[] args) =>
        args.Any(a => string.Equals(a, RecordSwitch, StringComparison.OrdinalIgnoreCase));

    /// <summary>Scan folders under <paramref name="root"/> that have a recorded answer.</summary>
    public static List<string> FindGoldenScans(string root) =>
        Directory.Exists(root)
            ? [.. BatchReplayRunner.FindScanFolders(root).Where(f => File.Exists(Path.Combine(f, ExpectedFile)))]
            : [];

    /// <summary>
    /// Replays one scan folder with <paramref name="settings"/> and its saved
    /// calibration and returns the answer. Uses (and clears) the shared pipeline state.
    /// </summary>
    public static GoldenExpectation Replay(string scanFolder, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        PipelineState state = PipelineState.Instance;
        AppSettings previous = state.Settings;

        try
        {
            state.Clear();
            state.Settings = settings;

            ScannerLookupTable table =
                OfflineReplayRunner.LoadSavedCalibration(scanFolder)
                ?? throw new InvalidDataException($"No valid scanner-calibration.json in {scanFolder}.");

            List<ScanFrame> frames = ScanFrameLoader.LoadFromFolder(Path.Combine(scanFolder, "Frames"));
            state.ScanFrames = frames;

            LogProcessingPipeline.Instance.RunCurrentFramesPipeline(table, CancellationToken.None);

            var r = state.FinalSpindleRecommendation
                ?? throw new InvalidOperationException($"No recommendation was produced for {scanFolder}.");

            return new GoldenExpectation(
                r.SelectedStrategy,
                r.CommandTailXMM,
                r.CommandTailYMM,
                r.CommandHeadXMM,
                r.CommandHeadYMM,
                r.InscribedMinorDiameterMM,
                r.UsableFullSheets,
                r.TotalFullSheets,
                r.UsableVeneerLengthMM);
        }
        finally
        {
            state.Clear();
            state.Settings = previous;
        }
    }

    /// <summary>The settings recorded with a golden scan.</summary>
    public static AppSettings LoadSettings(string scanFolder) =>
        JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path.Combine(scanFolder, SettingsFile)), JsonOptions)
        ?? throw new InvalidDataException($"{SettingsFile} in {scanFolder} is empty.");

    /// <summary>The answer recorded with a golden scan.</summary>
    public static GoldenExpectation LoadExpected(string scanFolder) =>
        JsonSerializer.Deserialize<GoldenExpectation>(File.ReadAllText(Path.Combine(scanFolder, ExpectedFile)), JsonOptions)
        ?? throw new InvalidDataException($"{ExpectedFile} in {scanFolder} is empty.");

    /// <summary>
    /// Differences between two answers beyond the tolerance (mm for positions
    /// and lengths, exact for the strategy and sheet counts); empty when equal.
    /// </summary>
    public static List<string> Compare(GoldenExpectation expected, GoldenExpectation actual, double toleranceMM = 0.01)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        CultureInfo ci = CultureInfo.InvariantCulture;
        var differences = new List<string>();

        void Number(string name, double e, double a)
        {
            if (Math.Abs(e - a) > toleranceMM)
                differences.Add($"{name} {e.ToString("0.000", ci)} -> {a.ToString("0.000", ci)} mm");
        }

        if (!string.Equals(expected.Strategy, actual.Strategy, StringComparison.Ordinal))
            differences.Add($"strategy '{expected.Strategy}' -> '{actual.Strategy}'");

        Number("tail X", expected.TailXMM, actual.TailXMM);
        Number("tail Y", expected.TailYMM, actual.TailYMM);
        Number("head X", expected.HeadXMM, actual.HeadXMM);
        Number("head Y", expected.HeadYMM, actual.HeadYMM);
        Number("inscribed diameter", expected.InscribedDiameterMM, actual.InscribedDiameterMM);
        Number("usable veneer length", expected.UsableVeneerLengthMM, actual.UsableVeneerLengthMM);

        if (expected.UsableSheets != actual.UsableSheets)
            differences.Add($"usable sheets {expected.UsableSheets} -> {actual.UsableSheets}");

        if (expected.TotalSheets != actual.TotalSheets)
            differences.Add($"total sheets {expected.TotalSheets} -> {actual.TotalSheets}");

        return differences;
    }

    /// <summary>
    /// --golden-record: for every saved scan folder under the given folder,
    /// writes golden-settings.json (this computer's appsettings.json) and
    /// golden-expected.json (the answer this build gives).
    /// </summary>
    public static void Record(string[] args)
    {
        try
        {
            int index = Array.FindIndex(args, a => string.Equals(a, RecordSwitch, StringComparison.OrdinalIgnoreCase));
            string? root = index >= 0 && index + 1 < args.Length ? args[index + 1].Trim('"') : null;

            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                throw new DirectoryNotFoundException($"Give the golden scans folder after {RecordSwitch}.");

            AppSettings settings = AppSettingsStore.LoadOrCreateDefault();
            var log = new StringBuilder();

            foreach (string folder in BatchReplayRunner.FindScanFolders(root))
            {
                GoldenExpectation answer = Replay(folder, settings);

                File.WriteAllText(Path.Combine(folder, SettingsFile), JsonSerializer.Serialize(settings, JsonOptions), Encoding.UTF8);
                File.WriteAllText(Path.Combine(folder, ExpectedFile), JsonSerializer.Serialize(answer, JsonOptions), Encoding.UTF8);

                log.AppendLine(CultureInfo.InvariantCulture,
                    $"{Path.GetRelativePath(root, folder)}: {answer.Strategy}, tail {answer.TailXMM:F2}/{answer.TailYMM:F2}, head {answer.HeadXMM:F2}/{answer.HeadYMM:F2} mm, {answer.UsableSheets}/{answer.TotalSheets} sheets");
            }

            Logger.Info("[GOLDEN] Recorded:" + Environment.NewLine + log);

            if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                MessageBox.Show(
                    log.Length > 0 ? "Golden answers recorded:\r\n\r\n" + log : "No saved scan folder (with a Frames folder) was found.",
                    "Optimization XY Charger - Golden scans",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[GOLDEN] Recording failed.");

            if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                MessageBox.Show(
                    $"Golden recording failed:\r\n\r\n{ex.Message}",
                    "Optimization XY Charger - Golden scans",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
