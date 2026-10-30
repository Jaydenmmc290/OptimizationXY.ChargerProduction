namespace OptimizationXY.Charger.Models.Operator;


/// <summary>
/// Live status shown on the lathe-operator screen (OperatorScanQualityForm):
/// calibration loaded, PLC enabled/connected, the PLC ScanStart/ScanComplete
/// signals, and how many head/tail pairs have been captured so far.
/// </summary>
public sealed class OperatorRuntimeStatus
{

    /// <summary>
    /// Gets or sets a value indicating whether an active scanner calibration is loaded.
    /// </summary>
    public bool CalibrationActive { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the system is ready for production scanning
    /// (calibration active AND PLC connected).
    /// </summary>
    public bool SystemReady => CalibrationActive && PlcConnected;

    /// <summary>
    /// Gets or sets a value indicating whether the PLC (Programmable Logic Controller) is enabled.
    /// </summary>
    public bool PlcEnabled { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the PLC is connected.
    /// </summary>
    public bool PlcConnected { get; init; }

    /// <summary>
    /// Gets or sets whether the PLC ScanStart signal is currently on (the log is turning and frames are being captured).
    /// </summary>
    public bool ScanStart { get; init; }

    /// <summary>
    /// Gets or sets whether the PLC ScanComplete signal is on (capture for this log is finished).
    /// </summary>
    public bool ScanComplete { get; init; }

    /// <summary>
    /// Gets or sets the number of captured pairs during the scan process.
    /// </summary>
    public int CapturedPairs { get; init; }

    /// <summary>
    /// Gets or sets the nominal pair count (Acquisition.TargetFramePairsPerLog). A
    /// production scan captures every pair until ScanComplete, so the real count
    /// is normally higher than this (typically 85-100).
    /// </summary>
    public int ExpectedPairs { get; init; }
}
