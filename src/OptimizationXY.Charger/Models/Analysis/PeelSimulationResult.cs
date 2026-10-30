using Emgu.CV;

namespace OptimizationXY.Charger.Models.Analysis;


/// <summary>
/// Result of one virtual peel: the log is "turned" on the lathe around one
/// fixed spindle axis (head X/Y and tail X/Y) and peeled down to the core,
/// and the result records how much good veneer and wane that axis would give.
/// The optimizers compare these results to choose the spindle position.
/// Includes the (optional) picture,
/// log dimensions, wane area percentages, and sheet analysis results.
/// </summary>
public sealed class PeelSimulationResult
{


   // The unwrapped-ribbon picture of the peel.
   //  Empty when the result came from UnwrappedPeelViewBuilder.ComputeMetrics
   //  (numbers only); the image is then drawn later by DeferredPeelViewRenderer.
    public Mat Image { get; set; } = new();

    // Spindle axis (head/tail X/Y, mm, point-cloud frame) this peel was
    // simulated for. Lets the image be drawn later for exactly the same axis.
    public double AxisTailXMM { get; set; }
    public double AxisTailYMM { get; set; }
    public double AxisHeadXMM { get; set; }
    public double AxisHeadYMM { get; set; }

    // Number of lathe revolutions simulated from the outer diameter down to the core.
    public int Revolutions { get; set; }

    // Diameter (mm) at which the first veneer is cut around this axis.
    public double OuterDiameterMM { get; set; }

    // Diameter (mm) of the core left when peeling stops.
    public double CoreDiameterMM { get; set; }

    // Block length (mm) used for the simulation.
    public double LogLengthMM { get; set; }

    // Percentage of the peeled ribbon area that is wane (no wood / bark gap).
    public double TotalWaneAreaPercent { get; set; }

    // Percentage of the peeled ribbon area that is SEVERE wane.
    public double SevereWaneAreaPercent { get; set; }

    // Sheet counts copied from the PeelSheetSummary (see that class for definitions):
    // total number of whole sheets in the ribbon.
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
