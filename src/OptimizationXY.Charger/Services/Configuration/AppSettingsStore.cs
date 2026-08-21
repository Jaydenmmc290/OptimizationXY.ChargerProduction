using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OptimizationXY.Charger.Infrastructure;
using OptimizationXY.Charger.Models.Acquisition;
using OptimizationXY.Charger.Models.Settings;

namespace OptimizationXY.Charger.Services.Configuration;

/// <summary>
/// Provides methods to load and save application settings from a JSON file.
/// </summary>
public static class AppSettingsStore
{
    private const int CurrentSettingsVersion = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ConfigDirectory => ApplicationPaths.ConfigDirectory;
    public static string SettingsPath => Path.Combine(ConfigDirectory, "appsettings.json");

    private static string LegacySettingsPath => Path.Combine(
        AppContext.BaseDirectory,
        "Config",
        "appsettings.json");

    /// <summary>
    /// Loads the application settings from the JSON file or creates default settings if the file does not exist.
    /// </summary>
    /// <returns>The loaded or default application settings.</returns>
    public static AppSettings LoadOrCreateDefault()
    {
        ApplicationPaths.EnsureCreated();
        MigrateLegacySettingsIfNeeded();

        if (!File.Exists(SettingsPath))
        {
            AppSettings defaults = new();
            Save(defaults);
            return defaults;
        }

        try
        {
            string json = File.ReadAllText(SettingsPath, Encoding.UTF8);
            AppSettings? settings =
                JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? throw new InvalidOperationException(
                    "The settings file was empty or could not be deserialized.");
            bool migrated = ApplySafeMigrations(settings);
            AppSettingsValidator.Validate(settings);

            if (migrated)
            {
                Save(settings);
            }

            return settings;
        }
        catch (JsonException ex)
        {
            string backupPath = BackupInvalidSettings();
            throw new InvalidOperationException(
                $"Settings JSON is invalid: {SettingsPath}. " +
                $"A copy was saved to: {backupPath}",
                ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                $"Settings could not be read: {SettingsPath}",
                ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                $"Access to the settings file was denied: {SettingsPath}",
                ex);
        }
    }

    /// <summary>
    /// Saves the application settings to the JSON file, ensuring that the directory exists and that the settings are valid.
    /// </summary>
    /// <param name="settings">The application settings to save.</param>
    public static void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ApplicationPaths.EnsureCreated();
        AppSettingsValidator.Validate(settings);

        string json = JsonSerializer.Serialize(settings, JsonOptions);
        string temporaryPath = SettingsPath + ".tmp";

