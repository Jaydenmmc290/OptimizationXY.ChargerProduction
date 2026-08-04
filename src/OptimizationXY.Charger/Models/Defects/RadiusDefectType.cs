namespace OptimizationXY.Charger.Models.Defects
{
    /// <summary>
    /// Enumeration defining the types of radius defects detected at specific points on a log cross-section.
    ///
    /// Each enum value represents a different classification category for a measured radius point,
    /// ranging from acceptable ("Good") to various severity levels of defects ("LowRadius", "SevereLowRadius", etc.).
    ///
    /// Defect type classification is used to:
    /// - Identify problem areas on the log
    /// - Determine correction strategies (spindle/charger movements)
    /// - Calculate penalty scores during optimization
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
        /// Represents a minor wane or material loss defect that should be corrected.
        /// </summary>
        LowRadius,

        /// <summary>
        /// Indicates the radius is significantly below the acceptable limit.
        /// Represents a severe wane or major material loss defect requiring correction.
        /// This level of defect heavily impacts log quality and yield.
        /// </summary>
        SevereLowRadius,

        /// <summary>
        /// Indicates the radius is above the acceptable limit, but within a moderate tolerance.
        /// Represents over-milling or unexpected excess material.
        /// Generally less problematic than low-radius defects.
        /// </summary>
        HighRadius,

        /// <summary>
        /// Indicates the radius measurement is above acceptable limits but is likely due to
        /// measurement noise rather than an actual defect.
        /// Used to distinguish sensor noise from true defects.
        /// Can be filtered out or given lower weight in scoring.
        /// </summary>
        Noise
    }
}
