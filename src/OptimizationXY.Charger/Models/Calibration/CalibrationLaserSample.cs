namespace OptimizationXY.Charger.Models.Calibration;


/// <summary>
/// Represents a sample point for calibration of the laser.
/// </summary>
public sealed class CalibrationLaserSample
{
    /// <summary>
    /// Gets or sets the X coordinate of the sample point.
    /// </summary>
    public int X { get; set; }
    /// <summary>
    /// Gets or sets the Y coordinate of the laser sample point.
    /// </summary>
    public double LaserY { get; set; }
}
