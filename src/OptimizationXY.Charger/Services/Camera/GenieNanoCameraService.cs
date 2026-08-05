#if SAPERA
using System.Text.Json;
using DALSA.SaperaLT.SapClassBasic;
using Emgu.CV;
using OptimizationXY.Charger.Models.Acquisition;

namespace OptimizationXY.Charger.Services.Camera;

/// <summary>
/// Production Genie Nano service. In programmatic mode it opens the Sapera
/// acquisition device directly by server name/resource index, verifies camera
/// identity, and writes only the TriggerSource feature before creating buffers.
/// </summary>
public sealed class GenieNanoCameraService : IDisposable
{
    private SapAcqDevice? _acqDevice;
    private SapBuffer? _buffer;
    private SapTransfer? _transfer;
    private SapView? _view;
    private Control? _cameraViewHost;
    private bool _ownsCameraViewHost;
    private byte[]? _loadedConfigurationSnapshot;
    private int _frameCounter;
    private readonly Lock _frameCounterLock = new();
    private bool _isCapturingManagedFrames;

    public bool IsConnected { get; private set; }
    public string ConnectedSerialNumber { get; private set; } = string.Empty;
    public string ConnectedModelName { get; private set; } = string.Empty;
    public Control? PanelCameraView { get; private set; }
    public ReadOnlyMemory<byte> LoadedConfigurationSnapshot =>
        _loadedConfigurationSnapshot is null
            ? ReadOnlyMemory<byte>.Empty
            : new ReadOnlyMemory<byte>(_loadedConfigurationSnapshot);

    public event EventHandler<CameraFrameEventArgs>? FrameAcquired;
    public event EventHandler<string>? DiagnosticsMessage;

    public void Connect(CameraDeviceSettings settings, Exception argumentOutOfRangeException, Exception argumentOutOfRangeException1)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Panel hiddenViewHost = new()
        {
            Width = 1,
            Height = 1,
            Visible = false
        };
        hiddenViewHost.CreateControl();

