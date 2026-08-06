using OptimizationXY.Charger.Models.Acquisition;
using OptimizationXY.Charger.Models.Calibration;
using OptimizationXY.Charger.Models.Core;
using OptimizationXY.Charger.Services.Acquisition;

namespace OptimizationXY.Charger.Services.Calibration;

/// <summary>
/// Collects a bounded number of synchronized head/tail frames for scanner
/// calibration without starting the normal production processing pipeline.
/// Ownership of accepted CameraFrameEventArgs.Frame instances transfers to
/// this session.
/// </summary>
public sealed class CalibrationFrameCaptureSession : IDisposable
{
    private readonly Lock _sync = new();
    private readonly CameraAcquisitionSettings _settings;
    private readonly int _targetPairCount;
    private readonly List<ScanFrame> _headFrames = [];
    private readonly List<ScanFrame> _tailFrames = [];
    private readonly TaskCompletionSource<CalibrationCaptureBatch> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private byte[] _headConfiguration = [];
    private byte[] _tailConfiguration = [];
    private bool _completed;
    private bool _disposed;


    /// <summary>
    /// Initializes a new instance of the CalibrationFrameCaptureSession class with the specified settings, target pair count, and camera configuration snapshots.
    /// </summary>
    /// <param name="settings">The camera acquisition settings.</param>
    /// <param name="targetPairCount">The target number of frame pairs to capture.</param>
    /// <param name="headConfiguration">The head camera configuration snapshot.</param>
    /// <param name="tailConfiguration">The tail camera configuration snapshot.</param>
    /// <exception cref="ArgumentNullException">Thrown if the settings parameter is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the targetPairCount is less than 1 or greater than the maximum allowed frame pairs per log.</exception>
    /// <exception cref="InvalidOperationException">Thrown if either camera configuration snapshot is empty.</exception>
    public CalibrationFrameCaptureSession(
        CameraAcquisitionSettings settings,
        int targetPairCount,
        ReadOnlyMemory<byte> headConfiguration,
        ReadOnlyMemory<byte> tailConfiguration)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        if (targetPairCount < 1 || targetPairCount > settings.TargetFramePairsPerLog)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetPairCount),
                $"Calibration capture pairs must be between 1 and {settings.TargetFramePairsPerLog}.");
        }

        if (headConfiguration.IsEmpty || tailConfiguration.IsEmpty)
        {
            throw new InvalidOperationException(
                "Both cameras must have a successfully loaded camera-configuration snapshot before calibration capture.");
        }

        _targetPairCount = targetPairCount;
        _headConfiguration = headConfiguration.ToArray();
        _tailConfiguration = tailConfiguration.ToArray();
    }

    /// <summary>
    /// Gets a value indicating whether the calibration capture session is still active and accepting frames.
    /// </summary>
    public bool IsActive
    {
        get
        {
            lock (_sync)
            {
                return !_completed && !_disposed;
            }
        }
    }

    /// <summary>
    /// Gets the number of head frames that have been captured in the calibration session.
    /// </summary>
    public int HeadFrameCount
    {
        get
        {
            lock (_sync)
            {
                return _headFrames.Count;
            }
        }
    }

    
    /// <summary>
    /// Gets the number of tail frames that have been captured in the calibration session.
    /// </summary>
    public int TailFrameCount
    {
        get
        {
            lock (_sync)
            {
                return _tailFrames.Count;
            }
        }
    }

    /// <summary>
    /// Gets a task that completes when the calibration capture session is finished, either successfully with a
    /// CalibrationCaptureBatch or with an exception if the session was canceled or failed.
    /// </summary>
    public Task<CalibrationCaptureBatch> Completion => _completion.Task;


    /// <summary>
    /// Attempts to add a frame to the calibration capture session for the specified camera position (head or tail).
    /// </summary>
    /// <param name="position">The camera position (head or tail) from which the frame was captured.</param>
    /// <param name="frameArgs">The frame event arguments containing the captured frame.</param>
    /// <returns>True if the frame was successfully added; otherwise, false.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the position parameter is not a valid CameraPosition value.</exception>
    public bool TryAddFrame(
        CameraPosition position,
        CameraFrameEventArgs frameArgs)
    {
        ArgumentNullException.ThrowIfNull(frameArgs);

        lock (_sync)
        {
            if (_completed || _disposed)
            {
                frameArgs.Frame.Dispose();
                return false;
            }

            List<ScanFrame> target = position switch
            {
                CameraPosition.Head => _headFrames,
                CameraPosition.Tail => _tailFrames,
                _ => throw new ArgumentOutOfRangeException(nameof(position))
            };

            if (target.Count >= _targetPairCount)
            {
                frameArgs.Frame.Dispose();
                return false;
            }

            int index = target.Count;
            target.Add(new ScanFrame
            {
                FrameIndex = index,
                AngleDeg = index * _settings.GetAngularStepDegrees(),
                GrayImage = frameArgs.Frame,
                IsValid = true,
                StatusMessage = $"Calibration {position} frame"
            });

            if (_headFrames.Count >= _targetPairCount &&
                _tailFrames.Count >= _targetPairCount)
            {
                CompleteCaptureLocked();
            }

            return true;
        }
    }

    /// <summary>
    /// Cancels the calibration capture session, disposing of any captured frames and completing the session with an OperationCanceledException.
    /// </summary>
    /// <param name="reason">The reason for canceling the calibration capture session.</param>
    public void Cancel(string reason)
    {
        lock (_sync)
        {
            if (_completed || _disposed)
            {
                return;
            }

            _completed = true;
            DisposeFrames(_headFrames);
            DisposeFrames(_tailFrames);
            _headFrames.Clear();
            _tailFrames.Clear();
            _completion.TrySetException(
                new OperationCanceledException(reason));
        }
    }


    /// <summary>
    /// Completes the calibration capture session by concatenating the captured head and tail frames,
    /// creating a CalibrationCaptureBatch, and completing the session's
    /// task. This method is called when the target number of frame pairs has been captured.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if a concatenated calibration frame is empty.</exception>
    private void CompleteCaptureLocked()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;

        try
        {
            List<ScanFrame> combined =
                DualCameraFrameConcatenator.ConcatenatePairs(
                    _headFrames,
                    _tailFrames,
                    _settings.Concatenation,
                    _settings.GetAngularStepDegrees());

            var frames = combined
                .Select(frame => frame.GrayImage ??
                    throw new InvalidOperationException(
                        "A concatenated calibration frame was empty."))
                .ToList();

            foreach (ScanFrame frame in combined)
            {
                frame.GrayImage = null;
            }

            _completion.TrySetResult(new CalibrationCaptureBatch
            {
                ConcatenatedFrames = frames,
                HeadCameraConfigurationSnapshot = _headConfiguration,
                TailCameraConfigurationSnapshot = _tailConfiguration,
                CapturedUtc = DateTime.UtcNow,
                HeadFrameCount = _headFrames.Count,
                TailFrameCount = _tailFrames.Count
            });
        }
        catch (Exception ex)
        {
            _completion.TrySetException(ex);
        }
        finally
        {
            DisposeFrames(_headFrames);
            DisposeFrames(_tailFrames);
            _headFrames.Clear();
            _tailFrames.Clear();
        }
    }


    /// <summary>
    /// Disposes of the GrayImage, BinaryImage, and OverlayImage properties of each ScanFrame in the provided collection.
    /// </summary>
    /// <param name="frames">The collection of ScanFrame objects to dispose.</param>
    private static void DisposeFrames(IEnumerable<ScanFrame> frames)
    {
        foreach (ScanFrame frame in frames)
        {
            frame.GrayImage?.Dispose();
            frame.BinaryImage?.Dispose();
            frame.OverlayImage?.Dispose();
        }
    }


    /// <summary>
    /// Disposes of the calibration capture session, releasing any captured
    /// frames and completing the session's task if it has not already been completed or canceled.
    /// </summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (!_completed)
            {
                _completed = true;
                _completion.TrySetCanceled();
            }

            DisposeFrames(_headFrames);
            DisposeFrames(_tailFrames);
            _headFrames.Clear();
            _tailFrames.Clear();
            _headConfiguration = [];
            _tailConfiguration = [];
        }
    }
}
