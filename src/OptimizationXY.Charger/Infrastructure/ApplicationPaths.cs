// =============================================================================
// ApplicationPaths.cs - where the program keeps its writable files
// -----------------------------------------------------------------------------
// ClickOnce installs the program into a hidden, version-specific folder that
// is REPLACED on every update. Anything that must survive an update
// (settings, calibration, logs, saved scans) therefore lives under a separate
// data root, worked out once at startup:
//
//   1. If the OPTIMIZATION_XY_DATA_DIR environment variable is set, use it.
//   2. Otherwise use  %LOCALAPPDATA%\OptimizationXY\Charger
//      (e.g. C:\Users\<user>\AppData\Local\OptimizationXY\Charger).
//
// Folder layout under the root:
//   Config\                     appsettings.json
//   Config\Calibration\         active scanner calibration (lookup table + metadata)
//   Config\Calibration\Archive\ previous calibrations (for rollback)
//   Config\Calibration\Staging\ a calibration being built/validated before activation
//   Config\Calibration\InProgress\ half-finished calibration sessions that can be resumed
//   Config\CameraConfiguration\ optional machine-specific camera files
//   Config\Setup\               production setup wizard status files
//   Logs\                       NLog output, including OperatorDiagnostics.log
//   SavedScans\                 one Scan_#### folder per scan
// =============================================================================

namespace OptimizationXY.Charger.Infrastructure;

/// <summary>
/// Single source of truth for every folder and file path the program writes to.
/// All properties are computed from <see cref="RootDirectory"/>, so changing the
/// data root (environment variable) moves everything together.
/// </summary>
public static class ApplicationPaths
{
    /// <summary>Name of the environment variable that, when set, overrides the data root folder.</summary>
    public const string DataDirectoryEnvironmentVariable =
        "OPTIMIZATION_XY_DATA_DIR";

    /// <summary>The data root folder. Resolved once when the program starts (see <see cref="ResolveRootDirectory"/>).</summary>
    public static string RootDirectory { get; } = ResolveRootDirectory();

    /// <summary><c>&lt;root&gt;\Config</c> - holds appsettings.json and the sub-folders below.</summary>
    public static string ConfigDirectory => Path.Combine(RootDirectory, "Config");

    /// <summary><c>&lt;root&gt;\Logs</c> - NLog writes its log files here.</summary>
    public static string LogDirectory => Path.Combine(RootDirectory, "Logs");

    /// <summary>
    /// <c>&lt;root&gt;\Logs\OperatorDiagnostics.log</c> - a plain-language log of
    /// scan results and problems aimed at whoever is supporting the operator.
    /// </summary>
    public static string OperatorDiagnosticsLogPath =>Path.Combine(LogDirectory,"OperatorDiagnostics.log");

    /// <summary><c>&lt;root&gt;\Config\Calibration</c> - the ACTIVE scanner calibration lives here.</summary>
    public static string CalibrationDirectory => Path.Combine(ConfigDirectory, "Calibration");

    /// <summary><c>...\Calibration\Archive</c> - older calibration packages kept so a calibration can be rolled back.</summary>
    public static string CalibrationArchiveDirectory => Path.Combine(CalibrationDirectory, "Archive");

    /// <summary><c>...\Calibration\Staging</c> - a new calibration is written and checked here before it replaces the active one.</summary>
    public static string CalibrationStagingDirectory => Path.Combine(CalibrationDirectory, "Staging");

    /// <summary><c>...\Calibration\InProgress</c> - incomplete calibration sessions that can be resumed later.</summary>
    public static string CalibrationInProgressDirectory => Path.Combine(CalibrationDirectory, "InProgress");

    /// <summary>The active scanner lookup table (pixel to real-world mm conversion), <c>scanner-lookup-table.json</c>.</summary>
    public static string CalibrationLookupTablePath => Path.Combine(CalibrationDirectory, "scanner-lookup-table.json");

    /// <summary>Metadata describing the active calibration (when/how it was built, quality numbers), <c>scanner-calibration.json</c>.</summary>
    public static string CalibrationMetadataPath => Path.Combine(CalibrationDirectory, "scanner-calibration.json");

    /// <summary><c>&lt;root&gt;\Config\CameraConfiguration</c> - optional machine-specific camera files that override the copies shipped with the program.</summary>
    public static string CameraConfigurationDirectory => Path.Combine(ConfigDirectory, "CameraConfiguration");

    /// <summary><c>&lt;root&gt;\Config\Setup</c> - status files written by the production setup wizard.</summary>
    public static string SetupDirectory => Path.Combine(ConfigDirectory, "Setup");

    /// <summary><c>&lt;root&gt;\SavedScans</c> - each scan gets its own <c>Scan_####</c> folder here.</summary>
    public static string SavedScansDirectory => Path.Combine(RootDirectory, "SavedScans");

    /// <summary>
    /// Creates every folder above if it does not exist yet. Safe to call any number
    /// of times (Directory.CreateDirectory does nothing when the folder exists).
    /// </summary>
    public static void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(CalibrationDirectory);
        Directory.CreateDirectory(CalibrationArchiveDirectory);
        Directory.CreateDirectory(CalibrationStagingDirectory);
        Directory.CreateDirectory(CalibrationInProgressDirectory);
        Directory.CreateDirectory(SetupDirectory);
        Directory.CreateDirectory(SavedScansDirectory);

    }

    /// <summary>
    /// Turns a camera configuration file name (or path) from settings into a full path.
    /// </summary>
    /// <remarks>
    /// Search order: (1) an absolute path is used as-is; (2) a file with the same
    /// name in <see cref="CameraConfigurationDirectory"/> (machine-specific copy);
    /// (3) the <c>CameraConfiguration</c> folder shipped beside the .exe.
    /// Returns an empty string when no name is configured.
    /// </remarks>
    public static string ResolveCameraConfigurationPath(string fileNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(fileNameOrPath))
        {
            return string.Empty;
        }

        // Expand environment variables and trim whitespace from the input path.
        string expanded = Environment.ExpandEnvironmentVariables(
            fileNameOrPath.Trim());

        if (Path.IsPathRooted(expanded))
        {
            return Path.GetFullPath(expanded);
        }

        // Check for a machine-specific configuration file in the CameraConfigurationDirectory.
        string machineSpecificPath = Path.Combine(
            CameraConfigurationDirectory,
            Path.GetFileName(expanded));

        if (File.Exists(machineSpecificPath))
        {
            return machineSpecificPath;
        }

        return Path.Combine(
            AppContext.BaseDirectory,
            "CameraConfiguration",
            Path.GetFileName(expanded));
    }



    /// <summary>
    /// Works out the data root: the OPTIMIZATION_XY_DATA_DIR environment variable
    /// if set, otherwise %LOCALAPPDATA%\OptimizationXY\Charger (or the temp folder
    /// if Windows reports no local app-data folder).
    /// </summary>
    private static string ResolveRootDirectory()
    {
        string? configuredDirectory =
            Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(configuredDirectory));
        }

        // If the environment variable is not set, use the local application data folder.
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.GetTempPath();
        }

        return Path.Combine(localAppData, "OptimizationXY", "Charger");
    }
}
