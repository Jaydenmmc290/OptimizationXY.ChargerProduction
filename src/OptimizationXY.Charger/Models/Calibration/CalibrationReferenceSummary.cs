namespace OptimizationXY.Charger.Models.Calibration;


/// <summary>
/// Summary of one calibration reference object (a tube of known diameter, or
/// the home cable at radius 0) and how well its laser line was detected.
/// </summary>
public sealed class CalibrationReferenceSummary
{

    // RadiusBandId: number identifying this reference's radius "band"; every lookup point made from it carries the same id.
    public int RadiusBandId { get; set; }

    // Name is the name of the calibration reference.
    public string Name { get; set; } = string.Empty;

    // DiameterMM is the diameter of the calibration reference in millimeters.
    public double DiameterMM { get; set; }

    // RadiusMM: DiameterMM / 2 - the radius every lookup point from this reference maps to.
    public double RadiusMM { get; set; }

    // ImageWidth / ImageHeight: size (pixels) of the joined calibration images.
    public int ImageWidth { get; set; }

    public int ImageHeight { get; set; }

    // CapturedFrameCount: number of joined frames captured of this reference.
    public int CapturedFrameCount { get; set; }

    // AcceptedPointCount: number of lookup points kept after filtering out bad detections.
    public int AcceptedPointCount { get; set; }

    // MeanDetectedLaserY: average image row (pixels) of the detected laser line on this reference.
    public double MeanDetectedLaserY { get; set; }

    // LaserYStandardDeviation: spread (pixels) of the detected laser row - a noise / steadiness measure.
    public double LaserYStandardDeviation { get; set; }

    // CapturedUtc is the UTC timestamp when the calibration reference was captured.
    public DateTime CapturedUtc { get; set; }
}
