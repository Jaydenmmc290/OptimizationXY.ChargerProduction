// =============================================================================
// CameraAcquisitionSettings.cs - camera and image-joining settings
// -----------------------------------------------------------------------------
// Saved under "Acquisition" in appsettings.json. Three classes live here:
//   CameraAcquisitionSettings        - both cameras + capture limits
//   DualCameraConcatenationSettings  - how a head image and a tail image are
//                                      cropped, flipped and joined into one
//                                      full-length picture of the log
//   CameraDeviceSettings             - one physical Genie Nano camera
//
// IMPORTANT: these values are part of the production camera setup. The
// calibration was built with them, so changing crop/flip/overlap/offset or
// the camera identity after calibration makes the calibration invalid.
// =============================================================================

using OptimizationXY.Charger.Infrastructure;

namespace OptimizationXY.Charger.Models.Acquisition;

/// <summary>
/// Saved production acquisition configuration for the head and tail cameras.
/// Property initializers are first-run defaults only. Values loaded from the
/// production settings file replace these defaults after commissioning.
/// </summary>
public sealed class CameraAcquisitionSettings
{

    /// <summary>
    /// The HEAD camera (the end of the block nearest the lathe operator).
    /// ServerName and DeviceIndex identify its Sapera location; if
    /// <see cref="CameraDeviceSettings.ExpectedSerialNumber"/> is set the camera is
    /// found by serial number instead. The values written here are first-run
    /// defaults only - the saved settings file replaces them.
    /// </summary>
    public CameraDeviceSettings HeadCamera { get; set; } = new()
    {
        Position = CameraPosition.Head,
        ServerName = string.Empty,
        DeviceIndex = 0,
        InitializationMode = CameraInitializationMode.Programmatic,
        BufferCount = 8,
        AcquisitionFrameRate = 30.0,
        ExposureTimeMicroseconds = 15017.0,
        Gain = 1.0,
        PixelFormat = "Mono8",
        Mode = AcquisitionMode.Encoder
    };

    /// <summary>
    /// The TAIL camera (the end of the block farthest from the lathe operator).
    /// Same rules as <see cref="HeadCamera"/>.
    /// </summary>
    public CameraDeviceSettings TailCamera { get; set; } = new()
    {
        Position = CameraPosition.Tail,
        ServerName = string.Empty,
        DeviceIndex = 0,
        InitializationMode = CameraInitializationMode.Programmatic,
        BufferCount = 8,
        AcquisitionFrameRate = 30.0,
        ExposureTimeMicroseconds = 15017.0,
        Gain = 1.0,
        PixelFormat = "Mono8",
        Mode = AcquisitionMode.Encoder 
    };

    /// <summary>
    /// Nominal ("reference") number of head/tail frame pairs per log.
    /// It is NOT a stop condition for a production scan: capture keeps every pair
    /// between the PLC ScanStart and ScanComplete signals (typically 85-100). This
    /// number is used for the provisional angle step (360 / this value, see
    /// <see cref="GetAngularStepDegrees"/>), for calibration capture limits, and
    /// in the production readiness and calibration-compatibility checks. The real
    /// per-frame angles are assigned afterwards by RevolutionAngleAssigner.
    /// </summary>
    public int TargetFramePairsPerLog { get; set; } = 36;

    /// <summary>How the head and tail images are cropped, flipped and joined. See <see cref="DualCameraConcatenationSettings"/>.</summary>
    public DualCameraConcatenationSettings Concatenation { get; set; } = new();
    /// <summary>
    /// How many frames may wait in each camera's hand-off queue before frames
    /// are counted as dropped. Must be at least 1.
    /// </summary>
    public int QueueCapacityPerCamera { get; set; } = 16;
    /// <summary>
    /// Bench-test override for the angle between frames, in degrees. Leave empty
    /// (null) in production so the step comes from <see cref="TargetFramePairsPerLog"/>.
    /// </summary>
    public double? TestAngularStepDegrees { get; set; }

    /// <summary>
    /// Maximum number of frames per camera (head or tail) before capture is forcibly stopped.
    /// This is a safety limit to prevent stale buffered frames or missed ScanComplete signals
    /// from causing unbounded frame accumulation. Typical healthy runs are 90–100 frame pairs;
    /// this limit is set to roughly 2x that to catch overcapture while remaining flexible for
    /// slower or longer scans. Must be at least <see cref="TargetFramePairsPerLog"/>.
    /// Default: 150 frames per camera.
    /// </summary>
    public int MaxFramesPerCamera { get; set; } = 150;

