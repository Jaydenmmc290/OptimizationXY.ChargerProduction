namespace OptimizationXY.Charger.Models.Geometry;

/// <summary>
/// Represents a binned/aggregated radius measurement for a specific location on the log.
///
/// Binning groups individual radius measurements into discrete cells based on axial position (Z)
/// and circumferential angle. This cell contains statistical measures (mean, median, min, max,
/// standard deviation) of all measurement points that fall within this bin.
///
/// Binned data provides:
/// - Noise reduction through statistical aggregation
/// - More efficient processing (fewer points to analyze)
/// - Reliability metrics (standard deviation, count)
/// - Foundation for defect detection algorithms
/// </summary>
public class BinnedRadiusFieldPoint
{
    /// <summary>
    /// Gets or sets the axial position (Z coordinate) bin location, measured in millimeters.
    ///
    /// Identifies which longitudinal section along the log this bin represents.
    /// Bin width determines the axial resolution of the analysis.
    /// </summary>
    public double ZBinMM { get; set; }

    /// <summary>
    /// Gets or sets the angular position bin location, measured in degrees.
    ///
    /// Range: 0-360 degrees around the log circumference.
    /// Bin width determines the circumferential resolution of the analysis.
    /// </summary>
    public double AngleBinDeg { get; set; }

    /// <summary>
    /// Gets or sets the mean (average) radius of all points in this bin, measured in millimeters.
    ///
    /// The primary metric for this bin's radius. Represents the central tendency
    /// of all measurements at this location. Used as the reference for error calculations.
    /// </summary>
    public double MeanRadiusMM { get; set; }

    /// <summary>
    /// Gets or sets the median radius of all points in this bin, measured in millimeters.
    ///
    /// The middle value when all radius measurements are sorted.
    /// Provides a robust central tendency measure less affected by outliers.
    /// Useful for detecting measurement noise or bimodal distributions.
    /// </summary>
    public double MedianRadiusMM { get; set; }

    /// <summary>
    /// Gets or sets the minimum radius measured in this bin, measured in millimeters.
    ///
    /// The smallest value among all points in the bin.
    /// Useful for identifying worst-case local defects.
    /// Important for determining if bin represents severe wane.
    /// </summary>
    public double MinRadiusMM { get; set; }

    /// <summary>
    /// Gets or sets the maximum radius measured in this bin, measured in millimeters.
    ///
    /// The largest value among all points in the bin.
    /// Indicates best local radius condition within the bin.
    /// Range (Max - Min) gives measure of radius variation.
    /// </summary>
    public double MaxRadiusMM { get; set; }

    /// <summary>
    /// Gets or sets the standard deviation of radius values in this bin, measured in millimeters.
    ///
    /// Quantifies the spread or variability of measurements around the mean.
    /// High standard deviation indicates inconsistent measurements (potentially noise).
    /// Low standard deviation indicates consistent, reliable measurements.
    /// Used to assess measurement confidence and reliability.
    /// </summary>
    public double StdDevRadiusMM { get; set; }

    /// <summary>
    /// Gets or sets the number of individual measurement points in this bin.
    ///
    /// Higher counts provide greater statistical confidence in the mean and standard deviation.
    /// Low counts (1-2 points) indicate sparse data requiring caution in interpretation.
    /// Zero count indicates an empty bin with no data.
    /// Used as a reliability indicator for this bin's measurements.
    /// </summary>
    public int Count { get; set; }
}
