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
    private readonly object _sync = new();
    private readonly CameraAcquisitionSettings _settings;
    private readonly int _targetPairCount;
    private readonly List<ScanFrame> _headFrames = new();
    private readonly List<ScanFrame> _tailFrames = new();
    private readonly TaskCompletionSource<CalibrationCaptureBatch> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private byte[] _headConfiguration = [];
    private byte[] _tailConfiguration = [];
    private bool _completed;
    private bool _disposed;

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

    public Task<CalibrationCaptureBatch> Completion => _completion.Task;

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

    private static void DisposeFrames(IEnumerable<ScanFrame> frames)
    {
        foreach (ScanFrame frame in frames)
        {
            frame.GrayImage?.Dispose();
            frame.BinaryImage?.Dispose();
            frame.OverlayImage?.Dispose();
        }
    }

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
