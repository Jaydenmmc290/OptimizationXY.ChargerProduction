

namespace OptimizationXY.Charger.Models.Geometry;

/// <summary>
/// Represents a single point in 3D Cartesian space with X, Y, and Z coordinates.
///
/// This is a fundamental geometric data structure used throughout the application
/// for representing 3D coordinates in point clouds, geometry calculations, and
/// spatial analysis. All coordinates are in millimeters.
///
/// The scan point cloud uses this frame (see RotatingScanPointCloudBuilder):
/// Z = distance along the log, 0 at the TAIL end, increasing toward the head;
/// X/Y = position across the log, with (0,0) on the spindle axis the log was
/// scanned on. X = r cos(angle), Y = r sin(angle), where angle is the frame's
/// AngleDeg = RotatingScan.LaserAngleOffsetDeg + direction x (frame step),
/// set by RevolutionAngleAssigner. Those two settings are what line this frame
/// up with the machine X/Y directions; verify them with an off-centre
/// reference before trusting the X/Y signs.
///
/// Typical usage:
/// - Representing points in a 3D point cloud from the laser scanner
/// - Storing centerline positions along the log length
/// - Defining log geometry vertices
/// </summary>
public class Point3D
{
    /// <summary>
    /// Gets or sets the X coordinate of this point in 3D space, measured in millimeters.
    ///
    /// In the scan point cloud: r x cos(rotation angle), across the log.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Gets or sets the Y coordinate of this point in 3D space, measured in millimeters.
    ///
    /// In the scan point cloud: r x sin(rotation angle), across the log.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Gets or sets the Z coordinate of this point in 3D space, measured in millimeters.
    ///
    /// In the scan point cloud: distance along the log from the tail end.
    /// </summary>
    public double Z { get; set; }

    /// <summary>
    /// Initializes a new instance of the Point3D class at the origin (0, 0, 0).
    ///
    /// Properties must be set after construction using the X, Y, Z properties.
    /// </summary>
    public Point3D()
    {
    }

    /// <summary>
    /// Initializes a new instance of the Point3D class with specific coordinate values.
    /// </summary>
    /// <param name="x">The X coordinate in millimeters.</param>
    /// <param name="y">The Y coordinate in millimeters.</param>
    /// <param name="z">The Z coordinate in millimeters.</param>
    public Point3D(
        double x,
        double y,
        double z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}
