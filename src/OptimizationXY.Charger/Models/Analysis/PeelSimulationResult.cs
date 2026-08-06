using Emgu.CV;

namespace OptimizationXY.Charger.Models.Analysis;


/// <summary>
/// Represents the result of a peel simulation, including the processed image,
/// log dimensions, wane area percentages, and sheet analysis results.
/// </summary>
public sealed class PeelSimulationResult
{


   //  Gets or sets the processed image resulting from the peel simulation.
    public Mat Image { get; set; } = new();

    //  Gets or sets the number of revolutions made during the peel simulation.
    public int Revolutions { get; set; }

    //  Gets or sets the outer diameter of the log in millimeters.
    public double OuterDiameterMM { get; set; }

    //  Gets or sets the core diameter of the log in millimeters.
    public double CoreDiameterMM { get; set; }

    //  Gets or sets the length of the log in millimeters.
    public double LogLengthMM { get; set; }

    //  Gets or sets the total wane area percentage of the log.
    public double TotalWaneAreaPercent { get; set; }

    //  Gets or sets the severe wane area percentage of the log.
    public double SevereWaneAreaPercent { get; set; }

    //  Gets or sets the summary of peel sheet analysis results, including counts of full sheets, usable sheets, rejected sheets,
    public int TotalFullSheets { get; set; }

    //  Gets or sets the number of usable full sheets after analysis.
    public int UsableFullSheets { get; set; }

    //  Gets or sets the number of rejected full sheets after analysis.
    public int RejectedFullSheets { get; set; }

    //  Gets or sets the total length of veneer in millimeters across all sheets analyzed.
    public double TotalVeneerLengthMM { get; set; }

    //  Gets or sets the total usable length of veneer in millimeters across all sheets analyzed.
    public double UsableVeneerLengthMM { get; set; }

    //  Gets or sets the total length of partial sheets in millimeters across all sheets analyzed.
    public double PartialSheetLengthMM { get; set; }

    //  Gets or sets the average usable coverage percentage across all sheets analyzed.
    public double AverageUsableCoveragePercent { get; set; }
}
