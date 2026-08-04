namespace OptimizationXY.Charger.Models.Calibration;

public sealed class CameraHardwareStatus
{
    public bool HeadConnected { get; set; }
    public bool TailConnected { get; set; }
    public string HeadServerName { get; set; } = string.Empty;
    public string TailServerName { get; set; } = string.Empty;
    public string HeadSerialNumber { get; set; } = string.Empty;
    public string TailSerialNumber { get; set; } = string.Empty;
    public string HeadConfigurationPath { get; set; } = string.Empty;
    public string TailConfigurationPath { get; set; } = string.Empty;
    public bool HeadConfigurationLoaded { get; set; }
    public bool TailConfigurationLoaded { get; set; }
}
