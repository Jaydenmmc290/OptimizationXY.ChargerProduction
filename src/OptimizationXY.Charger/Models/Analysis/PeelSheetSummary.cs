namespace OptimizationXY.Charger.Models.Analysis;

public sealed class PeelSheetSummary
{
    public int TotalFullSheets { get; set; }
    public int UsableFullSheets { get; set; }
    public int RejectedFullSheets { get; set; }

    public double TotalVeneerLengthMM { get; set; }
    public double UsableVeneerLengthMM { get; set; }
    public double PartialSheetLengthMM { get; set; }
    public double AverageUsableCoveragePercent { get; set; }
}
