using OptimizationXY.Charger.Models.Optimization.Spindle;

namespace OptimizationXY.Charger.Models.Optimization.Recovery;

public sealed class DirectRecoverySearchResult
{
    public OptimizedSpindleMovement Movement { get; set; } = new();
    public RecoverySearchMetrics Metrics { get; set; } = new();
    public int EvaluatedCandidates { get; set; }
    public int RejectedCandidates { get; set; }
    public string SearchSummary { get; set; } = string.Empty;
}
