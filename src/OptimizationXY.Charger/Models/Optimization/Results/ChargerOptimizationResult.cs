using OptimizationXY.Charger.Models.Optimization.Spindle;

/// <summary>
/// Represents the result of the charger optimization process, including offsets, scores, penalties, and candidate counts.
/// </summary>
namespace OptimizationXY.Charger.Models.Optimization.Results;

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
    /// Gets or sets a score representing the wane (deviation from ideal shape) of the charger.
    /// </summary>
    public double WaneScore { get; set; }  

    /// <summary>
    /// Gets or sets a penalty score representing the presence of defects in the charger.
    /// </summary>
    public double DefectPenalty { get; set; } 

    /// <summary>
    /// Gets or sets a penalty score representing the amount of movement applied to the charger, which may affect its performance.
    /// </summary>
    public double MovementPenalty { get; set; } 

    /// <summary>
    /// Gets or sets the final score of the optimization, which may be a combination of the wane score and the various penalties.
    /// </summary>
    public double FinalScore { get; set; }  

    /// <summary>
    /// Gets or sets a value indicating whether the optimization result is recommended based on the final score and other criteria.
    /// </summary>
    public bool IsRecommended { get; set; }  

    /// <summary>
    /// Gets or sets the percentage improvement of the optimization result compared to a baseline or previous result.
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
