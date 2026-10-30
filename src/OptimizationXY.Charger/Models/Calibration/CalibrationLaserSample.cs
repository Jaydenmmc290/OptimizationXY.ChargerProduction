namespace OptimizationXY.Charger.Models.Calibration;


/// <summary>
/// One laser-line measurement from a calibration image: at image column
/// <see cref="X"/> the laser line was found at row <see cref="LaserY"/>.
/// Held-out samples like these are not put in the lookup table; they are used
/// afterwards to check the finished table against a known radius.
/// </summary>
public sealed class CalibrationLaserSample
{
    /// <summary>
    /// Gets or sets the image column (pixels, along the log).
    /// </summary>
    public int X { get; set; }
    /// <summary>
    /// Gets or sets the detected laser-line row (pixels, sub-pixel precision).
    /// </summary>
    public double LaserY { get; set; }
}
