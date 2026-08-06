namespace OptimizationXY.Charger.Models.Optimization.Recovery;

/// <summary>
/// Lightweight veneer-recovery result used while searching spindle positions.
/// It intentionally contains no display image so hundreds of candidates can be
/// evaluated without allocating large OpenCV canvases.
/// </summary>
public sealed class RecoverySearchMetrics
{

    /// <summary>
    /// Gets or sets a value indicating whether the recovery candidate is valid based on the evaluation criteria.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the reason for invalidity if the recovery candidate is not valid.
    /// </summary>
    public string InvalidReason { get; set; } = string.Empty;


    /// <summary>
    /// Gets or sets the number of revolutions required for the recovery candidate.
    /// </summary>
    public int Revolutions { get; set; }

    /// <summary>
    /// Gets or sets the total number of full sheets produced by the recovery candidate.
    /// </summary>
    public int TotalFullSheets { get; set; }

    /// <summary>
    /// Gets or sets the number of usable full sheets produced by the recovery candidate.
    /// </summary>
    public int UsableFullSheets { get; set; }

    /// <summary>
    /// Gets or sets the number of rejected full sheets produced by the recovery candidate.
    /// </summary>
    public int RejectedFullSheets { get; set; }

    /// <summary>
    /// Gets or sets the outer diameter of the recovery candidate in millimeters.
    /// </summary>
    public double OuterDiameterMM { get; set; }

    /// <summary>
    /// Gets or sets the core diameter of the recovery candidate in millimeters.
    /// </summary>
    public double CoreDiameterMM { get; set; }

    /// <summary>
    /// Gets or sets the total length of veneer produced by the recovery candidate in millimeters.
    /// </summary>
    public double TotalVeneerLengthMM { get; set; }

    /// <summary>
    /// Gets or sets the usable length of veneer produced by the recovery candidate in millimeters.
    /// </summary>
    public double UsableVeneerLengthMM { get; set; }

    /// <summary>
    /// Gets or sets the total wane area percentage of the recovery candidate, which indicates the proportion
    /// of the veneer area that is affected by wane (defects or irregularities) relative to the total veneer area.
    /// </summary>
    public double TotalWaneAreaPercent { get; set; }

    /// <summary>
    /// Gets or sets the severe wane area percentage of the recovery candidate, which indicates the proportion
    /// of the veneer area that is severely affected by wane (defects or irregularities) relative to the total veneer area.
    /// </summary>
    public double SevereWaneAreaPercent { get; set; }

    /// <summary>
    /// Gets or sets the average usable coverage percentage of the recovery candidate, which indicates the proportion
    /// </summary>
    public double AverageUsableCoveragePercent { get; set; }
}
