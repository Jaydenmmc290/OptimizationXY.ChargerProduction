using OptimizationXY.Charger.Models.Acquisition;
using OptimizationXY.Charger.Models.Calibration;


namespace OptimizationXY.Charger.UI.Forms;

/// <summary>
/// What the production setup wizard is allowed to ask the main form to do.
/// The main form owns the cameras; the wizard never opens them itself, it
/// calls these delegates instead (MainForm builds this object when it opens
/// the wizard).
/// </summary>
public sealed class ProductionSetupContext
{
    /// <summary>Current connection/configuration state of both cameras.</summary>
    public required Func<CameraHardwareStatus> GetCameraStatus { get; init; }
    /// <summary>Re-initialises both camera services (refused while a capture is running), then returns their state.</summary>
    public required Func<Task<CameraHardwareStatus>> ReconnectCamerasAsync { get; init; }
    /// <summary>
    /// Captures the given number of head/tail frame pairs for calibration or
    /// laser checks. The caller disposes the returned batch.
    /// </summary>
    public required Func<int, CancellationToken, Task<CalibrationCaptureBatch>> CaptureCalibrationFramesAsync { get; init; }
    /// <summary>Opens the head camera live preview window.</summary>
    public required Action ShowHeadPreview { get; init; }
    /// <summary>Opens the tail camera live preview window.</summary>
    public required Action ShowTailPreview { get; init; }
    /// <summary>Writes a line to the main form's diagnostics log.</summary>
    public required Action<string> LogMessage { get; init; }

    /// <summary>Lists the cameras the Sapera SDK can see (empty without the SDK).</summary>
    public required Func<IReadOnlyList<DiscoveredCamera>>
    DiscoverCameras
    { get; init; }
}
