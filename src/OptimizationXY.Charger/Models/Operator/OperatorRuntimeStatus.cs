namespace OptimizationXY.Charger.Models.Operator;


/// <summary>
/// Represents the runtime status of an operator, including PLC connection status, calibration status, scan progress, and captured pairs information.
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
    /// Gets or sets a value indicating whether the scan has started.
    /// </summary>
    public bool ScanStart { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the scan is complete.
    /// </summary>
    public bool ScanComplete { get; init; }

    /// <summary>
    /// Gets or sets the number of captured pairs during the scan process.
    /// </summary>
    public int CapturedPairs { get; init; }

    /// <summary>
    /// Gets or sets the expected number of pairs to be captured during the scan process.
    /// </summary>
    public int ExpectedPairs { get; init; }
}
