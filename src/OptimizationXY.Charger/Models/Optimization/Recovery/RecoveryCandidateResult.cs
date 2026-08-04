using OptimizationXY.Charger.Models.Analysis;
using OptimizationXY.Charger.Models.Optimization.Geometry;
using OptimizationXY.Charger.Models.Optimization.Spindle;

namespace OptimizationXY.Charger.Models.Optimization.Recovery;

public sealed class RecoveryCandidateResult
{
    public string StrategyName { get; set; } = string.Empty;
    public OptimizedSpindleMovement Movement { get; set; } = new();
    public PeelSimulationResult Peel { get; set; } = new();
    public InscribedGeometryResult? Geometry { get; set; }
    public int CandidateSearchCount { get; set; }

    public double MaxMovementMM =>
        new[]
        {
            Math.Abs(Movement.TailMoveXMM),
            Math.Abs(Movement.TailMoveYMM),
            Math.Abs(Movement.HeadMoveXMM),
            Math.Abs(Movement.HeadMoveYMM)
        }.Max();
}

public sealed class RecoveryOptimizationResult
{
    public RecoveryCandidateResult Selected { get; set; } = new();
    public List<RecoveryCandidateResult> Candidates { get; set; } = new();
    public string SelectionReason { get; set; } = string.Empty;
}
