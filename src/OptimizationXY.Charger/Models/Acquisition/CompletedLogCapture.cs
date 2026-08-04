using OptimizationXY.Charger.Models.Core;
using System.Collections.ObjectModel;

namespace OptimizationXY.Charger.Models.Acquisition;

/// <summary>
/// Immutable result of one completed dual-camera log capture.
///
/// The Frames collection contains the full-length concatenated processing
/// images. Therefore 36 head frames plus 36 tail frames become exactly
/// 36 processing frames, matching the supplied 40.zip example.
/// </summary>
public sealed class CompletedLogCapture
{
    private readonly byte[] _headCameraConfigurationSnapshot;
    private readonly byte[] _tailCameraConfigurationSnapshot;

    public string LogId { get; }

    public ReadOnlyCollection<ScanFrame> Frames { get; }

    public int HeadFrameCount { get; }
    public int TailFrameCount { get; }

    public int FramePairCount => Frames.Count;
    public int TotalSourceFrameCount => HeadFrameCount + TailFrameCount;

    public double CaptureDurationSeconds { get; }
    public DateTime CaptureStartedUtc { get; }
    public DateTime CaptureCompletedUtc { get; }
    public int DroppedFrameCount { get; }
    public AcquisitionMode AcquisitionMode { get; }
    public string Notes { get; }
    public ReadOnlyMemory<byte> HeadCameraConfigurationSnapshot =>
        new(_headCameraConfigurationSnapshot);
    public ReadOnlyMemory<byte> TailCameraConfigurationSnapshot =>
        new(_tailCameraConfigurationSnapshot);

    public CompletedLogCapture(
        string logId,
        IReadOnlyList<ScanFrame> concatenatedFrames,
        int headFrameCount,
        int tailFrameCount,
        double captureDurationSeconds,
        DateTime captureStartedUtc,
        DateTime captureCompletedUtc,
        ReadOnlyMemory<byte> headCameraConfigurationSnapshot,
        ReadOnlyMemory<byte> tailCameraConfigurationSnapshot,
        int droppedFrameCount = 0,
        AcquisitionMode acquisitionMode = AcquisitionMode.FrameRate,
        string notes = "")
    {
        if (string.IsNullOrWhiteSpace(logId))
            throw new ArgumentException("Log ID is required.", nameof(logId));

        ArgumentNullException.ThrowIfNull(concatenatedFrames);

        if (concatenatedFrames.Count == 0)
        {
            throw new ArgumentException(
                "At least one concatenated processing frame is required.",
                nameof(concatenatedFrames));
        }

        if (headCameraConfigurationSnapshot.IsEmpty)
        {
            throw new ArgumentException(
                "The exact head-camera-configuration snapshot is required.",
                nameof(headCameraConfigurationSnapshot));
        }

        if (tailCameraConfigurationSnapshot.IsEmpty)
        {
            throw new ArgumentException(
                "The exact tail-camera-configuration snapshot is required.",
                nameof(tailCameraConfigurationSnapshot));
        }

        LogId = logId;
        Frames = new ReadOnlyCollection<ScanFrame>(
            new List<ScanFrame>(concatenatedFrames));

        HeadFrameCount = headFrameCount;
        TailFrameCount = tailFrameCount;
        CaptureDurationSeconds = captureDurationSeconds;
        CaptureStartedUtc = captureStartedUtc;
        CaptureCompletedUtc = captureCompletedUtc;
        DroppedFrameCount = droppedFrameCount;
        AcquisitionMode = acquisitionMode;
        Notes = notes ?? string.Empty;
        _headCameraConfigurationSnapshot =
            headCameraConfigurationSnapshot.ToArray();
        _tailCameraConfigurationSnapshot =
            tailCameraConfigurationSnapshot.ToArray();
    }
}
