namespace OptimizationXY.Charger.Models.Geometry;

/// <summary>
/// Represents the result of a centerline calculation, including the origin and direction of the centerline.
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
    /// Gets or sets the X component of the direction vector of the centerline.
    /// </summary>
    public double DirectionX { get; set; }

    /// <summary>
    /// Gets or sets the Y component of the direction vector of the centerline.
    /// </summary>
    public double DirectionY { get; set; }

    /// <summary>
    /// Gets or sets the Z component of the direction vector of the centerline.
    /// </summary>
    public double DirectionZ { get; set; }
}
