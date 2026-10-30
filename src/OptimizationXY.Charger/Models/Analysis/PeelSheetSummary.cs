namespace OptimizationXY.Charger.Models.Analysis;

/// <summary>
/// Sheet counts from a virtual peel. The simulated veneer ribbon is cut into
/// sheets of PeelViewSettings.VeneerMarkerSpacingInches (50 inch by default).
/// A "full" sheet is a whole sheet length; the leftover at the end is the
/// partial sheet. A full sheet is usable when its wood coverage is at least
/// PeelViewSettings.MinimumUsableSheetCoveragePercent AND its severe wane is at
/// most MaximumUsableSheetSevereWanePercent; otherwise it is rejected.
/// Lengths are along the ribbon, in millimetres.
/// Counts of full sheets, usable sheets, rejected sheets,
/// and various measurements related to veneer length and coverage.
/// </summary>
public sealed class PeelSheetSummary
{

    /// <summary>
    /// Gets or sets the number of whole sheet lengths in the ribbon (floor of total length / sheet length).
    /// </summary>
    public int TotalFullSheets { get; set; }

    /// <summary>
    /// Gets or sets how many of the full sheets passed the wane rules (usable veneer).
    /// </summary>
    public int UsableFullSheets { get; set; }

    /// <summary>
    /// Gets or sets how many of the full sheets failed the wane rules.
    /// </summary>
    public int RejectedFullSheets { get; set; }

    /// <summary>
    /// Gets or sets the total length of veneer in millimeters across all sheets analyzed.
    /// </summary>
    public double TotalVeneerLengthMM { get; set; }

    /// <summary>
    /// Gets or sets the usable length in millimetres = usable full sheets x sheet length.
    /// </summary>
    public double UsableVeneerLengthMM { get; set; }

    /// <summary>
    /// Gets or sets the length (mm) of the final, shorter-than-a-sheet piece at the end of the ribbon.
    /// </summary>
    public double PartialSheetLengthMM { get; set; }

    /// <summary>
    /// Gets or sets the average percentage of wood (not wane) across the full sheets.
    /// </summary>
    public double AverageUsableCoveragePercent { get; set; }
}
