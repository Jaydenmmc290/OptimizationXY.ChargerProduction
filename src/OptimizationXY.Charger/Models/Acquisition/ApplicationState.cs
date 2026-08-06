namespace OptimizationXY.Charger.Models.Acquisition;

/// <summary>
/// High-level application state for the main operator form.
/// Tracks the overall workflow: camera initialization, capture, processing, and results.
/// </summary>
public enum ApplicationState 
{
    /// <summary>
    /// Application is idle, waiting for an operator action.
    /// Camera may be connected but no capture is active.
    /// </summary>
    Idle,

    /// <summary>
    /// Camera is connected and ready to begin a capture.
    /// </summary>
    CameraReady,

    /// <summary>
    /// An active capture is in progress. Frames are being collected.
    /// </summary>
    Capturing,

    /// <summary>
    /// Capture has completed. Frames are ready for processing.
    /// </summary>
    CaptureComplete,

    /// <summary>
    /// Processing pipeline is running (point cloud, centerline, optimization, etc.)
    /// </summary>
    Processing,

    /// <summary>
    /// Processing has completed. Results are available for display and saving.
    /// </summary>
    Completed,

    /// <summary>
    /// A fatal error occurred during camera operations or processing.
    /// </summary>
    Faulted,

    /// <summary>
    /// An active operation was cancelled by the user.
    /// </summary>
    Cancelled
}
