namespace OptimizationXY.Charger.Models.Geometry;

/// <summary>
/// A straight 3D line through the log: a point on it (Origin) and a unit
/// direction vector. Coordinates are millimetres in the point-cloud frame
/// (Z along the log, 0 = tail end; X/Y across the log, 0,0 = the spindle axis
/// the log was scanned on). Made by CenterlineMathService (PCA fit, then
/// refined through the slice centres).
/// </summary>
public class CenterlineResult
{
    /// <summary>
    /// Gets or sets the X coordinate of the origin point of the centerline.
    /// </summary>
    public double OriginX { get; set; }

    /// <summary>
    /// Gets or sets the Y coordinate of the origin point of the centerline.
    /// </summary>
    public double OriginY { get; set; }

    /// <summary>
    /// Gets or sets the Z coordinate of the origin point of the centerline.
    /// </summary>
    public double OriginZ { get; set; }

    /// <summary>
    /// Gets or sets the X component of the unit direction vector (length 1).
    /// </summary>
    public double DirectionX { get; set; }

    /// <summary>
    /// Gets or sets the Y component of the unit direction vector.
    /// </summary>
    public double DirectionY { get; set; }

    /// <summary>
    /// Gets or sets the Z component of the unit direction vector (close to 1 for a log lying along Z).
    /// </summary>
    public double DirectionZ { get; set; }
}
