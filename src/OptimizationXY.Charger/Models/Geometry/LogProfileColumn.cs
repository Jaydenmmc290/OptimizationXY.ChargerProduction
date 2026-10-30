
// LogProfileColumn.cs - one calibrated measurement from one image column.
namespace OptimizationXY.Charger.Models.Geometry;

/// <summary>
/// One calibrated measurement from one image column of one frame: at image
/// column <see cref="X"/>, the log surface was <see cref="RadiusMM"/> from the
/// spindle axis. A frame's list of these is its "profile" along the log.
/// </summary>
public class LogProfileColumn 
{
    /// <summary>
    /// Gets or sets the image column (pixels in the joined image). Converted to
    /// a position along the log using the axial scale (pixels per mm).
    /// </summary>
    public int X { get; set; }  



    /// <summary>
    /// Gets or sets the image row (pixels) of the laser line centre in this column.
    /// </summary>
    public double CenterY { get; set; }  

    /// <summary>
    /// Gets or sets 2 x <see cref="RadiusMM"/>, in millimetres (the equivalent diameter at this point).
    /// </summary>
    public double DiameterMM { get; set; }  

    /// <summary>
    /// Gets or sets the calibrated radius in millimetres: the distance from the
    /// spindle (home) axis to the log surface where the laser hits it, from the calibration lookup table.
    /// </summary>
    public double RadiusMM { get; set; } 
}