    /// <summary>
    /// Maximum duration (in seconds) for a capture session before it is forcibly stopped.
    /// This is a safety fallback if ScanComplete signal is missed or severely delayed.
    /// Default: 20 seconds, chosen to fire before the PLC's own scan timeout
    /// (30 seconds in the original design notes - confirm against the PLC program).
    /// </summary>
    public double CaptureTimeoutSeconds { get; set; } = 20.0;

    /// <summary>
    /// Validates the camera acquisition settings. Throws an exception if any required
    /// settings are missing or invalid.
    /// </summary>
    public void Validate()
    {
        if (HeadCamera is null)
            throw new InvalidOperationException("Head camera settings are required.");
        if (TailCamera is null)
            throw new InvalidOperationException("Tail camera settings are required.");

        HeadCamera.Validate();
        TailCamera.Validate();

        if (HeadCamera.Position != CameraPosition.Head)
            throw new InvalidOperationException("HeadCamera must use CameraPosition.Head.");
        if (TailCamera.Position != CameraPosition.Tail)
            throw new InvalidOperationException("TailCamera must use CameraPosition.Tail.");

        if (string.Equals(
            HeadCamera.ServerName,
            TailCamera.ServerName,
            StringComparison.OrdinalIgnoreCase) &&
            HeadCamera.DeviceIndex == TailCamera.DeviceIndex)
        {
            throw new InvalidOperationException(
                "The head and tail cameras cannot use the same Sapera server and device index.");
        }

        if (!string.IsNullOrWhiteSpace(HeadCamera.ExpectedSerialNumber) &&
            CameraSerialNumber.Matches(HeadCamera.ExpectedSerialNumber, TailCamera.ExpectedSerialNumber))
        {
            throw new InvalidOperationException(
                "The head and tail cameras cannot have the same ExpectedSerialNumber.");
        }

        if (TargetFramePairsPerLog <= 0)
            throw new ArgumentOutOfRangeException(nameof(TargetFramePairsPerLog));
        if (QueueCapacityPerCamera < 1)
            throw new ArgumentOutOfRangeException(nameof(QueueCapacityPerCamera));
        if (MaxFramesPerCamera < TargetFramePairsPerLog)
            throw new ArgumentOutOfRangeException(
                nameof(MaxFramesPerCamera),
                "MaxFramesPerCamera must be at least as large as TargetFramePairsPerLog.");
        if (!double.IsFinite(CaptureTimeoutSeconds) || CaptureTimeoutSeconds <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(CaptureTimeoutSeconds),
                "CaptureTimeoutSeconds must be a positive finite value.");

        if (Concatenation is null)
            throw new InvalidOperationException("Dual-camera concatenation settings are required.");

        Concatenation.Validate();

        if (TestAngularStepDegrees.HasValue &&
            (TestAngularStepDegrees.Value <= 0 || TestAngularStepDegrees.Value > 360))
        {
            throw new ArgumentOutOfRangeException(nameof(TestAngularStepDegrees));
        }
    }


    /// <summary>
    /// Provisional degrees of log rotation between consecutive frames:
    /// <see cref="TestAngularStepDegrees"/> if set, otherwise 360 / <see cref="TargetFramePairsPerLog"/>
    /// (10 degrees for the default 36). Used before the measured revolution is known.
    /// </summary>
    public double GetAngularStepDegrees() =>
        TestAngularStepDegrees ?? 360.0 / TargetFramePairsPerLog;
}

