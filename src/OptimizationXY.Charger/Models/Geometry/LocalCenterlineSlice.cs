namespace OptimizationXY.Charger.Models.Geometry;


/// <summary>
/// One thin cross-section of the point cloud (a "slice" along the log): its
/// fitted circle centre and mean radius. Made by
/// CenterlineMathService.FitLocalCenterlines. Used for the refined centreline,
/// sweep/bow analysis and diagnostics.
/// </summary>
public class LocalCenterlineSlice 
{

    /// <summary>
    /// Gets or sets the slice position along the log (mean Z of its points), in millimetres. Z = 0 is the tail end.
    /// </summary>
    public double ZMM { get; set; }

    /// <summary>
    /// Gets or sets the X of the slice centre (least-squares circle fit, or the point average if the fit fails), mm.
    /// </summary>
    public double CenterXMM { get; set; }

    /// <summary>
    /// Gets or sets the Y of the slice centre, mm (same method as <see cref="CenterXMM"/>).
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
