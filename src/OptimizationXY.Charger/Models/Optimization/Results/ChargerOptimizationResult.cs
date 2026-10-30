using OptimizationXY.Charger.Models.Optimization.Spindle;

// ChargerOptimizationResult.cs - result of the legacy penalty-scored search (ChargerOptimizer).
namespace OptimizationXY.Charger.Models.Optimization.Results;

/// <summary>
/// Result of the legacy penalty-scored search (ChargerOptimizer, PenaltyScored
/// mode): the best offset of the log centre, its scores and how many candidate
/// positions were tried. Lower scores/penalties are better.
/// </summary>
public class ChargerOptimizationResult  
{

    /// <summary>
    /// Gets or sets the offset in millimeters along the X-axis for the optimized charger position.
    /// </summary>
    public double OffsetXMM { get; set; }  
    /// <summary>
    /// Gets or sets the offset in millimeters along the Y-axis for the optimized charger position.
    /// </summary>
    public double OffsetYMM { get; set; }  

    /// <summary>
    /// Gets or sets the wane score of the log around the chosen axis (how much of the surface falls inside the target radius).
    /// </summary>
    public double WaneScore { get; set; }  

    /// <summary>
    /// Gets or sets the defect-patch penalty of the log around the chosen axis.
    /// </summary>
    public double DefectPenalty { get; set; } 

    /// <summary>
    /// Gets or sets the penalty for how far the spindles must move (bigger moves cost more).
    /// </summary>
    public double MovementPenalty { get; set; } 

    /// <summary>
    /// Gets or sets the combined score (wane + penalties, weighted by the scoring settings). Lowest wins.
    /// </summary>
    public double FinalScore { get; set; }  

    /// <summary>
    /// Gets or sets a value indicating whether the optimization result is recommended based on the final score and other criteria.
    /// </summary>
    public bool IsRecommended { get; set; }  

    /// <summary>
    /// Gets or sets the percentage improvement of the final score versus not moving (the current axis).
    /// </summary>
    public double ImprovementPercent { get; set; }  

    /// <summary>
    /// Gets or sets the number of coarse candidates considered during the optimization process.
    /// </summary>
    public int CoarseCandidates { get; set; }

    /// <summary>
    /// Gets or sets the number of medium candidates considered during the optimization process.
    /// </summary>
    public int MediumCandidates { get; set; }
    
    /// <summary>
    /// Gets or sets the number of fine candidates considered during the optimization process.
    /// </summary>
    public int FineCandidates { get; set; }

    /// <summary>
    /// Gets or sets the optimized spindle movement resulting from the charger optimization process.
    /// </summary>
    public OptimizedSpindleMovement? SpindleMovement { get; set; }

    /// <summary>
    /// Gets or sets the number of candidate solutions that were tested during the optimization process.
    /// </summary>
    public int TestedCandidates { get; set; }  

}
