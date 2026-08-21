using System.Text;
using System.Text.Json;
using OptimizationXY.Charger.Infrastructure;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Calibration;


/// <summary>
/// Writes a JSON file containing the current production setup status, including configuration and calibration details.
/// </summary>
namespace OptimizationXY.Charger.Services.Configuration;

public static class ProductionSetupStatusWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Writes the production setup status to a JSON file in the setup directory.
    /// </summary>
    /// <param name="settings">The application settings containing the production setup status to write.</param>
    /// <returns>The path to the written JSON file.</returns>
    public static string Write(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ApplicationPaths.EnsureCreated();

        string path = Path.Combine(
            ApplicationPaths.SetupDirectory,
            "production-setup-status.json");

        var record = new
        {
            SchemaVersion = 4,
            UpdatedUtc = DateTime.UtcNow,
            Environment.MachineName,
            WindowsIdentity = Environment.UserDomainName + "\\" + Environment.UserName,
            RuntimeDataRoot = ApplicationPaths.RootDirectory,
            AppSettingsStore.SettingsPath,
            ApplicationPaths.CalibrationDirectory,
            ActiveCalibrationPackagePath = ApplicationPaths.CalibrationLookupTablePath,
            CurrentAcquisitionGeometrySha256 =
                ScannerCalibrationCompatibility.ComputeAcquisitionGeometrySha256(
                    settings.Acquisition),
            ApplicationPaths.SavedScansDirectory,
            Database = new
            {
                settings.Database.EnableDatabaseSaving,
                settings.Setup.DatabaseConfigured,
                UsesWindowsIntegratedSecurity = true
            },
            Cameras = new
            {
                HeadServer = settings.Acquisition.HeadCamera.ServerName, 
                TailServer = settings.Acquisition.TailCamera.ServerName,
                HeadDeviceIndex = settings.Acquisition.HeadCamera.DeviceIndex,
                TailDeviceIndex = settings.Acquisition.TailCamera.DeviceIndex,
                HeadCameraInitialization = settings.Acquisition.HeadCamera.GetInitializationDescription(),
                TailCameraInitialization = settings.Acquisition.TailCamera.GetInitializationDescription(),

                settings.Setup.CameraConfigurationVerified,
                settings.Setup.CameraPhysicalPositionsConfirmed,
                settings.Production.CameraSeamCalibrationVerified,
                settings.Setup.LaserDetectionVerified,

                Concatenation = new
                {
                    settings.Acquisition.Concatenation.HeadOnLeft,

                    settings.Acquisition.Concatenation
            .ReverseHeadHorizontal,

                    settings.Acquisition.Concatenation
            .ReverseTailHorizontal,

                    settings.Acquisition.Concatenation
            .HeadCropLeftPixels,

                    settings.Acquisition.Concatenation
            .HeadCropRightPixels,

                    settings.Acquisition.Concatenation
            .TailCropLeftPixels,

                    settings.Acquisition.Concatenation
            .TailCropRightPixels,

                    settings.Acquisition.Concatenation
            .TailVerticalOffsetPixels,

                    settings.Acquisition.Concatenation
            .OverlapPixels
                }

            },

            Calibration = new
            {
                settings.Setup.LengthCalibrationConfigured,
                settings.Setup.PendingLengthPixelsPerMM,
                settings.Setup.LengthCalibrationMeasuredUtc,
                settings.Setup.ScannerCalibrationCreated,
                settings.Setup.ScannerCalibrationRepeatabilityVerified,
                settings.Setup.ActiveCalibrationId
            },
            settings.Setup.MinimumSetupComplete,
            settings.Setup.LastSetupCompletedUtc,
            OperatingMode = settings.Production.OperatingMode.ToString(),
            settings.Production.CommissioningComplete
        };

        string temporaryPath = path + ".tmp";

        try
        {
            string json = JsonSerializer.Serialize(record, JsonOptions);
            File.WriteAllText(
                temporaryPath,
                json,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return path;
    }
}
