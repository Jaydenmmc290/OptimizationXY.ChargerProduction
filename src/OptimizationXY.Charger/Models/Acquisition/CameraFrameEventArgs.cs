using Emgu.CV;

namespace OptimizationXY.Charger.Models.Acquisition;

/// <summary>
/// Event arguments passed when a camera frame is acquired.
/// Contains the managed image and metadata.
/// The Frame property owns a cloned Mat (not a pointer to the unmanaged Sapera buffer).
/// </summary>
/// <remarks>
/// Constructor to create camera frame event arguments.
/// </remarks>
public sealed class CameraFrameEventArgs(Mat frame, long frameNumber, DateTime timestampUtc) : EventArgs 
{
    /// <summary>
    /// A managed copy of the frame (Mat).
    /// This is safe to use after the Sapera callback returns.
    /// The caller takes ownership and must dispose when done.
    /// </summary>
    public Mat Frame { get; } = frame ?? throw new ArgumentNullException(nameof(frame));

    /// <summary>
    /// Monotonically increasing frame number since camera started.
    /// </summary>
    public long FrameNumber { get; } = frameNumber;

    /// <summary>
    /// UTC timestamp when the frame was acquired.
    /// </summary>
    public DateTime TimestampUtc { get; } = timestampUtc;
}
