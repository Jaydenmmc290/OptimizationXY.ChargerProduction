namespace OptimizationXY.Charger.Models.Calibration;


/// <summary>
/// Represents the status of camera hardware, including connection status, server names,
/// device indices, and configuration paths for both head and tail cameras.
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

    // HeadConfigurationPath is the file path to the configuration for the head camera.
    public string HeadConfigurationPath { get; set; } = string.Empty;

    // TailConfigurationPath is the file path to the configuration for the tail camera.
    public string TailConfigurationPath { get; set; } = string.Empty;

    // HeadConfigurationLoaded indicates whether the configuration for the head camera has been successfully loaded.
    public bool HeadConfigurationLoaded { get; set; }

    // TailConfigurationLoaded indicates whether the configuration for the tail camera has been successfully loaded.
    public bool TailConfigurationLoaded { get; set; }
}
