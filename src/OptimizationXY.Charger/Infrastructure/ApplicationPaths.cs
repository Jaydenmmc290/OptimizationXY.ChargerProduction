namespace OptimizationXY.Charger.Infrastructure;

public static class ApplicationPaths
{
    public const string DataDirectoryEnvironmentVariable =
        "OPTIMIZATION_XY_DATA_DIR";

    public static string RootDirectory { get; } = ResolveRootDirectory();
    public static string ConfigDirectory => Path.Combine(RootDirectory, "Config");
    public static string LogDirectory => Path.Combine(RootDirectory, "Logs");
    public static string CalibrationDirectory => Path.Combine(ConfigDirectory, "Calibration");
    public static string CalibrationArchiveDirectory => Path.Combine(CalibrationDirectory, "Archive");
    public static string CalibrationStagingDirectory => Path.Combine(CalibrationDirectory, "Staging");
    public static string CalibrationLookupTablePath => Path.Combine(CalibrationDirectory, "scanner-lookup-table.json");
    public static string CalibrationMetadataPath => Path.Combine(CalibrationDirectory, "scanner-calibration.json");
    public static string CameraConfigurationDirectory => Path.Combine(ConfigDirectory, "CameraConfiguration");
    public static string SetupDirectory => Path.Combine(ConfigDirectory, "Setup");
    public static string SavedScansDirectory => Path.Combine(RootDirectory, "SavedScans");

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

    public static string ResolveCameraConfigurationPath(string fileNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(fileNameOrPath))
        {
            return string.Empty;
        }

        string expanded = Environment.ExpandEnvironmentVariables(
            fileNameOrPath.Trim());

        if (Path.IsPathRooted(expanded))
        {
            return Path.GetFullPath(expanded);
        }

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

    public static string ImportCameraConfiguration(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException(
                "A camera configuration source path is required.",
                nameof(sourcePath));
        }

        string fullSourcePath = Path.GetFullPath(
            Environment.ExpandEnvironmentVariables(sourcePath));

        if (!File.Exists(fullSourcePath))
        {
            throw new FileNotFoundException(
                $"Camera configuration file not found: {fullSourcePath}",
                fullSourcePath);
        }

        if (!string.Equals(
            Path.GetExtension(fullSourcePath),
            ".ccf",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Camera configuration files must use the .ccf extension.");
        }

        EnsureCreated();
        Directory.CreateDirectory(CameraConfigurationDirectory);

        string destinationPath = Path.Combine(
            CameraConfigurationDirectory,
            Path.GetFileName(fullSourcePath));

        string temporaryPath = destinationPath + ".tmp";

        try
        {
            File.Copy(fullSourcePath, temporaryPath, overwrite: true);
            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return destinationPath;
    }

    private static string ResolveRootDirectory()
    {
        string? configuredDirectory =
            Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(configuredDirectory));
        }

        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.GetTempPath();
        }

        return Path.Combine(localAppData, "OptimizationXY", "Charger");
    }
}
