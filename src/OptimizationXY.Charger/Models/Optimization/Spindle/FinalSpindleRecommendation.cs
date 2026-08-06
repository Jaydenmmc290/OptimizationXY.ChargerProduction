
/// <summary>
/// Represents the final recommendation for spindle optimization, including the selected strategy, risk assessment, and machine command details.
/// </summary>
namespace OptimizationXY.Charger.Models.Optimization.Spindle;

public sealed class FinalSpindleRecommendation
{

    /// <summary>
    /// Gets or sets the optimization mode used for spindle optimization, which may
    /// indicate the specific approach or algorithm applied to achieve the recommended spindle position.
    /// </summary>
    public SpindleOptimizationMode OptimizationMode { get; set; }

    /// <summary>
    /// Gets or sets the selected strategy for spindle optimization, which may
    /// indicate the specific approach or algorithm applied to achieve the recommended spindle position.
    /// </summary>
    public string SelectedStrategy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the recommended spindle position is
    /// considered safe and effective for the log being processed.
    /// </summary>
    public bool IsRecommended { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the machine is allowed to move to the recommended spindle position,
    /// which may depend on safety considerations, operational constraints, or other factors.
    /// </summary>
    public bool MachineMoveAllowed { get; set; }


    /// <summary>
    /// Gets or sets the recommended action for the operator, which may include instructions for adjusting the spindle position,
    /// modifying the log orientation, or taking other steps to optimize the cutting process.
    /// </summary>
    public string RecommendedAction { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the reason for the recommendation, which may provide context or
    /// justification for the selected strategy and its expected outcomes.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the list of warnings associated with the spindle recommendation, which may provide additional context or cautionary information for the operator.
    /// </summary>
    public List<string> Warnings { get; set; } = new();


    /// <summary>
    /// Gets or sets the risk score associated with the recommended spindle position, which may
    /// indicate the potential for defects, waste, or other negative outcomes during the cutting process.
    /// </summary>
    public double RiskScore { get; set; }

    /// <summary>
    /// Gets or sets the risk rating associated with the recommended spindle position, which may
    /// </summary>
    public string RiskRating { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the baseline risk score for the log before optimization, which may
    /// </summary>
    public double BaselineRiskScore { get; set; }

    /// <summary>
    /// Gets or sets the baseline risk rating for the log before optimization, which may
    /// </summary>
    public string BaselineRiskRating { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the percentage reduction in risk score achieved through optimization, which may
    /// </summary>
    public double RiskScoreReductionPercent { get; set; }


    /// <summary>
    /// Gets or sets the machine command details for the recommended spindle position,
    /// including the coordinates of the tail and head of the log in millimeters.
    /// </summary>
    public double CommandTailXMM { get; set; }
    public double CommandTailYMM { get; set; }
    public double CommandHeadXMM { get; set; }
    public double CommandHeadYMM { get; set; }


    /// <summary>
    /// Gets or sets the best tested spindle position during optimization, including the coordinates of the tail and head of the log in millimeters.
    /// </summary>
    public double BestTestedTailXMM { get; set; }
    public double BestTestedTailYMM { get; set; }
    public double BestTestedHeadXMM { get; set; }
    public double BestTestedHeadYMM { get; set; }

    /// <summary>
    /// Gets or sets the maximum spindle movement in millimeters, which may indicate the extent
    /// of adjustment required to achieve the recommended spindle position.
    /// </summary>
    public double MaxSpindleMovementMM { get; set; }


    /// <summary>
    /// Gets or sets the total number of full sheets produced from the log.
    /// Usable, rejected, and total full sheets are tracked to assess the efficiency and quality of the cutting process.
    /// </summary>
    public int TotalFullSheets { get; set; }
    public int UsableFullSheets { get; set; }
    public int RejectedFullSheets { get; set; }


    /// <summary>
    /// Gets or sets the usable veneer length in millimeters.
    /// </summary>
    public double UsableVeneerLengthMM { get; set; }

   
    public double InscribedMajorDiameterMM { get; set; }
    public double InscribedMinorDiameterMM { get; set; }
    public double InscribedOrientationDeg { get; set; }

    /// <summary>
    /// Gets or sets the baseline and optimized wane scores in millimeters, which may indicate the extent
    /// of wane (the amount of bark or sapwood present) and its potential impact on the log's quality and value.
    /// </summary>
    public double BaselineWaneScoreMM { get; set; }
    public double OptimizedWaneScoreMM { get; set; }

    /// <summary>
    /// Gets or sets the baseline and optimized internal search scores, which may indicate the effectiveness
    /// of the internal search algorithm in identifying optimal spindle positions.
    /// </summary>
    public double BaselineInternalSearchScore { get; set; }
    public double OptimizedInternalSearchScore { get; set; }

    public double BaselineDefectPenalty { get; set; }
    public double OptimizedDefectPenalty { get; set; }
    
    /// <summary>
    /// Gets or sets the number of candidates tested during optimization, which may indicate the thoroughness
    /// of the search process.
    /// </summary>
    public int TestedCandidates { get; set; }
    public int CoarseCandidates { get; set; }
    public int MediumCandidates { get; set; }
    public int FineCandidates { get; set; }
}
