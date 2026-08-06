namespace OptimizationXY.Charger.Models.Calibration;

/// <summary>
/// Complete scanner calibration package used by the runtime lookup
/// interpolator. The file is machine-specific and must be regenerated after a
/// camera, lens, laser, mount, crop, orientation, seam, or camera-configuration change.
/// </summary>
public sealed class ScannerLookupTable
{

    //SchemaVersion indicates the version of the schema used for the scanner lookup table.
    public int SchemaVersion { get; set; } = 3;

    // CalibrationId is the unique identifier for the calibration.  
    public string CalibrationId { get; set; } = string.Empty;

    // CreatedUtc is the UTC timestamp when the scanner lookup table was created.
    public DateTime CreatedUtc { get; set; }

    // OperatorName is the name of the operator who performed the calibration.
    public string OperatorName { get; set; } = string.Empty;

    // MachineName is the name of the machine on which the calibration was performed.
    public string MachineName { get; set; } = string.Empty;

    //  SoftwareVersion is the version of the software used for the calibration.
    public string SoftwareVersion { get; set; } = string.Empty;

    // HeadCameraSerialNumber is the serial number of the head camera used in the calibration.
    public string HeadCameraServerName { get; set; } = string.Empty;

    // TailCameraSerialNumber is the serial number of the tail camera used in the calibration.
    public string TailCameraServerName { get; set; } = string.Empty;

    // HeadCameraSerialNumber is the serial number of the head camera used in the calibration.
    public string HeadCameraConfigurationSha256 { get; set; } = string.Empty;

    // TailCameraSerialNumber is the serial number of the tail camera used in the calibration.
    public string TailCameraConfigurationSha256 { get; set; } = string.Empty;

    // AcquisitionGeometrySha256 is the SHA-256 hash of the acquisition geometry used in the calibration.
    public string AcquisitionGeometrySha256 { get; set; } = string.Empty;

    // ImageWidth is the width of the image captured during calibration.
    public int ImageWidth { get; set; }

    // ImageHeight is the height of the image captured during calibration.
    public int ImageHeight { get; set; }

    // LengthPixelsPerMM is the calibrated length in pixels per millimeter.
    public double LengthPixelsPerMM { get; set; }

    // MinimumCalibratedRadiusMM is the minimum calibrated radius in millimeters.
    public double MinimumCalibratedRadiusMM { get; set; }

    // MaximumCalibratedRadiusMM is the maximum calibrated radius in millimeters.
    public double MaximumCalibratedRadiusMM { get; set; }

    // References is a list of calibration reference summaries used in the calibration process.
    public List<CalibrationReferenceSummary> References { get; set; } = [];

    // Quality is a summary of the calibration quality, including various metrics and statistics.
    public CalibrationQualitySummary Quality { get; set; } = new();

    // Points is a list of lookup points that map scanner image columns and detected laser Y positions to known physical radii.
    public List<LookupPoint> Points { get; set; } = [];


    /// IsValid checks if the scanner lookup table is valid based on various criteria,
    /// including schema version, calibration ID, SHA-256 hashes, image dimensions, and point data.
    public bool IsValid
    {
        get
        {
            if (SchemaVersion < 3 ||
                string.IsNullOrWhiteSpace(CalibrationId) ||
                !IsSha256(HeadCameraConfigurationSha256) ||
                !IsSha256(TailCameraConfigurationSha256) ||
                !IsSha256(AcquisitionGeometrySha256) ||
                ImageWidth <= 0 ||
                ImageHeight <= 0 ||
                Points is not { Count: >= 2 } ||
                !double.IsFinite(LengthPixelsPerMM) ||
                LengthPixelsPerMM <= 0 ||
                !double.IsFinite(MinimumCalibratedRadiusMM) ||
                !double.IsFinite(MaximumCalibratedRadiusMM) ||
                MinimumCalibratedRadiusMM <= 0 ||
                MaximumCalibratedRadiusMM <= MinimumCalibratedRadiusMM)
            {
                return false;
            }

            if (Points.Any(point =>
                point is null ||
                point.X < 0 ||
                point.X >= ImageWidth ||
                !double.IsFinite(point.LaserY) ||
                point.LaserY < 0 ||
                point.LaserY >= ImageHeight ||
                !double.IsFinite(point.RadiusMM) ||
                point.RadiusMM <= 0))
            {
                return false;
            }

            int distinctRadiusBands = Points
                .Select(point => point.RadiusBandId > 0
                    ? $"B:{point.RadiusBandId}"
                    : $"R:{Math.Round(point.RadiusMM, 4)}")
                .Distinct(StringComparer.Ordinal)
                .Count();

            if (distinctRadiusBands < 2)
            {
                return false;
            }

            double minimumRadius = Points.Min(point => point.RadiusMM);
            double maximumRadius = Points.Max(point => point.RadiusMM);

            return maximumRadius - minimumRadius > 0.01 &&
                   Math.Abs(minimumRadius - MinimumCalibratedRadiusMM) <= 0.01 &&
                   Math.Abs(maximumRadius - MaximumCalibratedRadiusMM) <= 0.01;
        }
    }

    // IsSha256 checks if the given string is a valid SHA-256 hash by verifying its length and ensuring
    // all characters are hexadecimal digits.
    private static bool IsSha256(string value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

