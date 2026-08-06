namespace OptimizationXY.Charger.Models.Analysis;

/// <summary>
/// Represents a summary of peel sheet analysis results, including counts of full sheets, usable sheets, rejected sheets,
/// and various measurements related to veneer length and coverage.
/// </summary>
public sealed class PeelSheetSummary
{

    /// <summary>
    /// Gets or sets the total number of full sheets analyzed.
    /// </summary>
    public int TotalFullSheets { get; set; }

    /// <summary>
    /// Gets or sets the number of usable full sheets after analysis.
    /// </summary>
    public int UsableFullSheets { get; set; }

    /// <summary>
    /// Gets or sets the number of rejected full sheets after analysis.
    /// </summary>
    public int RejectedFullSheets { get; set; }

    /// <summary>
    /// Gets or sets the total length of veneer in millimeters across all sheets analyzed.
    /// </summary>
    public double TotalVeneerLengthMM { get; set; }

    /// <summary>
    /// Gets or sets the total usable length of veneer in millimeters across all sheets analyzed.
    /// </summary>
    public double UsableVeneerLengthMM { get; set; }

    /// <summary>
    /// Gets or sets the total length of partial sheets in millimeters across all sheets analyzed.
    /// </summary>
    public double PartialSheetLengthMM { get; set; }

    /// <summary>
    /// Gets or sets the average usable coverage percentage across all sheets analyzed.
    /// </summary>
    public double AverageUsableCoveragePercent { get; set; }
}
