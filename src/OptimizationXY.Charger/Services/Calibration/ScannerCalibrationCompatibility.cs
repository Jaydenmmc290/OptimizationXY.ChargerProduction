using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OptimizationXY.Charger.Models.Acquisition;

namespace OptimizationXY.Charger.Services.Calibration;

/// <summary>
/// Produces a stable fingerprint for every setting that changes the pixel
/// coordinate system used by the scanner lookup table. A calibration created
/// with one crop, orientation, overlap, or seam configuration must not be used
/// after those settings change.
/// <para>
/// The hash covers: TargetFramePairsPerLog, each camera's position, Sapera
/// server name/device index, initialization mode, pixel format, acquisition
/// mode, trigger source and activation, and every image-joining setting
/// (head on left, mirroring, crops, tail vertical offset, overlap, equal-height
/// rule). It is stored in the calibration (AcquisitionGeometrySha256) and
/// re-computed before every scan (LogProcessingCoordinator) - a mismatch stops
/// processing. Camera serial numbers are deliberately NOT included, so binding
/// a camera by serial number does not invalidate an existing calibration.
/// </para>
/// </summary>
public static class ScannerCalibrationCompatibility
{
    /// <summary>
    /// JSON serialization options for computing the acquisition geometry fingerprint.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// Computes a SHA256 fingerprint of the acquisition geometry settings that affect
    /// the pixel coordinate system used by the scanner lookup table.
    /// </summary>
    /// <param name="settings">The camera acquisition settings to compute the fingerprint for.</param>
    /// <returns>The SHA256 fingerprint as a hexadecimal string.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the settings or concatenation settings are null.</exception>
    public static string ComputeAcquisitionGeometrySha256(
        CameraAcquisitionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settings.Concatenation);

        DualCameraConcatenationSettings concatenation =
            settings.Concatenation;

        // Changing the order or names of these fields changes every hash and would
        // invalidate all existing calibrations - bump Schema only on purpose.
        var fingerprintRecord = new
        {
            Schema = 4,
            settings.TargetFramePairsPerLog,
            HeadPosition = settings.HeadCamera.Position.ToString(),
            TailPosition = settings.TailCamera.Position.ToString(),

            HeadServerName = settings.HeadCamera.ServerName,
            TailServerName = settings.TailCamera.ServerName,
            HeadDeviceIndex = settings.HeadCamera.DeviceIndex,
            TailDeviceIndex = settings.TailCamera.DeviceIndex,
            HeadInitialization = settings.HeadCamera.InitializationMode.ToString(),
            TailInitialization = settings.TailCamera.InitializationMode.ToString(),
            HeadPixelFormat = settings.HeadCamera.PixelFormat,
            TailPixelFormat = settings.TailCamera.PixelFormat,
            HeadMode = settings.HeadCamera.Mode.ToString(),
            TailMode = settings.TailCamera.Mode.ToString(),
            HeadTriggerSource = settings.HeadCamera.TriggerSource,
            TailTriggerSource = settings.TailCamera.TriggerSource,
            HeadTriggerActivation = settings.HeadCamera.TriggerActivation,
            TailTriggerActivation = settings.TailCamera.TriggerActivation,
            concatenation.HeadOnLeft,
            concatenation.ReverseHeadHorizontal,
            concatenation.ReverseTailHorizontal,
            concatenation.HeadCropLeftPixels,
            concatenation.HeadCropRightPixels,
            concatenation.TailCropLeftPixels,
            concatenation.TailCropRightPixels,
            concatenation.TailVerticalOffsetPixels,
            concatenation.OverlapPixels,
            concatenation.RequireEqualHeights
        };

        byte[] bytes = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(fingerprintRecord, JsonOptions));

        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