        try
        {
            Connect(settings, hiddenViewHost, argumentOutOfRangeException, argumentOutOfRangeException1);
            _ownsCameraViewHost = true;
        }
        catch
        {
            hiddenViewHost.Dispose();
            throw;
        }
    }

    public void Connect(CameraDeviceSettings settings, Control panelCameraView, Exception argumentOutOfRangeException, Exception argumentOutOfRangeException1)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(panelCameraView);
        settings.Validate();

        try
        {
            Disconnect();
            _cameraViewHost = panelCameraView;
            _ownsCameraViewHost = false;
            _cameraViewHost.SizeChanged += CameraViewHost_SizeChanged;
            _frameCounter = 0;
            PanelCameraView = panelCameraView;

            SapLocation location = new(settings.ServerName, settings.DeviceIndex);
            LogDiagnostics(
                $"Opening {settings.Position} Sapera device '{settings.ServerName}' resource {settings.DeviceIndex} using {settings.InitializationMode} initialization...");

            byte[]? legacySnapshot = null;
            if (settings.InitializationMode == CameraInitializationMode.ConfigurationFile)
            {
                string path = settings.GetConfigurationFilePath();
                legacySnapshot = File.ReadAllBytes(path);
                if (legacySnapshot.Length == 0)
                    throw new InvalidDataException($"Camera CCF is empty: {path}");
                _acqDevice = new SapAcqDevice(location, path);
            }
            else
            {
                _acqDevice = new SapAcqDevice(location);
            }

            // Sapera requires the acquisition device to be created before
            // feature access and before the transfer object is created.
            if (!_acqDevice.Create())
                throw new InvalidOperationException("Failed to create SapAcqDevice.");

            ConnectedSerialNumber = ReadCameraSerialNumber(_acqDevice, location);
            ConnectedModelName = ReadStringFeature(_acqDevice, "DeviceModelName");

            if (string.IsNullOrWhiteSpace(ConnectedSerialNumber) &&
                settings.RequireConfiguredFeatures)
            {
                throw new InvalidOperationException(
                    "The camera serial number could not be read. Production camera identity cannot be verified.");
            }

            if (!string.IsNullOrWhiteSpace(settings.ExpectedSerialNumber) &&
                !string.Equals(
                    settings.ExpectedSerialNumber.Trim(),
                    ConnectedSerialNumber,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Camera identity mismatch for {settings.Position}. Expected serial '{settings.ExpectedSerialNumber}', connected serial '{ConnectedSerialNumber}'. Clear or correct the expected serial in Production Setup only when intentionally replacing a camera, then recalibrate.");
            }

            var appliedFeatures = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Identity.DeviceSerialNumber"] = ConnectedSerialNumber,
                ["Identity.DeviceModelName"] = ConnectedModelName
            };


            if (settings.InitializationMode ==
        CameraInitializationMode.Programmatic)
            {
                ApplyAcquisitionMode(
                    _acqDevice,
                    settings,
                    appliedFeatures, argumentOutOfRangeException1);

                CaptureGeometryReadback(
                    _acqDevice,
                    appliedFeatures);
            }

            _buffer = new SapBufferWithTrash(
                settings.BufferCount,
                _acqDevice,
                SapBuffer.MemoryType.ScatterGather);
            _transfer = new SapAcqDeviceToBuf(_acqDevice, _buffer);
            _view = new SapView(_buffer)
            {
                Window = panelCameraView
            };
            _view.SetScalingMode(true);
            _view.OnSize();

            _transfer.Pairs[0].EventType = SapXferPair.XferEventType.EndOfFrame;
            _transfer.XferNotify += OnFrameReady;
            _transfer.XferNotifyContext = _view;

            if (!_buffer.Create())
                throw new InvalidOperationException("Failed to create SapBuffer.");
            if (!_transfer.Create())
                throw new InvalidOperationException("Failed to create SapTransfer.");
            if (!_view.Create())
                throw new InvalidOperationException("Failed to create SapView.");

            _loadedConfigurationSnapshot =
                settings.InitializationMode == CameraInitializationMode.Programmatic
                    ? BuildProgrammaticSnapshot(settings, appliedFeatures, GetOptions())
                    : legacySnapshot;

            IsConnected = true;
            LogDiagnostics(
                $"{settings.Position} camera connected. {settings.GetInitializationDescription()}; buffers: {settings.BufferCount}.");
        }
        catch (Exception ex)
        {
            LogDiagnostics($"Camera connection failed: {ex.Message}");
            Disconnect();
            throw;
        }
    }
    private void ApplyAcquisitionMode(
        SapAcqDevice device,
        CameraDeviceSettings settings,
        IDictionary<string, object?> applied, Exception argumentOutOfRangeException)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(applied);

        switch (settings.Mode)
        {
            case AcquisitionMode.FrameRate:
                ConfigureFrameRateMode(
                    device,
                    settings,
                    applied);
                break;

            case AcquisitionMode.Encoder:
                ConfigureEncoderMode(
                    device,
                    settings,
                    applied);
                break;

            default:
                throw argumentOutOfRangeException;
        }
    }

    private void ConfigureFrameRateMode(
        SapAcqDevice device,
        CameraDeviceSettings settings,
        IDictionary<string, object?> applied)
    {
        LogDiagnostics(
            $"{settings.Position} camera: switching to FrameRate mode.");

        if (!device.IsFeatureAvailable("TriggerMode"))
        {
            throw new InvalidOperationException(
                $"{settings.Position} camera does not expose TriggerMode.");
        }

        // FrameRate/free-running mode only requires TriggerMode=Off.
        // TriggerSource becomes unavailable while TriggerMode is Off,
        // so do not attempt to write TriggerSource here.
        if (!device.SetFeatureValue("TriggerMode", "Off"))
        {
            throw new InvalidOperationException(
                $"{settings.Position} camera rejected TriggerMode=Off.");
        }

        string actualMode =
            ReadStringFeature(device, "TriggerMode").Trim();

        if (!string.Equals(
                actualMode,
                "Off",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{settings.Position} camera could not verify TriggerMode=Off. " +
                $"Actual value: '{DisplayFeatureValue(actualMode)}'.");
        }

        applied["TriggerMode"] = actualMode;
        applied["TriggerSource"] = "NotEnabled";
        applied["AcquisitionMode"] = "FrameRate";
        applied["CameraWritePolicy"] = "TriggerModeOnly";

        LogDiagnostics(
            $"{settings.Position} camera configured for FrameRate mode: " +
            $"TriggerMode={actualMode}.");
    }

    private void ConfigureEncoderMode(
        SapAcqDevice device,
        CameraDeviceSettings settings,
        IDictionary<string, object?> applied)
    {
        LogDiagnostics(
            $"{settings.Position} camera: switching to Encoder mode.");

        if (!device.IsFeatureAvailable("TriggerMode"))
        {
            throw new InvalidOperationException(
                $"{settings.Position} camera does not expose TriggerMode.");
        }

        /*
         * On this Nano-M1920, TriggerSource is unavailable while
         * TriggerMode is Off. TriggerMode must be turned On first.
         */
        if (!device.SetFeatureValue("TriggerMode", "On"))
        {
            throw new InvalidOperationException(
                $"{settings.Position} camera rejected TriggerMode=On.");
        }

        string actualMode =
            ReadStringFeature(device, "TriggerMode").Trim();

        if (!string.Equals(
                actualMode,
                "On",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{settings.Position} camera could not verify TriggerMode=On. " +
                $"Actual value: '{DisplayFeatureValue(actualMode)}'.");
        }

        if (device.IsFeatureAvailable("TriggerSelector"))
        {
            string selector =
                ReadStringFeature(device, "TriggerSelector").Trim();

            if (!string.Equals(
                    selector,
                    "FrameStart",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!device.SetFeatureValue(
                        "TriggerSelector",
                        "FrameStart"))
                {
                    throw new InvalidOperationException(
                        $"{settings.Position} camera rejected " +
                        "TriggerSelector=FrameStart.");
                }
            }

            applied["TriggerSelector"] =
                ReadStringFeature(device, "TriggerSelector").Trim();
        }

        if (!device.IsFeatureAvailable("TriggerSource"))
        {
            throw new InvalidOperationException(
                $"{settings.Position} camera did not enable TriggerSource " +
                "after TriggerMode was turned On.");
        }

        string source =
            ReadStringFeature(device, "TriggerSource").Trim();

        if (!string.Equals(
                source,
                "Line1",
                StringComparison.OrdinalIgnoreCase))
        {
            if (!device.SetFeatureValue(
                    "TriggerSource",
                    "Line1"))
            {
                throw new InvalidOperationException(
                    $"{settings.Position} camera rejected TriggerSource=Line1.");
            }

            source =
                ReadStringFeature(device, "TriggerSource").Trim();
        }

        if (!string.Equals(
                source,
                "Line1",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{settings.Position} camera could not verify TriggerSource=Line1. " +
                $"Actual value: '{DisplayFeatureValue(source)}'.");
        }

        string activation = string.Empty;

        if (device.IsFeatureAvailable("TriggerActivation"))
        {
            activation =
                ReadStringFeature(device, "TriggerActivation").Trim();

            if (!string.Equals(
                    activation,
                    "RisingEdge",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!device.SetFeatureValue(
                        "TriggerActivation",
                        "RisingEdge"))
                {
                    throw new InvalidOperationException(
                        $"{settings.Position} camera rejected " +
                        "TriggerActivation=RisingEdge.");
                }

                activation =
                    ReadStringFeature(
                        device,
                        "TriggerActivation").Trim();
            }
        }

        applied["TriggerMode"] = actualMode;
        applied["TriggerSource"] = source;
        applied["TriggerActivation"] = activation;
        applied["AcquisitionMode"] = "Encoder";
        applied["CameraWritePolicy"] =
            "TriggerModeSourceAndActivationOnly";

        LogDiagnostics(
            $"{settings.Position} camera configured for Encoder mode: " +
            $"TriggerMode={actualMode}, " +
            $"TriggerSource={source}, " +
            $"TriggerActivation={DisplayFeatureValue(activation)}.");
    }
    private static string DisplayFeatureValue(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "<unavailable>"
            : value;
    }
    private static string ReadCameraSerialNumber(
        SapAcqDevice device,
        SapLocation location)
    {
        string serial = ReadStringFeature(device, "DeviceSerialNumber");
        if (!string.IsNullOrWhiteSpace(serial))
            return serial.Trim();

        try
        {
            return (SapManager.GetSerialNumber(location) ?? string.Empty).Trim();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string ReadStringFeature(
        SapAcqDevice device,
        string featureName)
    {
        try
        {
            if (device.IsFeatureAvailable(featureName) &&
                device.GetFeatureValue(featureName, out string value))
            {
                return value ?? string.Empty;
            }
        }
        catch (Exception)
        {
            // Optional identity/readback feature.
        }

        return string.Empty;
    }

    private static void CaptureGeometryReadback(
        SapAcqDevice device,
        IDictionary<string, object?> applied)
    {
        CaptureReadback(device, applied, "Width");
        CaptureReadback(device, applied, "Height");
        CaptureReadback(device, applied, "OffsetX");
        CaptureReadback(device, applied, "OffsetY");
        CaptureReadback(device, applied, "PixelFormat");
    }

    private static void CaptureReadback(
        SapAcqDevice device,
        IDictionary<string, object?> applied,
        string featureName)
    {
        if (!device.IsFeatureAvailable(featureName))
            return;

        try
        {
            if (device.GetFeatureValue(featureName, out string stringValue))
            {
                applied[$"Readback.{featureName}"] = stringValue;
                return;
            }
        }
        catch (Exception)
        {
            // Try numeric forms below.
        }

        try
        {
            if (device.GetFeatureValue(featureName, out long longValue))
            {
                applied[$"Readback.{featureName}"] = longValue;
                return;
            }
            if (device.GetFeatureValue(featureName, out int intValue))
            {
                applied[$"Readback.{featureName}"] = intValue;
                return;
            }
            if (device.GetFeatureValue(featureName, out double doubleValue))
                applied[$"Readback.{featureName}"] = doubleValue;
        }
        catch (Exception)
        {
            // Readback is audit enrichment; write verification remains authoritative.
        }
    }


    private static JsonSerializerOptions GetOptions()
    {
        return new JsonSerializerOptions { WriteIndented = true };
    }

    private static byte[] BuildProgrammaticSnapshot(
        CameraDeviceSettings settings,
        SortedDictionary<string, object?> appliedFeatures, JsonSerializerOptions options)
    {
        var snapshot = new
        {
            Schema = "OptimizationXY.CameraConnection.v2",
            settings.Position,
            settings.ServerName,
            settings.DeviceIndex,
            ConnectedSerialNumber =
                appliedFeatures.GetValueOrDefault("Identity.DeviceSerialNumber"),
            ConnectedModelName =
                appliedFeatures.GetValueOrDefault("Identity.DeviceModelName"),
            settings.InitializationMode,
            settings.BufferCount,
            settings.Mode,
            TriggerSource = appliedFeatures.GetValueOrDefault("TriggerSource"),
            CameraWritePolicy =
                appliedFeatures.GetValueOrDefault("CameraWritePolicy"),
            ReadbackWidth =
                appliedFeatures.GetValueOrDefault("Readback.Width"),
            ReadbackHeight =
                appliedFeatures.GetValueOrDefault("Readback.Height"),
            ReadbackOffsetX =
                appliedFeatures.GetValueOrDefault("Readback.OffsetX"),
            ReadbackOffsetY =
                appliedFeatures.GetValueOrDefault("Readback.OffsetY"),
            ReadbackPixelFormat =
                appliedFeatures.GetValueOrDefault("Readback.PixelFormat")
        };

        return JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
options);
    }

    private void OnFrameReady(object sender, SapXferNotifyEventArgs args)
    {
        if (args.Context is SapView view)
            view.Show();

        if (!_isCapturingManagedFrames)
            return;

        try
        {
            Mat? managedFrame = ExtractFrameFromSaperaBuffer();
            if (managedFrame is null || managedFrame.IsEmpty)
            {
                managedFrame?.Dispose();
                return;
            }

            long frameNumber;
            lock (_frameCounterLock)
                frameNumber = ++_frameCounter;

            EventHandler<CameraFrameEventArgs>? handler = FrameAcquired;
            if (handler is null)
            {
                managedFrame.Dispose();
                return;
            }

            handler(this, new CameraFrameEventArgs(managedFrame, frameNumber, DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            LogDiagnostics($"Error processing managed frame: {ex.Message}");
        }
    }

    private Mat? ExtractFrameFromSaperaBuffer()
    {
        SapBuffer? buffer = _buffer;
        if (buffer is null)
            return null;

        try
        {
            int width = checked((int)buffer.Width);
            int height = checked((int)buffer.Height);
            int pitch = checked((int)buffer.Pitch);
            if (width <= 0 || height <= 0 || pitch < width)
                throw new InvalidOperationException($"Invalid Sapera buffer geometry {width}x{height}, pitch {pitch}.");
            if (buffer.PixelDepth != 8)
                throw new InvalidOperationException($"Expected Mono8, received pixel depth {buffer.PixelDepth}.");

            buffer.GetAddress(out IntPtr sourceAddress);
            if (sourceAddress == IntPtr.Zero)
                throw new InvalidOperationException("Sapera returned a null buffer address.");

            using Mat saperaView = new(
                height,
                width,
                Emgu.CV.CvEnum.DepthType.Cv8U,
                1,
                sourceAddress,
                pitch);
            return saperaView.Clone();
        }
        catch (Exception ex)
        {
            LogDiagnostics($"Frame extraction error: {ex.Message}");
            return null;
        }
    }

    private void CameraViewHost_SizeChanged(object? sender, EventArgs e) => _view?.OnSize();

    public void StartLive()
    {
        if (!IsConnected || _transfer is null)
            throw new InvalidOperationException("Camera is not connected.");
        _transfer.Grab();
        LogDiagnostics("Live acquisition started.");
    }

    public void StopLive()
    {
        _isCapturingManagedFrames = false;
        if (_transfer is null)
            return;
        try
        {
            _transfer.Freeze();
            _transfer.Wait(1000);
        }
        catch (Exception ex)
        {
            LogDiagnostics($"Error stopping acquisition: {ex.Message}");
        }
    }

    public void StartManagedFrameCapture()
    {
        if (!IsConnected)
            throw new InvalidOperationException("Camera is not connected.");
        lock (_frameCounterLock)
            _frameCounter = 0;
        _isCapturingManagedFrames = true;
    }

    public void StopManagedFrameCapture() => _isCapturingManagedFrames = false;

    public void Disconnect()
    {
        bool hadResources = IsConnected || _transfer is not null || _buffer is not null ||
            _acqDevice is not null || _view is not null || _cameraViewHost is not null;
        if (!hadResources)
            return;

        _isCapturingManagedFrames = false;
        try
        {
            _transfer?.Freeze();
            _transfer?.Wait(1000);
        }
        catch (Exception ex)
        {
            LogDiagnostics($"Camera transfer did not stop cleanly: {ex.Message}");
        }

        _cameraViewHost?.SizeChanged -= CameraViewHost_SizeChanged;
        TryRelease("transfer", () => _transfer?.Destroy());
        TryRelease("view", () => _view?.Destroy());
        TryRelease("buffer", () => _buffer?.Destroy());
        TryRelease("acquisition device", () => _acqDevice?.Destroy());

        _transfer?.Dispose();
        _view?.Dispose();
        _buffer?.Dispose();
        _acqDevice?.Dispose();
        _transfer = null;
        _view = null;
        _buffer = null;
        _acqDevice = null;

        if (_ownsCameraViewHost)
            _cameraViewHost?.Dispose();
        _cameraViewHost = null;
        _ownsCameraViewHost = false;
        IsConnected = false;
        ConnectedSerialNumber = string.Empty;
        ConnectedModelName = string.Empty;
        PanelCameraView = null;
        _loadedConfigurationSnapshot = null;
    }

    private void TryRelease(string resourceName, Action release)
    {
        try { release(); }
        catch (Exception ex) { LogDiagnostics($"Failed to release Sapera {resourceName}: {ex.Message}"); }
    }

    private void LogDiagnostics(string message) => DiagnosticsMessage?.Invoke(this, message);

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
#endif
