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
    /// Saved configuration for one physical camera. ServerName and DeviceIndex
    /// locate the Sapera resource. AssignedSerialNumber verifies that the correct
    /// physical camera is connected at that machine position.
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
    /// Saved configuration for one physical camera. ServerName and DeviceIndex
    /// locate the Sapera resource. AssignedSerialNumber verifies that the correct
    /// physical camera is connected at that machine position.
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
    /// Target number of frame pairs to acquire for each log. The actual number of
    /// frame pairs may be slightly higher or lower depending on the angular step size
    /// and the encoder position at the start of the log.
    /// </summary>
    public int TargetFramePairsPerLog { get; set; } = 36;
    public DualCameraConcatenationSettings Concatenation { get; set; } = new();
    public int QueueCapacityPerCamera { get; set; } = 16;
    public double? TestAngularStepDegrees { get; set; }

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

        if (!string.IsNullOrWhiteSpace(HeadCamera.AssignedSerialNumber) &&
            !string.IsNullOrWhiteSpace(TailCamera.AssignedSerialNumber) &&
            string.Equals(
                HeadCamera.AssignedSerialNumber.Trim(),
                TailCamera.AssignedSerialNumber.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The head and tail cameras cannot be assigned the same physical serial number.");
        }

        if (TargetFramePairsPerLog <= 0)
            throw new ArgumentOutOfRangeException(nameof(TargetFramePairsPerLog));
        if (QueueCapacityPerCamera < 1)
            throw new ArgumentOutOfRangeException(nameof(QueueCapacityPerCamera));

        if (Concatenation is null)
            throw new InvalidOperationException("Dual-camera concatenation settings are required.");

        Concatenation.Validate();

        if (TestAngularStepDegrees.HasValue &&
            (TestAngularStepDegrees.Value <= 0 || TestAngularStepDegrees.Value > 360))
        {
            throw new ArgumentOutOfRangeException(nameof(TestAngularStepDegrees));
        }
    }

    public double GetAngularStepDegrees() =>
        TestAngularStepDegrees ?? 360.0 / TargetFramePairsPerLog;
}

/// <summary>
/// Settings for how the head and tail camera images are concatenated into a single image.
/// </summary>
public sealed class DualCameraConcatenationSettings
{
    public bool HeadOnLeft { get; set; } = true;
    public bool ReverseHeadHorizontal { get; set; }
    public bool ReverseTailHorizontal { get; set; }
    public int HeadCropLeftPixels { get; set; }
    public int HeadCropRightPixels { get; set; }
    public int TailCropLeftPixels { get; set; }
    public int TailCropRightPixels { get; set; }
    public int TailVerticalOffsetPixels { get; set; }
    public int OverlapPixels { get; set; }
    public bool RequireEqualHeights { get; set; } = true;

    /// <summary>
    /// Validates the dual-camera concatenation settings. Throws an exception if any required
    /// settings are missing or invalid
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
/// Settings for one physical Genie Nano. Programmatic initialization is the
/// production default; the CCF name is only used when legacy mode is selected.
/// </summary>
public sealed class CameraDeviceSettings
{
    public CameraPosition Position { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public int DeviceIndex { get; set; }
    /// <summary>
    /// Serial number learned directly from the physical camera assigned
    /// to this machine position. This is the persistent camera identity.
    /// </summary>
    public string AssignedSerialNumber { get; set; } = string.Empty;
    public CameraInitializationMode InitializationMode { get; set; } =
        CameraInitializationMode.Programmatic;
    public string ConfigurationFileName { get; set; } = string.Empty;
    public int BufferCount { get; set; } = 8;
    public double AcquisitionFrameRate { get; set; } = 30.0;
    public AcquisitionMode Mode { get; set; } = AcquisitionMode.Encoder;

    public string PixelFormat { get; set; } = "Mono8";
    public double ExposureTimeMicroseconds { get; set; } = 15017.0;
    public double Gain { get; set; } = 1.0;

    public string TriggerSource { get; set; } = "Line1";
    public string TriggerActivation { get; set; } = "RisingEdge";


    /// <summary>
    /// When true, a missing or rejected production-critical feature causes the
    /// camera connection to fail rather than silently using an unknown value.
    /// </summary>
    public bool RequireConfiguredFeatures { get; set; } = true;


    /// <summary>
    /// Validates the camera device settings. Throws an exception if any required
    /// settings are missing or invalid.
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
    /// Returns the full path to the camera configuration file, if any. The path is resolved
    /// relative to the application configuration directory. If no configuration file is specified,
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
