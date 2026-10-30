namespace OptimizationXY.Charger.Models.Calibration;

/// <summary>
/// One calibration point: "at image column X, a laser line at image row
/// LaserY means the surface is RadiusMM from the spindle (home) axis".
/// Because the cameras look down at an angle, a bigger log moves the laser
/// line to a different row; the table of these points is what turns a laser
/// row into a real radius. RadiusBandId identifies the physical reference used to
/// create the point so noisy fractional LaserY values are not mistaken for
/// separate calibration bands.
/// </summary>
public sealed class LookupPoint
{

    // X is the scanner image column (pixel) coordinate.
    public int X { get; set; }

    // Y is the detected laser Y position (pixel) coordinate.
    public double LaserY { get; set; }

    // RadiusMM: known physical radius (mm) from the spindle/home axis for this (X, LaserY) point. 0 = the home cable.
    public double RadiusMM { get; set; }

    // RadiusBandId identifies the physical reference used to create the point,
    // ensuring that noisy fractional LaserY values are not mistaken for separate calibration bands.
    public int RadiusBandId { get; set; }

    // SourceName: name of the reference (e.g. which tube) the point came from.
    public string SourceName { get; set; } = string.Empty;
}
