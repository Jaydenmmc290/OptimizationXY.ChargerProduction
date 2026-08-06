using OptimizationXY.Charger.Models.Optimization.Spindle;


/// <summary>
/// Represents the result of a direct recovery search, which includes the
/// optimized spindle movement, recovery search metrics, and other relevant information.
/// </summary>
namespace OptimizationXY.Charger.Models.Optimization.Recovery;

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
    /// Gets or sets the summary of the recovery search, which may include additional information or insights about the search process and its results.
    /// </summary>
    public int EvaluatedCandidates { get; set; }

    /// <summary>
    /// Gets or sets the number of rejected candidates during the recovery search, which indicates how many potential solutions were considered but ultimately not selected.
    /// </summary>
    public int RejectedCandidates { get; set; }

    /// <summary>
    /// Gets or sets the summary of the recovery search, which may include additional information or insights about the search process and its results.
    /// </summary>
    public string SearchSummary { get; set; } = string.Empty;

}
