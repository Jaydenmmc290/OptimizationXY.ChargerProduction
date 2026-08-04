using OptimizationXY.Charger.Models.Calibration;

namespace OptimizationXY.Charger.UI.Forms;

public sealed class ProductionSetupContext
{
    public required Func<CameraHardwareStatus> GetCameraStatus { get; init; }
    public required Func<Task<CameraHardwareStatus>> ReconnectCamerasAsync { get; init; }
    public required Func<int, CancellationToken, Task<CalibrationCaptureBatch>> CaptureCalibrationFramesAsync { get; init; }
    public required Action ShowHeadPreview { get; init; }
    public required Action ShowTailPreview { get; init; }
    public required Action<string> LogMessage { get; init; }
}
