using OptimizationXY.Charger.Models.Analysis;
using OptimizationXY.Charger.Models.Optimization.Geometry;
using OptimizationXY.Charger.Models.Optimization.Spindle;


// RecoveryCandidateResult.cs - one strategy's answer in RecoveryOptimized mode.
namespace OptimizationXY.Charger.Models.Optimization.Recovery;

/// <summary>
/// One strategy's proposed spindle position in RecoveryOptimized mode
/// (e.g. baseline, penalty, cylinder, oval, direct search), with the virtual
/// peel result for that position. RecoveryStrategySelector picks the best.
/// </summary>
public sealed class RecoveryCandidateResult
{

    /// <summary>
    /// Gets or sets the name of the recovery strategy used for this candidate result.
    /// </summary>
    public string StrategyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optimized spindle movement resulting from the recovery candidate evaluation.
    /// </summary>
    public OptimizedSpindleMovement Movement { get; set; } = new();

    /// <summary>
    /// Gets or sets the peel simulation result, which provides information about the peel behavior and performance of the recovery candidate.
    /// </summary>
    public PeelSimulationResult Peel { get; set; } = new();

    /// <summary>
    /// Gets or sets the inscribed-geometry result when the candidate came from the cylinder or oval search (otherwise null).
    /// </summary>
    public InscribedGeometryResult? Geometry { get; set; }

    /// <summary>
    /// Gets or sets the number of candidates that were searched during the recovery candidate evaluation.
    /// (How many spindle positions that strategy evaluated to produce this one.)
    /// </summary>
    public int CandidateSearchCount { get; set; }

    /// <summary>
    /// Gets the largest absolute single-axis move (tail X, tail Y, head X or head Y), in millimetres.
    /// </summary>

    public double MaxMovementMM =>
        new[]
        {
            Math.Abs(Movement.TailMoveXMM),
            Math.Abs(Movement.TailMoveYMM),
            Math.Abs(Movement.HeadMoveXMM),
            Math.Abs(Movement.HeadMoveYMM)
        }.Max();
}


/// <summary>
/// Result of RecoveryOptimized mode: every strategy's candidate, the selected
/// one, and why it was selected.
/// </summary>
public sealed class RecoveryOptimizationResult
{

    /// <summary>
    /// Gets or sets the selected recovery candidate result, which represents the best recovery strategy chosen from the evaluated candidates.
    /// </summary>
    public RecoveryCandidateResult Selected { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of recovery candidate results, which includes all evaluated
    /// candidates during the recovery optimization process.
    /// </summary>
    public List<RecoveryCandidateResult> Candidates { get; set; } = [];


    /// <summary>
    /// Gets or sets the reason for selecting the best recovery candidate, which provides
    /// insight into why the chosen strategy was preferred over others.
    /// </summary>
    public string SelectionReason { get; set; } = string.Empty;
}