/// <summary>
/// Settings for how the head and tail camera images are joined into one
/// full-length image of the log (see DualCameraFrameConcatenator).
/// </summary>
/// <remarks>
/// Processing order for each pair: crop each image left/right -> optionally
/// mirror it -> shift the tail image vertically by <see cref="TailVerticalOffsetPixels"/>
/// -> place head and tail side by side (head on the left when <see cref="HeadOnLeft"/>)
/// with <see cref="OverlapPixels"/> of overlap at the seam. All values are in
/// camera pixels. These settings are part of the calibrated setup.
/// </remarks>
public sealed class DualCameraConcatenationSettings
{
    /// <summary>True = head image on the left of the joined picture, tail on the right.</summary>
    public bool HeadOnLeft { get; set; } = true;
    /// <summary>Mirror the head image left-to-right before joining.</summary>
    public bool ReverseHeadHorizontal { get; set; }
    /// <summary>Mirror the tail image left-to-right before joining.</summary>
    public bool ReverseTailHorizontal { get; set; }
    /// <summary>Pixels cut off the left edge of the head image (before any mirroring).</summary>
    public int HeadCropLeftPixels { get; set; }
    /// <summary>Pixels cut off the right edge of the head image (before any mirroring).</summary>
    public int HeadCropRightPixels { get; set; }
    /// <summary>Pixels cut off the left edge of the tail image (before any mirroring).</summary>
    public int TailCropLeftPixels { get; set; }
    /// <summary>Pixels cut off the right edge of the tail image (before any mirroring).</summary>
    public int TailCropRightPixels { get; set; }
    /// <summary>
    /// Moves the tail image up or down (pixels) so the laser line lines up with
    /// the head image's laser line at the seam (vertical registration).
    /// Positive = move down, negative = move up; uncovered rows are filled black.
    /// </summary>
    public int TailVerticalOffsetPixels { get; set; }
    /// <summary>How many pixels the two images overlap at the seam (0 = butt-joined).</summary>
    public int OverlapPixels { get; set; }
    /// <summary>
    /// When true, a head/tail pair whose images end up different heights is
    /// rejected instead of padded. Kept true for production measurement.
    /// </summary>
    public bool RequireEqualHeights { get; set; } = true;

    /// <summary>
    /// Validates the dual-camera concatenation settings. Throws an exception if any required
    /// settings are missing or invalid (negative crop or overlap).
    /// </summary>
    public void Validate()
    {
        if (HeadCropLeftPixels < 0 || HeadCropRightPixels < 0 ||
            TailCropLeftPixels < 0 || TailCropRightPixels < 0)
        {
            throw new InvalidOperationException("Dual-camera crop values cannot be negative.");
        }

        if (OverlapPixels < 0)
            throw new InvalidOperationException("Dual-camera overlap cannot be negative.");
    }
}

/// <summary>
/// Settings for one physical Teledyne DALSA Genie Nano camera. Programmatic
/// initialization (open the camera directly by its Sapera location) is the
/// production default; the CCF name is only used when the legacy
/// configuration-file mode is selected.
/// <para>
/// IMPORTANT - what is actually written to the camera: in programmatic mode
/// GenieNanoCameraService writes ONLY the trigger features (TriggerMode, and
/// in Encoder mode TriggerSelector=FrameStart, TriggerSource=Line1,
/// TriggerActivation=RisingEdge). Exposure, gain, frame rate and pixel format
/// are NOT written; the camera keeps its own saved values. Those settings
/// here are validated and recorded only. The camera's real width, height,
/// offsets and pixel format are read back and stored in the capture snapshot.
/// </para>
/// </summary>
public sealed class CameraDeviceSettings
{
    /// <summary>Which end of the block this camera looks at (Head or Tail).</summary>
    public CameraPosition Position { get; set; }
    /// <summary>Sapera server name (for example a "GigEVision_Device_N" name). Together with <see cref="DeviceIndex"/> this is the camera's Sapera location.</summary>
    public string ServerName { get; set; } = string.Empty;
    /// <summary>Sapera resource (device) index on <see cref="ServerName"/>; usually 0.</summary>
    public int DeviceIndex { get; set; }

    /// <summary>
    /// Serial number printed on / reported by this physical camera (for example
    /// "02-2020C-07697"). When set, the camera is found by this serial number at
    /// every start-up instead of by its Sapera name, because Sapera can hand out
    /// the GigEVision_Device_N names in a different order after a reboot, power
    /// cycle or network change. ServerName/DeviceIndex are then kept only as the
    /// calibrated identity (they are part of the calibration fingerprint), so the
    /// existing calibration stays valid. Leave empty to keep the old behaviour.
    /// </summary>
    public string ExpectedSerialNumber { get; set; } = string.Empty;
    /// <summary>
    /// How the camera is set up when it is opened: Programmatic (production -
    /// open by location and write only the trigger features) or ConfigurationFile
    /// (legacy - load a Sapera .ccf file named by <see cref="ConfigurationFileName"/>).
    /// </summary>
    public CameraInitializationMode InitializationMode { get; set; } =
        CameraInitializationMode.Programmatic;
    /// <summary>Legacy .ccf file name; only used when <see cref="InitializationMode"/> is ConfigurationFile.</summary>
    public string ConfigurationFileName { get; set; } = string.Empty;
    /// <summary>Number of Sapera image buffers (ring buffer) for this camera. Minimum 4.</summary>
    public int BufferCount { get; set; } = 8;
    /// <summary>Nominal frames per second for free-running mode. 0 &lt; value &lt;= 1000. Validated/recorded only - not written to the camera.</summary>
    public double AcquisitionFrameRate { get; set; } = 30.0;
    /// <summary>Free-running or triggered (Encoder) acquisition. Production uses Encoder.</summary>
    public AcquisitionMode Mode { get; set; } = AcquisitionMode.Encoder;

