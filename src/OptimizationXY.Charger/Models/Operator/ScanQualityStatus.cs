namespace OptimizationXY.Charger.Models.Operator;


/// <summary>
/// Represents the quality status of a scan operation in the system.
/// </summary>
public enum ScanQualityStatus
{
    Waiting,
    Capturing,
    Processing,
    Good,
    Warning,
    Failed
}
