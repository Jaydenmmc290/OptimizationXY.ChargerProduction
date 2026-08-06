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
    private readonly byte[] _headCameraConfigurationSnapshot; // Exact head-camera-configuration snapshot at the time of capture.
    private readonly byte[] _tailCameraConfigurationSnapshot; // Exact tail-camera-configuration snapshot at the time of capture.

    public string LogId { get; } // Unique identifier for the log capture.

    public ReadOnlyCollection<ScanFrame> Frames { get; } // Full-length concatenated processing frames from the dual-camera capture.

    public int HeadFrameCount { get; } // Number of head frames captured during the log capture.
    public int TailFrameCount { get; } // Number of tail frames captured during the log capture.

    public int FramePairCount => Frames.Count; // Number of frame pairs in the concatenated Frames collection.
    public int TotalSourceFrameCount => HeadFrameCount + TailFrameCount; // Total number of source frames captured (head + tail).

    public double CaptureDurationSeconds { get; } // Duration of the log capture in seconds.
    public DateTime CaptureStartedUtc { get; } // UTC timestamp when the log capture started.
    public DateTime CaptureCompletedUtc { get; } // UTC timestamp when the log capture completed.
    public int DroppedFrameCount { get; } // Number of frames dropped during the log capture (if any).
    public AcquisitionMode AcquisitionMode { get; } // Mode of acquisition used during the log capture (e.g., FrameRate, Triggered, etc.).
    public string Notes { get; } // Optional notes or comments associated with the log capture.

    // The following properties expose the exact camera configuration snapshots as read-only memory.
    public ReadOnlyMemory<byte> HeadCameraConfigurationSnapshot =>
        new(_headCameraConfigurationSnapshot);

    // The following properties expose the exact camera configuration snapshots as read-only memory.
    public ReadOnlyMemory<byte> TailCameraConfigurationSnapshot =>
        new(_tailCameraConfigurationSnapshot);

    /// Constructor for creating a CompletedLogCapture instance.
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


        // Validate that the head and tail frame counts match the concatenated frames count.
        if (headFrameCount + tailFrameCount != concatenatedFrames.Count)
        {
            throw new ArgumentException(
                "The sum of headFrameCount and tailFrameCount must equal the number of concatenated frames.",
                nameof(concatenatedFrames));
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
