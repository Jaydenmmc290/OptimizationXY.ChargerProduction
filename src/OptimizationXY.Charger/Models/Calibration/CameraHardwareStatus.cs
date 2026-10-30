namespace OptimizationXY.Charger.Models.Calibration;


/// <summary>
/// Snapshot of the two cameras' connection state for display and readiness
/// checks: whether each is connected, its Sapera server name/device index,
/// and (legacy CCF mode only) its configuration file path and load result.
/// </summary>
public sealed class CameraHardwareStatus
{

    //HeadConnected indicates whether the head camera is connected.
    public bool HeadConnected { get; set; }

    //TailConnected indicates whether the tail camera is connected.
    public bool TailConnected { get; set; }

    //HeadServerName is the name of the server associated with the head camera.
    public string HeadServerName { get; set; } = string.Empty;

    //TailServerName is the name of the server associated with the tail camera.
    public string TailServerName { get; set; } = string.Empty;

    // HeadDeviceIndex is the device index of the head camera in Sapera.
    public int HeadDeviceIndex { get; set; }

    // TailDeviceIndex is the device index of the tail camera in Sapera.
    public int TailDeviceIndex { get; set; }

    // HeadConfigurationPath: legacy .ccf file path for the head camera (empty in programmatic mode).
    public string HeadConfigurationPath { get; set; } = string.Empty;

    // TailConfigurationPath: legacy .ccf file path for the tail camera (empty in programmatic mode).
    public string TailConfigurationPath { get; set; } = string.Empty;

    // HeadConfigurationLoaded indicates whether the configuration for the head camera has been successfully loaded.
    public bool HeadConfigurationLoaded { get; set; }

    // TailConfigurationLoaded indicates whether the configuration for the tail camera has been successfully loaded.
    public bool TailConfigurationLoaded { get; set; }
}
