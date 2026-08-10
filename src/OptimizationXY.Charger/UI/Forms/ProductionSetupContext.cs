using OptimizationXY.Charger.Models.Acquisition;
using OptimizationXY.Charger.Models.Calibration;
using OptimizationXY.Charger.Models.Acquisition;

namespace OptimizationXY.Charger.UI.Forms;

public sealed class ProductionSetupContext
{
    public required Func<CameraHardwareStatus> GetCameraStatus { get; init; }
    public required Func<Task<CameraHardwareStatus>> ReconnectCamerasAsync { get; init; }
    public required Func<int, CancellationToken, Task<CalibrationCaptureBatch>> CaptureCalibrationFramesAsync { get; init; }
    public required Action ShowHeadPreview { get; init; }
    public required Action ShowTailPreview { get; init; }
    public required Action<string> LogMessage { get; init; }

    public required Func<IReadOnlyList<DiscoveredCamera>>
    DiscoverCameras
    { get; init; }
}
