using Emgu.CV;

/// <summary>
/// Represents a batch of calibration captures, including concatenated frames and camera configuration snapshots.
/// </summary>
namespace OptimizationXY.Charger.Models.Calibration;



public sealed class CalibrationCaptureBatch : IDisposable
{
    
    // Gets the concatenated frames captured during calibration.
    public required IReadOnlyList<Mat> ConcatenatedFrames { get; init; }

    // Gets the head camera configuration snapshot captured during calibration.
    public required byte[] HeadCameraConfigurationSnapshot { get; init; }

    // Gets the tail camera configuration snapshot captured during calibration.
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
