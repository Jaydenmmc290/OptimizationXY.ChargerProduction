namespace OptimizationXY.Charger.Models.Calibration;

public sealed class CalibrationReferenceSummary
{
    public int RadiusBandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double DiameterMM { get; set; }
    public double RadiusMM { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public int CapturedFrameCount { get; set; }
    public int AcceptedPointCount { get; set; }
    public double MeanDetectedLaserY { get; set; }
    public double LaserYStandardDeviation { get; set; }
    public DateTime CapturedUtc { get; set; }
}
