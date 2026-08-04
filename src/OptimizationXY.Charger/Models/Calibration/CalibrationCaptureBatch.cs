using Emgu.CV;

namespace OptimizationXY.Charger.Models.Calibration;

public sealed class CalibrationCaptureBatch : IDisposable
{
    public required IReadOnlyList<Mat> ConcatenatedFrames { get; init; }
    public required byte[] HeadCameraConfigurationSnapshot { get; init; }
    public required byte[] TailCameraConfigurationSnapshot { get; init; }
    public required DateTime CapturedUtc { get; init; }
    public required int HeadFrameCount { get; init; }
    public required int TailFrameCount { get; init; }

    public void Dispose()
    {
        foreach (Mat frame in ConcatenatedFrames)
        {
            frame.Dispose();
        }
    }
}
