namespace OptimizationXY.Charger.Models.Geometry;


/// <summary>
/// Represents a slice of the centerline at a specific Z height, including the center point and mean radius of the points in that slice.
/// </summary>
public class LocalCenterlineSlice 
{

    /// <summary>
    /// Gets or sets the Z height of the slice in millimeters.
    /// </summary>
    public double ZMM { get; set; }

    /// <summary>
    /// Gets or sets the X coordinate of the center point of the slice in millimeters.
    /// </summary>
    public double CenterXMM { get; set; }

    /// <summary>
    /// Gets or sets the Y coordinate of the center point of the slice in millimeters.
    /// </summary>
    public double CenterYMM { get; set; }

    /// <summary>
    /// Gets or sets the mean radius of the points in this slice from the center point in millimeters.
    /// </summary>
    public double MeanRadiusMM { get; set; }

    /// <summary>
    /// Gets or sets the number of points in this slice.
    /// </summary>
    public int PointCount { get; set; }

}
