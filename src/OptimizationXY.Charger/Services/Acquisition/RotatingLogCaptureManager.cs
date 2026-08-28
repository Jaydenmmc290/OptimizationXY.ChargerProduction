using OptimizationXY.Charger.Models.Acquisition;
using OptimizationXY.Charger.Models.Core;


namespace OptimizationXY.Charger.Services.Acquisition;

/// <summary>
/// Collects one complete rotating-log scan from both the head and tail cameras.
/// Capture continues dynamically until explicitly stopped (via ScanComplete or manual cancellation).
/// Both cameras capture independently and are paired during concatenation.
/// </summary>
public sealed class RotatingLogCaptureManager
{
    private readonly CameraAcquisitionSettings _settings;
    private readonly DiagnosticsCollector _diagnostics;
    private readonly List<ScanFrame> _headFrames = [];
    private readonly List<ScanFrame> _tailFrames = [];
    private readonly Lock _captureLock = new();

    private bool _isCapturingActive;
    private DateTime _captureStartTime;
    private int _droppedFrameCount;
    private string _currentLogId;
    private byte[]? _headCameraConfigurationSnapshot;
    private byte[]? _tailCameraConfigurationSnapshot;

    public event EventHandler<string>? DiagnosticsMessage;

    public RotatingLogCaptureManager(
        CameraAcquisitionSettings settings,
        DiagnosticsCollector diagnostics)
    {
        _settings = settings
            ?? throw new ArgumentNullException(nameof(settings));
        _diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));

        _settings.Validate();
        _currentLogId = GenerateLogId();
    }

    public bool IsCapturingActive
    {
        get
        {
            lock (_captureLock)
                return _isCapturingActive;
        }
    }

    public int HeadFrameCount
    {
        get
        {
            lock (_captureLock)
                return _headFrames.Count;
        }
    }

    public int TailFrameCount
    {
        get
        {
            lock (_captureLock)
                return _tailFrames.Count;
        }
    }

    /// <summary>
    /// Number of complete head/tail positions currently available.
    /// </summary>
    public int CapturedFrameCount
    {
        get
        {
            lock (_captureLock)
                return Math.Min(_headFrames.Count, _tailFrames.Count);
        }
    }

    /// <summary>
    /// Begins a new dual-camera capture session. Both cameras must have a valid
    /// </summary>
    /// <param name="headCameraConfigurationSnapshot">The head camera configuration snapshot.</param>
    /// <param name="tailCameraConfigurationSnapshot">The tail camera configuration snapshot.</param>
    /// <exception cref="InvalidOperationException"></exception>
    public void BeginCapture(
        ReadOnlyMemory<byte> headCameraConfigurationSnapshot,
        ReadOnlyMemory<byte> tailCameraConfigurationSnapshot)
    {
        if (headCameraConfigurationSnapshot.IsEmpty)
        {
            throw new InvalidOperationException(
                "The head camera has no loaded camera-configuration snapshot. Reconnect the camera before starting a capture.");
        }

        if (tailCameraConfigurationSnapshot.IsEmpty)
        {
            throw new InvalidOperationException(
                "The tail camera has no loaded camera-configuration snapshot. Reconnect the camera before starting a capture.");
        }

        lock (_captureLock)
        {
            if (_isCapturingActive)
            {
                throw new InvalidOperationException(
                    "A capture is already in progress.");
            }

            // Defensive cleanup in case a previous capture was abandoned.
            DisposeFrames(_headFrames);
            DisposeFrames(_tailFrames);
            _headFrames.Clear();
            _tailFrames.Clear();

            _droppedFrameCount = 0;
            _captureStartTime = DateTime.UtcNow;
            _isCapturingActive = true;
            _currentLogId = GenerateLogId();
            _headCameraConfigurationSnapshot =
                headCameraConfigurationSnapshot.ToArray();
            _tailCameraConfigurationSnapshot =
                tailCameraConfigurationSnapshot.ToArray();

            LogDiagnostics(
                $"Capture started for Log ID: {_currentLogId}");

            LogDiagnostics(
                $"Capture will continue until ScanComplete or manual stop. " +
                $"(Reference target: {_settings.TargetFramePairsPerLog} frame pairs)");
        }
    }

    /// <summary>
    /// Adds a new frame from the specified camera position to the capture session.
    /// </summary>
    /// <param name="position">The camera position (head or tail) from which the frame was captured.</param>
    /// <param name="frameArgs">The frame event arguments containing the captured frame.</param>
    /// <returns>True if the frame was successfully added; otherwise, false.</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public bool AddFrame(
        CameraPosition position,
        CameraFrameEventArgs frameArgs)
    {
        ArgumentNullException.ThrowIfNull(frameArgs);

        _diagnostics.RecordFrameReceived(position);

        if (frameArgs.Frame == null || frameArgs.Frame.IsEmpty)
        {
            frameArgs.Frame?.Dispose();
            _diagnostics.RecordFrameDropped(position);
            LogDiagnostics($"Rejected empty {position} frame.");
            return false;
        }

        lock (_captureLock)
        {
            if (!_isCapturingActive)
            {
                frameArgs.Frame.Dispose();
                _diagnostics.RecordFrameDropped(position);
                return false;
            }

            List<ScanFrame> targetFrames = position switch
            {
                CameraPosition.Head => _headFrames,
                CameraPosition.Tail => _tailFrames,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(position),
                    position,
                    "Unsupported camera position.")
            };

            int frameIndex = targetFrames.Count;

            try
            {
                var scanFrame = new ScanFrame
                {
                    FrameIndex = frameIndex,
                    AngleDeg =
                        frameIndex *
                        _settings.GetAngularStepDegrees(),
                    ImagePath = string.Empty,
                    GrayImage = frameArgs.Frame,
                    IsValid = true,
                    StatusMessage =
                        $"{position} live captured"
                };

                targetFrames.Add(scanFrame);

                LogDiagnostics(
                    $"{position} capture progress: " +
                    $"{targetFrames.Count} frames");

                return true;
            }
            catch
            {
                frameArgs.Frame.Dispose();
                _droppedFrameCount++;
                _diagnostics.RecordFrameDropped(position);
                throw;
            }
        }
    }

    /// <summary>
    /// Determines whether the capture session is complete.
    /// </summary>
    /// <returns>True if the capture session is complete; otherwise, false.</returns>
    public bool IsCaptureComplete()
    {
        lock (_captureLock)
        {
            return _isCapturingActive &&
                   _headFrames.Count > 0 &&
                   _tailFrames.Count > 0;
        }
    }
    
    /// <summary>
    /// Finishes the current capture session and returns the completed log capture.
    /// </summary>
    /// <returns>The completed log capture.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no capture is currently active or if there are no frames captured.</exception>
    public CompletedLogCapture FinishCapture()
    {
        lock (_captureLock)
        {
            if (!_isCapturingActive)
            {
                throw new InvalidOperationException(
                    "No capture is currently active.");
            }

            if (_headFrames.Count == 0 ||
                _tailFrames.Count == 0)
            {
                throw new InvalidOperationException(
                    "A dual-camera capture requires at least one " +
                    "head frame and one tail frame.");
            }

            _isCapturingActive = false;

            DateTime captureEndTime = DateTime.UtcNow;
            double captureDuration =
                (captureEndTime - _captureStartTime).TotalSeconds;

            int originalHeadCount = _headFrames.Count;
            int originalTailCount = _tailFrames.Count;

            List<ScanFrame> concatenatedFrames;

            try
            {
                concatenatedFrames =
                    DualCameraFrameConcatenator.ConcatenatePairs(
                        _headFrames,
                        _tailFrames,
                        _settings.Concatenation,
                        _settings.GetAngularStepDegrees());
            }
            finally
            {
                // The concatenated frames own independent image data.
                // Raw camera halves are no longer needed by the processing
                // pipeline and are released immediately.
                DisposeFrames(_headFrames);
                DisposeFrames(_tailFrames);
                _headFrames.Clear();
                _tailFrames.Clear();
            }

            AcquisitionMode acquisitionMode =
                _settings.HeadCamera.Mode;

            // Use the original frame counts to track what was actually captured.
            // Each concatenated frame is created from one synchronized head/tail pair,
            // so the number of concatenated frames equals Math.Min(originalHeadCount, originalTailCount).
            int actualFrameCount = concatenatedFrames.Count;

            var result = new CompletedLogCapture(
                logId: _currentLogId,
                concatenatedFrames: concatenatedFrames,
                headFrameCount: originalHeadCount,
                tailFrameCount: originalTailCount,
                captureDurationSeconds: captureDuration,
                captureStartedUtc: _captureStartTime,
                captureCompletedUtc: captureEndTime,
                headCameraConfigurationSnapshot:
                    _headCameraConfigurationSnapshot ?? [],
                tailCameraConfigurationSnapshot:
                    _tailCameraConfigurationSnapshot ?? [],
                droppedFrameCount: _droppedFrameCount,
                acquisitionMode: acquisitionMode,
                notes:
                    $"Concatenated frames: {actualFrameCount}; " +
                    $"Original head source frames: {originalHeadCount}; " +
                    $"Original tail source frames: {originalTailCount}; " +
                    $"Angular step: " +
                    $"{_settings.GetAngularStepDegrees():F2}°.");

            _headCameraConfigurationSnapshot = null;
            _tailCameraConfigurationSnapshot = null;

            LogDiagnostics(
                $"Capture finished: {result.FramePairCount} concatenated " +
                $"processing frames created from {originalHeadCount} head and " +
                $"{originalTailCount} tail frames in {captureDuration:F2} seconds.");

            return result;
        }
    }
    
    /// <summary>
    /// Cancels the current capture session and releases all resources.
    /// </summary>
    public void CancelCapture()
    {
        lock (_captureLock)
        {
            _isCapturingActive = false;

            DisposeFrames(_headFrames);
            DisposeFrames(_tailFrames);

            _headFrames.Clear();
            _tailFrames.Clear();
            _headCameraConfigurationSnapshot = null;
            _tailCameraConfigurationSnapshot = null;

            LogDiagnostics(
                "Capture cancelled and all head/tail frames disposed.");
        }
    }

    private static void DisposeFrames(
        IEnumerable<ScanFrame> frames)
    {
        foreach (ScanFrame frame in frames)
        {
            frame.GrayImage?.Dispose();
            frame.BinaryImage?.Dispose();
            frame.OverlayImage?.Dispose();
        }
    }

    /// <summary>
    /// Generates a unique log ID based on the current timestamp.
    /// </summary>
    /// <returns>The generated unique log ID.</returns>
    private static string GenerateLogId()
    {
        return $"Log_{DateTime.Now:yyyyMMdd_HHmmss_fff}";
    }

    /// <summary>
    /// Logs a diagnostic message to the registered event handlers.
    /// </summary>
    /// <param name="message">The diagnostic message to log.</param>
    private void LogDiagnostics(string message)
    {
        DiagnosticsMessage?.Invoke(this, message);
    }
}
