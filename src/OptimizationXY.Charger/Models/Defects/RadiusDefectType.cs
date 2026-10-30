namespace OptimizationXY.Charger.Models.Defects;

/// <summary>
/// Enumeration defining the types of radius defects detected at specific points on a log cross-section.
///
/// Each enum value represents a different classification category for a measured radius point,
/// ranging from acceptable ("Good") to various severity levels of defects ("LowRadius", "SevereLowRadius", etc.).
///
/// A point is classified by its radius error compared with the median radius of
/// its slice. A defect level needs BOTH the millimetre threshold AND the
/// percentage threshold to be passed (see RadiusDefectMapBuilder.Classify).
/// The millimetre thresholds come from settings (and may be scaled by adaptive
/// scoring); the percentages are fixed in the code (4.5% / 7.5% / 4.5%).
///
/// Defect type classification is used to:
/// - Identify problem areas on the log
/// - Build connected defect patches
/// - Calculate penalty scores in the legacy PenaltyScored optimization mode
/// - Filter noise from real defects
/// </summary>
public enum RadiusDefectType
{
    /// <summary>
    /// Indicates the radius measurement is within acceptable limits.
    /// No defect is present at this location.
    /// </summary>
    Good,

    /// <summary>
    /// Indicates the radius is below the acceptable limit, but within a moderate tolerance.
    /// Represents moderate wane or missing wood: at least DefectSettings.LowRadiusThresholdMM
    /// (default 2 mm) AND 4.5% below the slice median.
    /// </summary>
    LowRadius,

    /// <summary>
    /// Indicates the radius is significantly below the acceptable limit.
    /// Represents severe wane or missing wood: at least DefectSettings.SevereLowRadiusThresholdMM
    /// (default 4 mm) AND 7.5% below the slice median.
    /// This level of defect heavily impacts log quality and yield.
    /// </summary>
    SevereLowRadius,

    /// <summary>
    /// Indicates the radius is above the slice median by at least
    /// DefectSettings.HighRadiusThresholdMM (default 2 mm) AND 4.5%.
    /// Usually bumps, knots, bark or a laser reflection.
    /// Generally less problematic than low-radius defects.
    /// </summary>
    HighRadius,

    /// <summary>
    /// Reserved for measurement noise. Not assigned by the current classifier.
    /// </summary>
    Noise
}
