using OptimizationXY.Charger.Infrastructure;

namespace OptimizationXY.Charger.Models.Acquisition;

/// <summary>
/// Production acquisition settings for the head and tail Genie Nano cameras.
/// </summary>
public sealed class CameraAcquisitionSettings
{
    public CameraDeviceSettings HeadCamera { get; set; } = new()
    {
        Position = CameraPosition.Head,
        ServerName = "Nano-M1920_1",
        DeviceIndex = 0,
        InitializationMode = CameraInitializationMode.Programmatic,
        BufferCount = 8,
        AcquisitionFrameRate = 30.0,
        ExposureTimeMicroseconds = 15017.0,
        Gain = 1.0,
        PixelFormat = "Mono8",
        Mode = AcquisitionMode.Encoder
    };

    public CameraDeviceSettings TailCamera { get; set; } = new()
    {
        Position = CameraPosition.Tail,
        ServerName = "Nano-M1920_2",
        DeviceIndex = 0,
        InitializationMode = CameraInitializationMode.Programmatic,
        BufferCount = 8,
        AcquisitionFrameRate = 30.0,
        ExposureTimeMicroseconds = 15017.0,
        Gain = 1.0,
        PixelFormat = "Mono8",
        Mode = AcquisitionMode.Encoder
    };

    public int TargetFramePairsPerLog { get; set; } = 36;
    public DualCameraConcatenationSettings Concatenation { get; set; } = new();
    public int QueueCapacityPerCamera { get; set; } = 16;
    public double? TestAngularStepDegrees { get; set; }

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
            !string.IsNullOrWhiteSpace(TailCamera.ExpectedSerialNumber) &&
            string.Equals(
                HeadCamera.ExpectedSerialNumber.Trim(),
                TailCamera.ExpectedSerialNumber.Trim(),
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
    public string ExpectedSerialNumber { get; set; } = string.Empty;
    public CameraInitializationMode InitializationMode { get; set; } =
        CameraInitializationMode.Programmatic;
    public string ConfigurationFileName { get; set; } = string.Empty;
    public int BufferCount { get; set; } = 8;
    public double AcquisitionFrameRate { get; set; } = 30.0;
    public AcquisitionMode Mode { get; set; } = AcquisitionMode.Encoder;

    public string PixelFormat { get; set; } = "Mono8";
    public double ExposureTimeMicroseconds { get; set; } = 15017.0;
    public double Gain { get; set; } = 1.0;
    public bool DisableAutomaticExposure { get; set; } = true;
    public bool DisableAutomaticGain { get; set; } = true;
    public string TriggerSource { get; set; } = "Line1";
    public string TriggerActivation { get; set; } = "RisingEdge";

    /// <summary>
    /// When true, a missing or rejected production-critical feature causes the
    /// camera connection to fail rather than silently using an unknown value.
    /// </summary>
    public bool RequireConfiguredFeatures { get; set; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServerName))
            throw new ArgumentException($"{Position} camera ServerName is required.");
        if (DeviceIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(DeviceIndex));
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

        if (Mode == AcquisitionMode.Encoder && string.IsNullOrWhiteSpace(TriggerSource))
            throw new ArgumentException($"{Position} camera TriggerSource is required in Encoder mode.");
        if (Mode == AcquisitionMode.Encoder && string.IsNullOrWhiteSpace(TriggerActivation))
            throw new ArgumentException($"{Position} camera TriggerActivation is required in Encoder mode.");

        if (InitializationMode == CameraInitializationMode.ConfigurationFile)
        {
            if (string.IsNullOrWhiteSpace(ConfigurationFileName))
                throw new ArgumentException($"{Position} camera ConfigurationFileName is required in legacy CCF mode.");

            string path = GetConfigurationFilePath();
            if (!File.Exists(path))
                throw new FileNotFoundException($"{Position} camera configuration file was not found: {path}", path);
        }
    }

    public string GetConfigurationFilePath() =>
        ApplicationPaths.ResolveCameraConfigurationPath(ConfigurationFileName);

    public string GetInitializationDescription() =>
        InitializationMode == CameraInitializationMode.Programmatic
            ? $"Direct Sapera device {ServerName} [{DeviceIndex}]"
            : $"Legacy CCF {GetConfigurationFilePath()}";
}
