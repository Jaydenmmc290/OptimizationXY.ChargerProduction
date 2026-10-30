
// PeelViewSettings.cs - virtual peel (simulated lathe) settings.
namespace OptimizationXY.Charger.Models.Settings;

/// <summary>
/// Settings for the virtual peel. The scanned log is "peeled" around a chosen
/// spindle axis and the veneer ribbon is unrolled into a flat picture split
/// into sheets. The SAME rules decide how many usable sheets each candidate
/// axis gives, so these settings affect the production position choice, not
/// only the picture.
/// </summary>
public sealed class PeelViewSettings
{

    /// <summary>
    /// Picture only: scales the gradual darkening of the wood colour with peel
    /// depth in the drawn ribbon. Does not affect any measurement or sheet count.
    /// </summary>
    public double VeneerLengthPerRevolutionInches { get; set; } = 0.5;


    /// <summary>
    /// Sheet length in inches (50 by default). The ribbon is cut into sheets of
    /// this length for the usable/rejected sheet counts and drawn sheet markers.
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
    /// Number of cells along the log length in the peel grid (also the picture height).
    /// </summary>
    public int LengthBins { get; set; } = 520;

    /// <summary>
    /// Number of cells around the log in the peel grid.
    /// </summary>
    public int AngleBins { get; set; } = 360;

    /// <summary>
    /// Picture scaling (pixels per mm of ribbon). Auto-reduced if the image gets too wide.
    /// </summary>
    public double PixelsPerMM { get; set; } = 2.0;

    /// <summary>Picture margins (pixels) around the drawn ribbon.</summary>
    public int TopMargin { get; set; } = 95;
    public int LeftMargin { get; set; } = 95;
    public int RightMargin { get; set; } = 80;
    public int BottomMargin { get; set; } = 85;

           
    /// <summary>Picture only: highlight low spots deeper than this (mm) in the first LowSpotVisualMaxRevolutions revolutions.</summary>
    public double LowSpotVisualWarningMM { get; set; } = 6.0;
    public int LowSpotVisualMaxRevolutions { get; set; } = 4;



    /// <summary>
    /// The percentile (as a fraction, 0.98 = 98th) of all radii used as the log's
    /// outer radius, i.e. where the simulated peel starts.
    /// This helps to ignore outliers and noise in the scan data when determining the log's shape.
    /// </summary>
    public double OuterRadiusPercentile { get; set; } = 0.98;

         /// <summary>
         /// Number of smoothing passes over the surface grid before peeling (reduces single-cell noise).
         /// </summary>
         public int SurfaceSmoothPasses { get; set; } = 1;

         /// <summary>
         /// When enabled, the saved PICTURE is cut off after the first completely usable
         /// sheet (100% coverage, no wane) to keep the file small. The numbers (total sheets, final
         /// diameter, etc.) are always computed from the complete simulated peel.
         /// </summary>
         public bool EnableTruncatedVisualization { get; set; } = true;
    }
