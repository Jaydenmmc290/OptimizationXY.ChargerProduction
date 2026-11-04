using Emgu.CV;

namespace OptimizationXY.Charger.Models.Acquisition;

/// <summary>
/// Event arguments passed when a camera frame is acquired.
/// Contains the managed image and metadata.
/// The Frame property owns a cloned Mat (not a pointer to the unmanaged Sapera buffer).
/// </summary>
public sealed class CameraFrameEventArgs : EventArgs
{
    /// <summary>
    /// A managed copy of the frame (Mat).
    /// This is safe to use after the Sapera callback returns.
    /// The caller takes ownership and must dispose when done.
    /// </summary>
    public Mat Frame { get; }

    /// <summary>
    /// Monotonically increasing frame number since camera started.
    /// </summary>
    public long FrameNumber { get; }

    /// <summary>
    /// UTC timestamp when the frame was acquired.
    /// </summary>
    public DateTime TimestampUtc { get; }

    /// <summary>
    /// When the frame callback ran, <see cref="System.Diagnostics.Stopwatch"/>
    /// ticks (high resolution, for pairing head and tail frames by time).
    /// </summary>
    public long HostTicks { get; }

    /// <summary>
    /// Constructor to create camera frame event arguments.
    /// </summary>
    public CameraFrameEventArgs(Mat frame, long frameNumber, DateTime timestampUtc)
        : this(frame, frameNumber, timestampUtc, System.Diagnostics.Stopwatch.GetTimestamp())
    {
    }

    /// <summary>
    /// Constructor with the host arrival time in Stopwatch ticks.
    /// </summary>
    public CameraFrameEventArgs(Mat frame, long frameNumber, DateTime timestampUtc, long hostTicks)
    {
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
        FrameNumber = frameNumber;
        TimestampUtc = timestampUtc;
        HostTicks = hostTicks;
    }
}
