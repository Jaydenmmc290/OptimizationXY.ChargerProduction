

namespace OptimizationXY.Charger.Models.Geometry;

/// <summary>
/// Represents a single measurement point in a radius field for a specific cross-section.
///
/// A radius field point stores both the polar coordinates (angle, radius) and Cartesian
/// coordinates (X, Y) of a measurement. The axial position (Z) identifies which cross-section
/// this point belongs to.
///
/// Radius field points are aggregated into RadiusFieldColumns for processing,
/// and form the basis for defect analysis and geometric calculations.
/// </summary>
public class RadiusFieldPoint
{
    /// <summary>
    /// Gets or sets the axial position (Z coordinate) of this measurement point along the log, measured in millimeters.
    ///
    /// Identifies which cross-sectional slice this point belongs to.
    /// Multiple points at the same Z form a complete cross-section.
    /// </summary>
    public double Z { get; set; }

    /// <summary>
    /// Gets or sets the circumferential angle of this measurement point, measured in degrees.
    ///
    /// Range: 0-360 degrees around the log circumference.
    /// 0° typically corresponds to the +X axis (three o'clock position).
    /// Combined with RadiusMM to specify position in polar coordinates.
    /// </summary>
    public double AngleDeg { get; set; }

    /// <summary>
    /// Gets or sets the radial distance from the log centerline to this point, measured in millimeters.
    ///
    /// Represents the actual measured distance from the geometric center (centerline)
    /// to the log surface at this angular and axial position.
    /// This is the primary measurement used for defect analysis.
    /// </summary>
    public double RadiusMM { get; set; }

    /// <summary>
    /// Gets or sets the X coordinate (Cartesian) of this measurement point, measured in millimeters.
    ///
    /// Can be calculated from angle and radius using: X = RadiusMM * cos(AngleDeg)
    /// Stored separately for convenience and efficiency in calculations.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Gets or sets the Y coordinate (Cartesian) of this measurement point, measured in millimeters.
    ///
    /// Can be calculated from angle and radius using: Y = RadiusMM * sin(AngleDeg)
    /// Stored separately for convenience and efficiency in calculations.
    /// </summary>
    public double Y { get; set; }
}
