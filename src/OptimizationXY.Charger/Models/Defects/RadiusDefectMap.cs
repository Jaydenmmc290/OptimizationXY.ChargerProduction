namespace OptimizationXY.Charger.Models.Defects;

/// <summary>
/// Represents a complete map of all radius defects detected across an entire log.
///
/// This class aggregates all defect points from all cross-sections and axial positions,
/// providing a comprehensive view of the log's defect distribution. It also provides
/// summary statistics including min/max errors and counts of each defect type.
///
/// The defect map is essential for:
/// - Understanding overall log quality
/// - Feeding input to the optimization algorithms
/// - Calculating penalty scores
/// - Visualizing defect distribution
/// </summary>
public class RadiusDefectMap
{
    /// <summary>
    /// Gets or sets the complete collection of all defect points across the entire log.
    ///
    /// Each point represents a single measurement location on the log surface with
    /// associated error and defect type classification.
    /// Initialized to empty list by default.
    /// Points are typically organized spatially but stored in a flat list.
    /// </summary>
    public List<RadiusDefectPoint> Points { get; set; } = [];

    /// <summary>
    /// Gets the minimum radius error observed across all points in the map, measured in millimeters.
    ///
    /// Returns 0 if no points are present.
    /// Negative values indicate material loss (wane).
    /// Useful for understanding the best-case radius condition.
    /// </summary>
    public double MinErrorMM => Points.Count == 0 ? 0 : Points.Min(p => p.RadiusErrorMM);

    

    /// <summary>
    /// Gets the total count of points classified as "Good" (within acceptable limits).
    ///
    /// Higher values indicate better overall log quality.
    /// Useful for computing quality percentage metrics.
    /// </summary>
    public int GoodCount => Points.Count(p => p.DefectType == RadiusDefectType.Good);

    /// <summary>
    /// Gets the total count of points classified as "LowRadius" (minor material deficiency).
    ///
    /// These points represent wane or material loss but not at severe levels.
    /// Should be minimized but represents recoverable defects.
    /// </summary>
    public int LowCount => Points.Count(p => p.DefectType == RadiusDefectType.LowRadius);

    /// <summary>
    /// Gets the total count of points classified as "SevereLowRadius" (severe material deficiency).
    ///
    /// These points represent severe wane or major material loss.
    /// Primary focus of optimization efforts to minimize severity impact.
    /// Log must be rejected if SevereLowCount exceeds quality thresholds.
    /// </summary>
    public int SevereLowCount => Points.Count(p => p.DefectType == RadiusDefectType.SevereLowRadius);

    /// <summary>
    /// Gets the total count of points classified as "HighRadius" (excess material).
    ///
    /// These points represent measurements where radius exceeds expected value.
    /// Generally less problematic than low-radius defects.
    /// May represent under-milling or deviations from expected shape.
    /// </summary>
    public int HighCount => Points.Count(p => p.DefectType == RadiusDefectType.HighRadius);
}