    /// <summary>Expected GenICam pixel format; "Mono8" = 8-bit greyscale. Not written to the camera (frames that are not 8-bit are rejected).</summary>
    public string PixelFormat { get; set; } = "Mono8";
    /// <summary>Exposure (shutter) time in microseconds (15017 us is about 15 ms). Validated/recorded only - not written to the camera.</summary>
    public double ExposureTimeMicroseconds { get; set; } = 15017.0;
    /// <summary>Camera gain (brightness multiplier); 1.0 = no extra gain. Validated/recorded only - not written to the camera.</summary>
    public double Gain { get; set; } = 1.0;

    /// <summary>Intended trigger input for Encoder mode. NOTE: the camera service always writes "Line1"; this value is not used.</summary>
    public string TriggerSource { get; set; } = "Line1";
    /// <summary>Intended trigger edge. NOTE: the camera service always writes "RisingEdge"; this value is not used.</summary>
    public string TriggerActivation { get; set; } = "RisingEdge";


    /// <summary>
    /// When true, a missing or rejected production-critical feature causes the
    /// camera connection to fail rather than silently using an unknown value.
    /// </summary>
    public bool RequireConfiguredFeatures { get; set; } = true;


    /// <summary>
    /// Validates this camera's settings (buffer count, frame rate, pixel format,
    /// exposure, gain, and - in legacy CCF mode - that the .ccf file exists).
    /// Throws an exception describing the first problem found.
    /// </summary>
    public void Validate()
    {


        if (BufferCount < 4)
            throw new ArgumentOutOfRangeException(nameof(BufferCount), "BufferCount must be at least 4.");
        if (!double.IsFinite(AcquisitionFrameRate) || AcquisitionFrameRate <= 0 || AcquisitionFrameRate > 1000)
            throw new ArgumentOutOfRangeException(nameof(AcquisitionFrameRate));
        if (string.IsNullOrWhiteSpace(PixelFormat))
            throw new ArgumentException($"{Position} camera PixelFormat is required.");
        if (!double.IsFinite(ExposureTimeMicroseconds) || ExposureTimeMicroseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(ExposureTimeMicroseconds));
        if (!double.IsFinite(Gain) || Gain < 0)
            throw new ArgumentOutOfRangeException(nameof(Gain));



        if (InitializationMode == CameraInitializationMode.ConfigurationFile)
        {
            if (string.IsNullOrWhiteSpace(ConfigurationFileName))
                throw new ArgumentException($"{Position} camera ConfigurationFileName is required in legacy CCF mode.");

            string path = GetConfigurationFilePath();
            if (!File.Exists(path))
                throw new FileNotFoundException($"{Position} camera configuration file was not found: {path}", path);
        }
    }

    /// <summary>
    /// Returns the full path to the legacy .ccf file, if any, using
    /// <see cref="ApplicationPaths.ResolveCameraConfigurationPath"/>. If no file name is configured,
    /// an empty string is returned.
    /// </summary>
    public string GetConfigurationFilePath() =>
        ApplicationPaths.ResolveCameraConfigurationPath(ConfigurationFileName);

    /// <summary>
    /// Returns a human-readable description of how the camera is initialized, either programmatically or via a legacy CCF file.
    /// </summary>
    public string GetInitializationDescription() =>
        InitializationMode == CameraInitializationMode.Programmatic
            ? $"Direct Sapera device {ServerName} [{DeviceIndex}]"
            : $"Legacy CCF {GetConfigurationFilePath()}";
}
