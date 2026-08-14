namespace OptimizationXY.Charger.Infrastructure;

public static class ApplicationPaths
{
    // Environment variable name for the root data directory.
    public const string DataDirectoryEnvironmentVariable =
        "OPTIMIZATION_XY_DATA_DIR";

    // Root directory for application data, resolved from environment variable or default location.
    public static string RootDirectory { get; } = ResolveRootDirectory();

    // Subdirectories for various application data.
    public static string ConfigDirectory => Path.Combine(RootDirectory, "Config");

    // Directory for log files.
    public static string LogDirectory => Path.Combine(RootDirectory, "Logs");

    public static string OperatorDiagnosticsLogPath =>Path.Combine(LogDirectory,"OperatorDiagnostics.log");

    // Directory for calibration data, including archive and staging.
    public static string CalibrationDirectory => Path.Combine(ConfigDirectory, "Calibration");

    // Directory for archived calibration data.
    public static string CalibrationArchiveDirectory => Path.Combine(CalibrationDirectory, "Archive");

    // Directory for staging calibration data before finalization.
    public static string CalibrationStagingDirectory => Path.Combine(CalibrationDirectory, "Staging");

    // Paths for specific calibration files.
    public static string CalibrationLookupTablePath => Path.Combine(CalibrationDirectory, "scanner-lookup-table.json");

    // Path for calibration metadata file.
    public static string CalibrationMetadataPath => Path.Combine(CalibrationDirectory, "scanner-calibration.json");

    // Directory for camera configuration files.
    public static string CameraConfigurationDirectory => Path.Combine(ConfigDirectory, "CameraConfiguration");

    // Directory for setup-related files.
    public static string SetupDirectory => Path.Combine(ConfigDirectory, "Setup");

    // Directory for saved scan data.
    public static string SavedScansDirectory => Path.Combine(RootDirectory, "SavedScans");

    // Ensures that all necessary directories exist, creating them if they do not.
    public static void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(CalibrationDirectory);
        Directory.CreateDirectory(CalibrationArchiveDirectory);
        Directory.CreateDirectory(CalibrationStagingDirectory);
        Directory.CreateDirectory(SetupDirectory);
        Directory.CreateDirectory(SavedScansDirectory);

    }

    // Resolves the full path to a camera configuration file, checking for machine-specific overrides.
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



    // Resolves the root directory for application data, using an environment
    // variable if set, or falling back to a default location.
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
