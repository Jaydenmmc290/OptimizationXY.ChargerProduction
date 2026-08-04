using System.Text;
using System.Text.Json;
using OptimizationXY.Charger.Infrastructure;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Calibration;

namespace OptimizationXY.Charger.Services.Configuration;

public static class ProductionSetupStatusWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string Write(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ApplicationPaths.EnsureCreated();

        string path = Path.Combine(
            ApplicationPaths.SetupDirectory,
            "production-setup-status.json");

        var record = new
        {
            SchemaVersion = 3,
            UpdatedUtc = DateTime.UtcNow,
            MachineName = Environment.MachineName,
            WindowsIdentity = Environment.UserDomainName + "\\" + Environment.UserName,
            RuntimeDataRoot = ApplicationPaths.RootDirectory,
            SettingsPath = AppSettingsStore.SettingsPath,
            CalibrationDirectory = ApplicationPaths.CalibrationDirectory,
            ActiveCalibrationPackagePath = ApplicationPaths.CalibrationLookupTablePath,
            CurrentAcquisitionGeometrySha256 =
                ScannerCalibrationCompatibility.ComputeAcquisitionGeometrySha256(
                    settings.Acquisition),
            SavedScansDirectory = ApplicationPaths.SavedScansDirectory,
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
                HeadExpectedSerial = settings.Acquisition.HeadCamera.ExpectedSerialNumber,
                TailExpectedSerial = settings.Acquisition.TailCamera.ExpectedSerialNumber,
                HeadDeviceIndex = settings.Acquisition.HeadCamera.DeviceIndex,
                TailDeviceIndex = settings.Acquisition.TailCamera.DeviceIndex,
                HeadCameraInitialization = settings.Acquisition.HeadCamera.GetInitializationDescription(),
                TailCameraInitialization = settings.Acquisition.TailCamera.GetInitializationDescription(),
                settings.Setup.CameraConfigurationVerified,
                settings.Setup.CameraPhysicalPositionsConfirmed,
                settings.Setup.LaserDetectionVerified
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
            CommissioningComplete = settings.Production.CommissioningComplete
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
