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
    /// Gets or sets the average diameter of the log, in millimetres.
    /// </summary>
    public double AverageDiameterMM { get; set; }


    /// <summary>
    /// Gets or sets the smallest diameter of the log, in millimetres (sent to the PLC as the small diameter).
    /// </summary>
    public double MinDiameterMM { get; set; }


    /// <summary>
    /// Gets or sets the largest diameter of the log, in millimetres (sent to the PLC as the large diameter).
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
