namespace OptimizationXY.Charger.Models.Optimization.Recovery;

/// <summary>
/// Lightweight veneer-recovery result used while searching spindle positions.
/// It intentionally contains no display image so hundreds of candidates can be
/// evaluated without allocating large OpenCV canvases.
/// </summary>
public sealed class RecoverySearchMetrics
{
    public bool IsValid { get; set; }
    public string InvalidReason { get; set; } = string.Empty;

    public int Revolutions { get; set; }
    public int TotalFullSheets { get; set; }
    public int UsableFullSheets { get; set; }
    public int RejectedFullSheets { get; set; }

    public double OuterDiameterMM { get; set; }
    public double CoreDiameterMM { get; set; }
    public double TotalVeneerLengthMM { get; set; }
    public double UsableVeneerLengthMM { get; set; }
    public double TotalWaneAreaPercent { get; set; }
    public double SevereWaneAreaPercent { get; set; }
    public double AverageUsableCoveragePercent { get; set; }
}
