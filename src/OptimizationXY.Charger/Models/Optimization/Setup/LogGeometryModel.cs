using OptimizationXY.Charger.Models.Analysis;
using OptimizationXY.Charger.Models.Geometry;

namespace OptimizationXY.Charger.Models.Optimization.Setup;

public class LogGeometryModel // This class represents the geometric properties of a log, including measurements
                              // and analysis results related to its shape and dimensions.
{

    public double AverageDiameterMM { get; set; }  // The average diameter of the log in millimeters, calculated from the profile measurements.

    public double MinDiameterMM { get; set; }  // The minimum diameter of the log in millimeters, which may indicate the presence of
                                               // defects or irregularities in the log's shape.

    public double MaxDiameterMM { get; set; }  // The maximum diameter of the log in millimeters, which may indicate the overall size and potential growth patterns of the log.

    public double MinDiameterInches => MinDiameterMM / 25.4;
    public double AverageDiameterInches => AverageDiameterMM / 25.4;
    public double MaxDiameterInches => MaxDiameterMM / 25.4;

    public double WaneScore { get; set; }  // A score representing the wane (deviation from ideal shape) of the log, which may be used to assess
                                           // its quality and suitability for processing.

    public OvalityResult? OvalityAnalysis { get; internal set; }  // A property representing the results of an ovality analysis, which assesses the deviation of the log's cross-section
                                                                 // from a perfect circle and may indicate defects or irregularities in the log's shape.
}
