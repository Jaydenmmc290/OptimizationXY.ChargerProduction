namespace OptimizationXY.Charger.Models.Calibration;

/// <summary>
/// Maps one scanner image column and detected laser Y position to a known
/// physical radius. RadiusBandId identifies the physical reference used to
/// create the point so noisy fractional LaserY values are not mistaken for
/// separate calibration bands.
/// </summary>
public sealed class LookupPoint
{

    // X is the scanner image column (pixel) coordinate.
    public int X { get; set; }

    // Y is the detected laser Y position (pixel) coordinate.
    public double LaserY { get; set; }

    // RadiusMM is the known physical radius in millimeters corresponding to the (X, LaserY) point.
    public double RadiusMM { get; set; }

    // RadiusBandId identifies the physical reference used to create the point,
    // ensuring that noisy fractional LaserY values are not mistaken for separate calibration bands.
    public int RadiusBandId { get; set; }

    // SourceName is the name of the source from which the lookup point was obtained.
    public string SourceName { get; set; } = string.Empty;
}