        try
        {
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>
    /// Migrates older development settings into the machine-safe production
    /// layout. The migration never enables production mode or grants any
    /// commissioning approval. Database saving is disabled only when an older
    /// file enabled it without supplying a usable connection string, allowing
    /// the first-run setup wizard to repair the configuration instead of
    /// preventing the application from opening.
    /// </summary>
    private static bool ApplySafeMigrations(AppSettings settings)
    {
        bool changed = false;
        int originalSettingsVersion = settings.SettingsVersion;

        if (settings.Acquisition is null)
        {
            settings.Acquisition = new CameraAcquisitionSettings();
            changed = true;
        }
        else
        {
            settings.Acquisition.HeadCamera ??= new CameraDeviceSettings
            {
                Position = CameraPosition.Head,
                
            };
            settings.Acquisition.TailCamera ??= new CameraDeviceSettings
            {
                Position = CameraPosition.Tail,
                
            };
        }

        if (settings.Setup is null)
        {
            settings.Setup = new ProductionSetupSettings();
            changed = true;
        }

        if (settings.Database is null)
        {
            settings.Database = new DatabaseSettings();
            changed = true;
        }

        if (settings.OperatorView is null)
        {
            settings.OperatorView = new OperatorViewSettings();
            changed = true;
        }

        if (settings.Production is null)
        {
            settings.Production = new ProductionSettings();
            changed = true;
        }

        if (originalSettingsVersion < 10)
        {
            // Schema 10 replaces production CCF loading with direct camera
            // identity/feature configuration. Old approval and calibration
            // evidence must not silently carry into the new camera contract.
            settings.Acquisition.HeadCamera.InitializationMode =
                CameraInitializationMode.Programmatic;
            settings.Acquisition.TailCamera.InitializationMode =
                CameraInitializationMode.Programmatic;
            settings.Acquisition.HeadCamera.ServerName = string.Empty;
            settings.Acquisition.HeadCamera.DeviceIndex = 0;
            settings.Acquisition.TailCamera.ServerName = string.Empty;
            settings.Acquisition.TailCamera.DeviceIndex = 0;

            settings.Setup.CameraConfigurationVerified = false;
            settings.Setup.CameraPhysicalPositionsConfirmed = false;
            settings.Setup.LaserDetectionVerified = false;
            settings.Setup.LengthCalibrationConfigured = false;
            settings.Setup.PendingLengthPixelsPerMM = 0.0;
            settings.Setup.LengthCalibrationMeasuredUtc = null;
            settings.Setup.ScannerCalibrationCreated = false;
            settings.Setup.ScannerCalibrationRepeatabilityVerified = false;
            settings.Setup.ActiveCalibrationId = string.Empty;
            settings.Setup.LastSetupCompletedUtc = null;

            settings.Production.OperatingMode = ChargerOperatingMode.Commissioning;
            settings.Production.CameraSeamCalibrationVerified = false;
            settings.Production.ScannerCalibrationVerified = false;
            settings.Production.CommissioningApprovedBy = string.Empty;
            settings.Production.CommissioningApprovedUtc = null;
            changed = true;
        }

        if (settings.SettingsVersion < CurrentSettingsVersion)
        {
            settings.SettingsVersion = CurrentSettingsVersion;
            changed = true;
        }

        if (settings.Database.EnableDatabaseSaving &&
            string.IsNullOrWhiteSpace(
                DatabaseConnectionStringResolver.Resolve(settings.Database)))
        {
            settings.Database.EnableDatabaseSaving = false;
            settings.Setup.DatabaseConfigured = false;
            changed = true;
        }

        if (settings.Setup.LengthCalibrationConfigured &&
            (!double.IsFinite(settings.Setup.PendingLengthPixelsPerMM) ||
             settings.Setup.PendingLengthPixelsPerMM <= 0) &&
            !settings.Setup.ScannerCalibrationCreated)
        {
            settings.Setup.LengthCalibrationConfigured = false;
            settings.Setup.LengthCalibrationMeasuredUtc = null;
            changed = true;
        }

        // Never carry an incomplete active-calibration marker forward.
        if (settings.Setup.ScannerCalibrationCreated &&
            string.IsNullOrWhiteSpace(settings.Setup.ActiveCalibrationId))
        {
            settings.Setup.ScannerCalibrationCreated = false;
            settings.Setup.ScannerCalibrationRepeatabilityVerified = false;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Migrates legacy settings from the old configuration path to the new configuration directory if needed. If the new
    /// settings file already exists or the legacy settings file does not exist, no action is taken.
    /// </summary>
    private static void MigrateLegacySettingsIfNeeded()
    {
        if (File.Exists(SettingsPath) || !File.Exists(LegacySettingsPath))
        {
            return;
        }

        File.Copy(LegacySettingsPath, SettingsPath, overwrite: false);
    }

    /// <summary>
    /// Creates a backup of the invalid settings file with a timestamped filename in the configuration directory.
    /// The backup file is named "appsettings.invalid.{timestamp}.json". Returns the path to the backup file.
    /// </summary>
    /// <returns>The path to the backup file.</returns>
    private static string BackupInvalidSettings()
    {
        string backupPath = Path.Combine(
            ConfigDirectory,
            $"appsettings.invalid.{DateTime.UtcNow:yyyyMMddHHmmss}.json");

        File.Copy(SettingsPath, backupPath, overwrite: false);
        return backupPath;
    }
}
