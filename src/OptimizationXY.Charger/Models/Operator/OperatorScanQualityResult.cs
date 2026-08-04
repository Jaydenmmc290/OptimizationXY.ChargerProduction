using OptimizationXY.Charger.UI.Controls;

namespace OptimizationXY.Charger.Models.Operator;

public sealed class OperatorScanQualityResult
{
    public ScanQualityStatus Status { get; init; }
    public string OperatorMessage { get; init; } = string.Empty;
    public string DetailMessage { get; init; } = string.Empty;
    public int FramesUsed { get; init; }
    public int ExpectedFrames { get; init; }
    public double ProfileCoveragePercent { get; init; }
    public double ValidSlicePercent { get; init; }
    public double LogLengthInches { get; init; }
    public double AverageDiameterInches { get; init; }
    public double MinimumDiameterInches { get; init; }
    public double MaximumDiameterInches { get; init; }
    public double LargestMissingGapInches { get; init; }
    public double ProcessingSeconds { get; init; }
    public IReadOnlyList<LogProfilePoint> ProfilePoints { get; init; } =
        Array.Empty<LogProfilePoint>();
}
