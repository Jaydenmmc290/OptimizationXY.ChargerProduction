
/// <summary>
/// Settings for the peel-view display, which shows a simulated 50-inch sheet of veneer
/// unrolled from the scanned log. The display is used to evaluate log quality and to
/// help the operator position the log for optimal veneer yield.
/// </summary>
namespace OptimizationXY.Charger.Models.Settings;

public sealed class PeelViewSettings
{

    /// <summary>
    /// The length of veneer unrolled per revolution of the log. This is used to
    /// calculate the number of revolutions needed to produce a 50-inch sheet of veneer.
    /// </summary>
    public double VeneerLengthPerRevolutionInches { get; set; } = 0.5;


    /// <summary>
    /// The spacing of markers along the simulated veneer sheet. These markers help the operator
    /// evaluate the veneer sheet and position the log for optimal yield.
    /// </summary>
    public double VeneerMarkerSpacingInches { get; set; } = 50.0;

    /// <summary>
    /// A full 50-inch sheet is usable when at least this percentage of its
    /// simulated cells contain wood.
    /// </summary>
    public double MinimumUsableSheetCoveragePercent { get; set; } = 98.0;

    /// <summary>
    /// Maximum severe-wane cell percentage allowed in a usable sheet.
    /// </summary>
    public double MaximumUsableSheetSevereWanePercent { get; set; } = 0.5;

    /// <summary>
    /// Vertical resolution along log length.
    /// </summary>
    public int LengthBins { get; set; } = 520;

    /// <summary>
    /// Horizontal resolution around log circumference.
    /// </summary>
    public int AngleBins { get; set; } = 360;

    /// <summary>
    /// Display scaling. Auto-reduced if image gets too wide.
    /// </summary>
    public double PixelsPerMM { get; set; } = 2.0;

    public int TopMargin { get; set; } = 95;
    public int LeftMargin { get; set; } = 95;
    public int RightMargin { get; set; } = 80;
    public int BottomMargin { get; set; } = 85;

           
    public double LowSpotVisualWarningMM { get; set; } = 6.0;
    public int LowSpotVisualMaxRevolutions { get; set; } = 4;



    /// <summary>
    /// The percentile of the log's outer radius used to define the log's outer surface.
    /// This helps to ignore outliers and noise in the scan data when determining the log's shape.
    /// </summary>
    public double OuterRadiusPercentile { get; set; } = 0.98;

         /// <summary>
         /// Smoothing for noisy scan cells.
         /// </summary>
         public int SurfaceSmoothPasses { get; set; } = 1;

         /// <summary>
         /// When enabled, truncates the peel visualization to show only from the cut beginning
         /// through the first completely usable sheet (100% coverage, no wane). This reduces file size
         /// while the mathematical calculations (total sheets, final diameter, etc.) are computed based
         /// on the complete peel simulation.
         /// </summary>
         public bool EnableTruncatedVisualization { get; set; } = true;
    }
