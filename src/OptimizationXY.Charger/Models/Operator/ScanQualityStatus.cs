namespace OptimizationXY.Charger.Models.Operator;


/// <summary>
/// Represents the quality status of a scan operation in the system.
/// </summary>
public enum ScanQualityStatus
{
    /// <summary>Waiting for the next PLC ScanStart.</summary>
    Waiting,
    /// <summary>Frames are being captured.</summary>
    Capturing,
    /// <summary>Capture finished; the pipeline is working out the position.</summary>
    Processing,
    /// <summary>Scan passed all quality checks.</summary>
    Good,
    /// <summary>
    /// Scan was usable but something should be looked at. With Plc.RequireGoodQuality
    /// on (the default) a Warning scan is NOT sent to the PLC - only Good is sent.
    /// </summary>
    Warning,
    /// <summary>Scan failed the quality gate; nothing is sent to the PLC for it (rescan the block).</summary>
    Failed
}
