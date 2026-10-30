using OptimizationXY.Charger.Models.Analysis;

// LogGeometryModel.cs - summary size numbers for the scanned log.
namespace OptimizationXY.Charger.Models.Optimization.Setup;

/// <summary>
/// Summary size numbers for the scanned log (PipelineState.Model): average,
/// minimum and maximum diameter, wane score and the ovality result. The min
/// and max diameters are sent to the PLC (in inches) as small/large diameter.
/// </summary>
public class LogGeometryModel 
{
    /// <summary>
    /// Gets or sets 2 x the average radius from the fitted centreline, in millimetres.
    /// </summary>
    public double AverageDiameterMM { get; set; }


    /// <summary>
    /// Gets or sets 2 x the SMALLEST radius measured from the fitted centreline anywhere
    /// on the log, in millimetres (sent to the PLC, in inches, as the small diameter).
    /// This is a radius-based figure, not a measured cross-section width.
    /// </summary>
    public double MinDiameterMM { get; set; }


    /// <summary>
    /// Gets or sets 2 x the LARGEST radius measured from the fitted centreline, in
    /// millimetres (sent to the PLC, in inches, as the large diameter).
    /// </summary>
    public double MaxDiameterMM { get; set; }


    /// <summary>
    /// Gets or sets the wane score of the log: a measure of how much of the surface
    /// falls short of a full round (missing wood), not bark or sapwood.
    /// </summary>
    public double WaneScore { get; set; }  
    
    /// <summary>
    /// Gets or sets the results of an ovality analysis, which assesses the deviation of the log's cross-section
    /// from a perfect circle and may indicate defects or irregularities in the log's shape.
    /// </summary>
    public OvalityResult? OvalityAnalysis { get; internal set; }  
}
