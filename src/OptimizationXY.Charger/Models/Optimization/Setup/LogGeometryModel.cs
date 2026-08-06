using OptimizationXY.Charger.Models.Analysis;

/// <summary>
/// Represents the geometric properties of a log, including measurements and analysis results related to its shape and dimensions.
/// </summary>
namespace OptimizationXY.Charger.Models.Optimization.Setup;

public class LogGeometryModel 
{
    /// <summary>
    /// Gets or sets the average diameter of the log in millimeters, which may be used to assess its overall size and potential growth patterns.
    /// </summary>
    public double AverageDiameterMM { get; set; }


    /// <summary>
    /// Gets or sets the minimum diameter of the log in millimeters, which may indicate the narrowest point of the log and potential defects or irregularities in its shape.
    /// </summary>
    public double MinDiameterMM { get; set; }


    /// <summary>
    /// Gets or sets the maximum diameter of the log in millimeters, which may indicate
    /// the widest point of the log and potential defects or irregularities in its shape.
    /// </summary>
    public double MaxDiameterMM { get; set; }


    /// <summary>
    /// Gets or sets the wane score of the log, which may be used to assess the extent of wane
    /// (the amount of bark or sapwood present) and its potential impact on the log's quality and value.
    /// </summary>
    public double WaneScore { get; set; }  
    
    /// <summary>
    /// Gets or sets the results of an ovality analysis, which assesses the deviation of the log's cross-section
    /// from a perfect circle and may indicate defects or irregularities in the log's shape.
    /// </summary>
    public OvalityResult? OvalityAnalysis { get; internal set; }  
}
