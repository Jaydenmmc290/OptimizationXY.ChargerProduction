namespace OptimizationXY.Charger.Models.Defects
{
    /// <summary>
    /// Represents a contiguous region or patch of defects on the log surface.
    ///
    /// A patch is a connected cluster of defect points (e.g., all LowRadius points in a region).
    /// Multiple patches across the log comprise the complete RadiusDefectMap.
    /// Patches are useful for:
    /// - Locating defect clusters
    /// - Calculating regional severity
    /// - Optimizing spindle/charger position to address worst patches
    /// - Prioritizing which defects to correct
    /// </summary>
    public class RadiusDefectPatch
    {
        /// <summary>
        /// Gets or sets a unique identifier for this defect patch within the log.
        ///
        /// Used to reference and track individual patches in analysis and optimization.
        /// Typically assigned sequentially (0, 1, 2, ...) as patches are detected.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the classification type of defects in this patch.
        ///
        /// Represents the dominant defect type:
        /// - LowRadius: Wane or material loss
        /// - SevereLowRadius: Severe material loss
        /// - HighRadius: Excess material
        ///
        /// All points in a patch typically share the same defect type.
        /// </summary>
        public RadiusDefectType DefectType { get; set; }

        /// <summary>
        /// Gets or sets the collection of individual defect points that comprise this patch.
        ///
        /// These points define the spatial extent and severity of the defect region.
        /// Initialized to empty list by default.
        /// Use CellCount property to get the point count.
        /// </summary>
        public List<RadiusDefectPoint> Points { get; set; } = new();

        /// <summary>
        /// Gets or sets the minimum axial position (start of patch along log length), measured in millimeters.
        ///
        /// Defines the beginning of the patch's extent along the log axis.
        /// Lower Z value of the patch boundaries.
        /// </summary>
        public double StartZMM { get; set; }

        /// <summary>
        /// Gets or sets the maximum axial position (end of patch along log length), measured in millimeters.
        ///
        /// Defines the end of the patch's extent along the log axis.
        /// Higher Z value of the patch boundaries.
        /// </summary>
        public double EndZMM { get; set; }

        /// <summary>
        /// Gets or sets the starting circumferential angle of the patch, measured in degrees.
        ///
        /// Represents the beginning angular position (0-360) of the patch around
        /// the log circumference. May wrap around (e.g., 350° to 10°).
        /// </summary>
        public double StartAngleDeg { get; set; }

        /// <summary>
        /// Gets or sets the ending circumferential angle of the patch, measured in degrees.
        ///
        /// Represents the ending angular position (0-360) of the patch around
        /// the log circumference.
        /// </summary>
        public double EndAngleDeg { get; set; }

        /// <summary>
        /// Gets or sets the average radius error across all points in this patch, measured in millimeters.
        ///
        /// Calculated as the mean of RadiusErrorMM for all points in the patch.
        /// Provides a quick indicator of typical defect severity in the patch.
        /// Used in penalty calculations and defect scoring.
        /// </summary>
        public double AverageErrorMM { get; set; }

        /// <summary>
        /// Gets or sets the maximum (worst-case) radius error in this patch, measured in millimeters.
        ///
        /// The single largest error value among all points.
        /// Identifies the most severe location within the patch.
        /// Critical for determining spindle positioning that must handle worst-case conditions.
        /// </summary>
        public double WorstErrorMM { get; set; }

        /// <summary>
        /// Gets or sets the computed severity score for this patch.
        ///
        /// Higher values indicate more severe defects requiring more aggressive correction.
        /// Typically calculated from error magnitude, patch size, and defect type.
        /// Used for prioritization and optimization weighting.
        /// </summary>
        public double SeverityScore { get; set; }

        /// <summary>
        /// Gets the number of individual defect points (cells) comprising this patch.
        ///
        /// Returns Points.Count.
        /// Larger cell counts indicate more extensive defect coverage.
        /// </summary>
        public int CellCount => Points.Count;
    }
}
