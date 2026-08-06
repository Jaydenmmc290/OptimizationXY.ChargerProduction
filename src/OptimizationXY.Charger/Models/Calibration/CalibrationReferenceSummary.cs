namespace OptimizationXY.Charger.Models.Calibration;


/// <summary>
/// Represents a summary of a calibration reference, including its properties and statistics.
/// </summary>
public sealed class CalibrationReferenceSummary
{

    //RadiusBandId is the unique identifier for the radius band associated with this calibration reference.
    public int RadiusBandId { get; set; }

    // Name is the name of the calibration reference.
    public string Name { get; set; } = string.Empty;

    // DiameterMM is the diameter of the calibration reference in millimeters.
    public double DiameterMM { get; set; }

    // RadiusMM is the radius of the calibration reference in millimeters.
    public double RadiusMM { get; set; }

    // ImageWidth is the width of the image captured during calibration.
    public int ImageWidth { get; set; }

    // ImageHeight is the height of the image captured during calibration.
    public int ImageHeight { get; set; }

    // CapturedFrameCount is the number of frames captured during the calibration process.
    public int CapturedFrameCount { get; set; }

    // AcceptedPointCount is the number of points accepted during the calibration process.
    public int AcceptedPointCount { get; set; }

    // MeanDetectedLaserX is the mean X coordinate of the detected laser points during calibration.
    public double MeanDetectedLaserY { get; set; }

    // MeanDetectedLaserY is the mean Y coordinate of the detected laser points during calibration.
    public double LaserYStandardDeviation { get; set; }

    // CapturedUtc is the UTC timestamp when the calibration reference was captured.
    public DateTime CapturedUtc { get; set; }
}
