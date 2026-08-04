namespace OptimizationXY.Charger.Models.Operator;

public sealed class OperatorRuntimeStatus
{
    public bool PlcEnabled { get; init; }
    public bool PlcConnected { get; init; }
    public bool ScanStart { get; init; }
    public bool ScanComplete { get; init; }
    public int CapturedPairs { get; init; }
    public int ExpectedPairs { get; init; }
}
