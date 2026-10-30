

namespace OptimizationXY.Charger.Models.Geometry;

/// <summary>
/// Represents a single measurement point in a radius field for a specific cross-section.
///
/// A radius field point stores both the polar coordinates (angle, radius) and Cartesian
/// coordinates (X, Y) of a measurement. The axial position (Z) identifies which cross-section
/// this point belongs to.
///
/// There are two kinds of radius field in the program: one measured from the
/// FITTED centreline (RadiusFieldGenerator - shape diagnostics) and one
/// measured from the SCANNER/spindle axis at (0,0) (RotatingScanPointCloudBuilder.
/// BuildMachineFrameRadiusField - used for machine-position optimization).
/// Radius field points are grouped into Z/angle cells by RadiusFieldBinner,
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
    /// 0 degrees is the point-cloud +X direction (atan2(Y, X)); it is a log-fixed
    /// angle, not a clock position on the machine.
    /// Combined with RadiusMM to specify position in polar coordinates.
    /// </summary>
    public double AngleDeg { get; set; }

    /// <summary>
    /// Gets or sets the distance from the reference axis (fitted centreline or
    /// spindle axis - see the class remarks) to this surface point, in millimetres.
    ///
    /// This is the primary measurement used for defect analysis.
    /// </summary>
    public double RadiusMM { get; set; }

    /// <summary>
    /// Gets or sets the X coordinate (Cartesian) of this measurement point, measured in millimeters.
    ///
    /// The original point-cloud X of the surface point (not relative to the centreline).
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Gets or sets the Y coordinate (Cartesian) of this measurement point, measured in millimeters.
    ///
    /// The original point-cloud Y of the surface point (not relative to the centreline).
    /// </summary>
    public double Y { get; set; }
}
