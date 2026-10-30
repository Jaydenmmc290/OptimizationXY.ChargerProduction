
// FinalSpindleRecommendation.cs - THE answer for one scan: where to move the spindles.
namespace OptimizationXY.Charger.Models.Optimization.Spindle;

/// <summary>
/// The final answer for one scan: which strategy won, whether the move is
/// allowed, and the head/tail commands that are sent to the PLC
/// (PlcPayloadBuilder converts the Command* values to inches).
/// </summary>
/// <remarks>
/// Sign rule: every optimization mode builds the PLC command as the NEGATIVE
/// of the selected axis position in the point-cloud frame
/// (Command = -SpindleMovement). A command of 0 means "stay on the current
/// axis". Head = end nearest the lathe operator, tail = far end. On this
/// machine +X is toward the V's, -X toward the lathe, +Y down and -Y up;
/// confirm on commissioning that the PLC applies the commands that way.
/// </remarks>
public sealed class FinalSpindleRecommendation
{

    /// <summary>
    /// Gets or sets the optimization mode that produced this recommendation
    /// (production: CompareInscribedGeometry).
    /// </summary>
    public SpindleOptimizationMode OptimizationMode { get; set; }

    /// <summary>
    /// Gets or sets the display name of the strategy that won (e.g. "Largest Inscribed Cylinder"),
    /// shown to the operator and saved with the scan.
    /// </summary>
    public string SelectedStrategy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the optimizer considers this position a real improvement worth using.
    /// </summary>
    public bool IsRecommended { get; set; }

    /// <summary>
    /// Gets or sets whether the move is inside the machine's safety/travel limits.
    /// When false, PlcPayloadBuilder refuses to build a PLC payload.
    /// </summary>
    public bool MachineMoveAllowed { get; set; }


    /// <summary>
    /// Gets or sets a short instruction for the operator screen.
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
    /// Gets or sets the adaptive risk score at the recommended position (legacy
    /// penalty/adaptive scoring; diagnostic in the inscribed-geometry modes).
    /// </summary>
    public double RiskScore { get; set; }

    /// <summary>
    /// Gets or sets the text rating for <see cref="RiskScore"/> (e.g. Low/Medium/High).
    /// </summary>
    public string RiskRating { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the risk score on the CURRENT axis (no move), for comparison.
    /// </summary>
    public double BaselineRiskScore { get; set; }

    /// <summary>
    /// Gets or sets the text rating for <see cref="BaselineRiskScore"/>.
    /// </summary>
    public string BaselineRiskRating { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how much lower (percent) the risk score is than on the current axis.
    /// </summary>
    public double RiskScoreReductionPercent { get; set; }


    /// <summary>
    /// The head/tail X/Y commands sent to the PLC, in MILLIMETRES here (inches in
    /// the PLC payload). Command = -SpindleMovement (see the class remarks).
    /// </summary>
    public double CommandTailXMM { get; set; }
    public double CommandTailYMM { get; set; }
    public double CommandHeadXMM { get; set; }
    public double CommandHeadYMM { get; set; }


    /// <summary>
    /// The best spindle axis position found (point-cloud frame, mm) before it is
    /// turned into a command. Kept for diagnostics.
    /// </summary>
    public double BestTestedTailXMM { get; set; }
    public double BestTestedTailYMM { get; set; }
    public double BestTestedHeadXMM { get; set; }
    public double BestTestedHeadYMM { get; set; }

    /// <summary>
    /// Gets or sets the largest single head/tail move (mm) needed to reach the commanded position.
    /// </summary>
    public double MaxSpindleMovementMM { get; set; }


    /// <summary>
    /// Gets or sets the total number of full sheets produced from the log.
    /// Sheet counts of the virtual peel at the commanded position (see PeelSheetSummary).
    /// </summary>
    public int TotalFullSheets { get; set; }
    public int UsableFullSheets { get; set; }
    public int RejectedFullSheets { get; set; }


    /// <summary>
    /// Gets or sets the usable veneer length in millimeters.
    /// </summary>
    public double UsableVeneerLengthMM { get; set; }

   
    /// <summary>
    /// Inscribed-geometry size at the chosen axis: major and minor diameters (mm;
    /// equal for a cylinder) and the oval's major-axis direction (degrees).
    /// </summary>
    public double InscribedMajorDiameterMM { get; set; }
    public double InscribedMinorDiameterMM { get; set; }
    public double InscribedOrientationDeg { get; set; }

    /// <summary>
    /// Gets or sets the wane score on the current axis (baseline) and at the
    /// chosen axis (optimized). Wane here means missing wood around the turning axis.
    /// </summary>
    public double BaselineWaneScoreMM { get; set; }
    public double OptimizedWaneScoreMM { get; set; }

    /// <summary>
    /// Gets or sets the search's internal score on the current axis and at the chosen
    /// axis (legacy PenaltyScored search; diagnostic in other modes).
    /// </summary>
    public double BaselineInternalSearchScore { get; set; }
    public double OptimizedInternalSearchScore { get; set; }

    /// <summary>Defect-patch penalty on the current axis and at the chosen axis (legacy scoring; diagnostic).</summary>
    public double BaselineDefectPenalty { get; set; }
    public double OptimizedDefectPenalty { get; set; }
    
    /// <summary>
    /// Gets or sets how many candidate positions were evaluated in total and at
    /// each search level (coarse, medium, fine).
    /// </summary>
    public int TestedCandidates { get; set; }
    public int CoarseCandidates { get; set; }
    public int MediumCandidates { get; set; }
    public int FineCandidates { get; set; }
}
