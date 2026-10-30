using Emgu.CV;

namespace OptimizationXY.Charger.Models.Calibration;



/// <summary>
/// A batch of joined (head + tail) frames captured while a calibration
/// reference (a round tube of known diameter, or the home cable) is in the
/// scanner, plus the camera settings snapshots in force at that moment. The
/// batch owns the images: dispose it to free the image memory.
/// </summary>
public sealed class CalibrationCaptureBatch : IDisposable
{
    
    // Joined head+tail images, one per captured pair.
    public required IReadOnlyList<Mat> ConcatenatedFrames { get; init; }

    // Head-camera settings snapshot (bytes) - hashed into the calibration fingerprint.
    public required byte[] HeadCameraConfigurationSnapshot { get; init; }

    // Tail-camera settings snapshot (bytes) - hashed into the calibration fingerprint.
    public required byte[] TailCameraConfigurationSnapshot { get; init; }

    // Gets the UTC timestamp when the calibration capture batch was captured.
    public required DateTime CapturedUtc { get; init; }

    // Gets the number of frames captured for the head camera during calibration.
    public required int HeadFrameCount { get; init; }

    // Gets the number of frames captured for the tail camera during calibration.
    public required int TailFrameCount { get; init; }


    // Disposes of the resources used by the CalibrationCaptureBatch, including the concatenated frames.
    public void Dispose()
    {
        foreach (Mat frame in ConcatenatedFrames)
        {
            frame.Dispose();
        }
    }
}
