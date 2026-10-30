using OptimizationXY.Charger.Models.Core;
using System.Collections.ObjectModel;

namespace OptimizationXY.Charger.Models.Acquisition;

/// <summary>
/// Immutable (read-only) result of one completed dual-camera log capture -
/// everything the processing pipeline needs from the cameras for one block.
///
/// The Frames collection contains the full-length concatenated processing
/// images: each head frame is joined with the tail frame taken at the same
/// moment, so N head frames plus N tail frames become N processing frames.
/// A production scan keeps every pair between PLC ScanStart and ScanComplete
/// (typically 85-100). If one camera delivered more frames than the other,
/// the extra unpaired frames are not in <see cref="Frames"/>.
/// <para>
/// The capture also carries a byte-for-byte JSON snapshot of each camera's
/// settings at capture time, saved with the scan so it is always known how
/// the pictures were taken.
/// </para>
/// </summary>
public sealed class CompletedLogCapture
{
    private readonly byte[] _headCameraConfigurationSnapshot; // Exact head-camera settings snapshot (JSON bytes) at the time of capture.
    private readonly byte[] _tailCameraConfigurationSnapshot; // Exact tail-camera settings snapshot (JSON bytes) at the time of capture.

    public string LogId { get; } // Unique identifier for this log/block capture (used in folder names and logs).

    public ReadOnlyCollection<ScanFrame> Frames { get; } // Full-length concatenated processing frames from the dual-camera capture.

    public int HeadFrameCount { get; } // Number of head frames captured during the log capture.
    public int TailFrameCount { get; } // Number of tail frames captured during the log capture.

    public int FramePairCount => Frames.Count; // Number of head/tail pairs = number of joined processing frames.
    public int TotalSourceFrameCount => HeadFrameCount + TailFrameCount; // Total number of source frames captured (head + tail).

    public double CaptureDurationSeconds { get; } // Duration of the log capture in seconds.
    public DateTime CaptureStartedUtc { get; } // UTC timestamp when the log capture started.
    public DateTime CaptureCompletedUtc { get; } // UTC timestamp when the log capture completed.
    public int DroppedFrameCount { get; } // Frames lost during capture (e.g. a full hand-off queue); 0 on a healthy scan.
    public AcquisitionMode AcquisitionMode { get; } // Acquisition mode used for this capture: FrameRate (free-running) or Encoder (triggered).
    public string Notes { get; } // Optional notes or comments associated with the log capture.

    // Head-camera settings snapshot, exposed read-only so nobody can change it after capture.
    public ReadOnlyMemory<byte> HeadCameraConfigurationSnapshot =>
        new(_headCameraConfigurationSnapshot);

    // Tail-camera settings snapshot, exposed read-only so nobody can change it after capture.
    public ReadOnlyMemory<byte> TailCameraConfigurationSnapshot =>
        new(_tailCameraConfigurationSnapshot);

    /// <summary>
    /// Creates the capture record. Checks that there is at least one frame, that
    /// both camera snapshots are present, and that the number of joined frames
    /// equals the smaller of the head and tail frame counts (one per pair).
    /// The frame list and snapshots are copied so later changes to the caller's
    /// lists cannot alter this record.
    /// </summary>
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

        // Validate that the minimum of head and tail frame counts equals the concatenated frames count.
        // Since each concatenated frame is a synchronized pair, the number of paired frames
        // (which is the minimum of head/tail counts) should equal the concatenated output count.
        if (Math.Min(headFrameCount, tailFrameCount) != concatenatedFrames.Count)
        {
            throw new ArgumentException(
                "The minimum of headFrameCount and tailFrameCount must equal the number of concatenated frames. " +
                "Each concatenated frame is created from one synchronized head/tail pair.",
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
