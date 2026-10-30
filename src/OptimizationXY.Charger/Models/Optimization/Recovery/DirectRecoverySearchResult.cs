using OptimizationXY.Charger.Models.Optimization.Spindle;


// DirectRecoverySearchResult.cs - result of DirectRecoveryOptimizer.
namespace OptimizationXY.Charger.Models.Optimization.Recovery;

/// <summary>
/// Result of the direct veneer-recovery search (DirectRecoveryOptimizer, used
/// in RecoveryOptimized mode): the best spindle position found by virtually
/// peeling many candidate positions, and that position's peel numbers.
/// </summary>
public sealed class DirectRecoverySearchResult
{

    /// <summary>
    /// Gets or sets the optimized spindle movement resulting from the direct recovery search.
    /// </summary>
    public OptimizedSpindleMovement Movement { get; set; } = new();

    /// <summary>
    /// Gets or sets the recovery search metrics, which provide detailed information about the search process and its performance.
    /// </summary>
    public RecoverySearchMetrics Metrics { get; set; } = new();

    /// <summary>
    /// Gets or sets how many candidate spindle positions were virtually peeled.
    /// </summary>
    public int EvaluatedCandidates { get; set; }

    /// <summary>
    /// Gets or sets how many candidates were invalid (e.g. outside travel limits or the peel could not be computed).
    /// </summary>
    public int RejectedCandidates { get; set; }

    /// <summary>
    /// Gets or sets a one-line text summary of the search for logs and the saved scan.
    /// </summary>
    public string SearchSummary { get; set; } = string.Empty;

}
