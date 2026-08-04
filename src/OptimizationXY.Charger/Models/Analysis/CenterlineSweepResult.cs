using System;
using System.Collections.Generic;
using System.Text;

namespace OptimizationXY.Charger.Models.Analysis
{
    /// <summary>
    /// Represents the results of a centerline sweep analysis performed on a log.
    ///
    /// This class captures measurements of how much the log's centerline deviates
    /// (sweeps) in different directions along its length. It provides both directional
    /// sweep measurements (X, Y) and aggregate measures (total, average, max).
    ///
    /// Sweep measurements are useful for detecting bow and other longitudinal defects
    /// in the log that affect its straightness and geometric quality.
    /// </summary>
    public class CenterlineSweepResult
    {
        /// <summary>
        /// Gets or sets the maximum centerline sweep magnitude in the X direction, measured in millimeters.
        ///
        /// This represents the absolute maximum deviation of the centerline from its nominal
        /// position when viewed from the X-axis perspective (typically side view).
        /// Higher values indicate more pronounced curvature in the X direction.
        /// </summary>
        public double XSweepMM { get; set; }

        /// <summary>
        /// Gets or sets the maximum centerline sweep magnitude in the Y direction, measured in millimeters.
        ///
        /// This represents the absolute maximum deviation of the centerline from its nominal
        /// position when viewed from the Y-axis perspective (typically front view).
        /// Higher values indicate more pronounced curvature in the Y direction.
        /// </summary>
        public double YSweepMM { get; set; }

        /// <summary>
        /// Gets or sets the total combined sweep, calculated as the vector magnitude of XSweepMM and YSweepMM.
        ///
        /// Formula: sqrt(XSweepMM² + YSweepMM²)
        /// This provides a single value representing the overall straightness quality of the log.
        /// Measured in millimeters.
        /// </summary>
        public double TotalSweepMM { get; set; }

        /// <summary>
        /// Gets or sets the X coordinate of the log's tail end centerline position, measured in millimeters.
        ///
        /// The tail is typically one end of the log. This coordinate represents the X position
        /// of the centerline at that end.
        /// </summary>
        public double TailCenterXMM { get; set; }

        /// <summary>
        /// Gets or sets the Y coordinate of the log's tail end centerline position, measured in millimeters.
        ///
        /// The tail is typically one end of the log. This coordinate represents the Y position
        /// of the centerline at that end.
        /// </summary>
        public double TailCenterYMM { get; set; }

        /// <summary>
        /// Gets or sets the X coordinate of the log's head end centerline position, measured in millimeters.
        ///
        /// The head is typically the other end of the log. This coordinate represents the X position
        /// of the centerline at that end.
        /// </summary>
        public double HeadCenterXMM { get; set; }

        /// <summary>
        /// Gets or sets the Y coordinate of the log's head end centerline position, measured in millimeters.
        ///
        /// The head is typically the other end of the log. This coordinate represents the Y position
        /// of the centerline at that end.
        /// </summary>
        public double HeadCenterYMM { get; set; }

        /// <summary>
        /// Gets or sets the maximum bow sweep deviation detected along the log's length, measured in millimeters.
        ///
        /// Bow refers to the curvature of the log. This value represents the worst-case
        /// deviation from a straight line connecting the tail and head centerline positions.
        /// </summary>
        public double MaxBowSweepMM { get; set; }

        /// <summary>
        /// Gets or sets the average bow sweep deviation calculated across all measurement points, measured in millimeters.
        ///
        /// This is computed as the mean of all bow deviations sampled along the log's length,
        /// providing an overall assessment of average curvature severity.
        /// </summary>
        public double AverageBowSweepMM { get; set; }

        /// <summary>
        /// Gets or sets the worst bow Z-axis deviation value, measured in millimeters.
        ///
        /// This represents the most severe deviation in the Z direction at any point
        /// along the log when measuring bow. Useful for identifying critical bow locations.
        /// </summary>
        public double WorstBowZMM { get; set; }
    }
}
