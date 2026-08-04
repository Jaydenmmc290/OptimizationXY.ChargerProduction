namespace OptimizationXY.Charger.Models.Calibration;

/// <summary>
/// Maps one scanner image column and detected laser Y position to a known
/// physical radius. RadiusBandId identifies the physical reference used to
/// create the point so noisy fractional LaserY values are not mistaken for
/// separate calibration bands.
/// </summary>
public sealed class LookupPoint
{
    public int X { get; set; }
    public double LaserY { get; set; }
    public double RadiusMM { get; set; }
    public int RadiusBandId { get; set; }
    public string SourceName { get; set; } = string.Empty;
}
