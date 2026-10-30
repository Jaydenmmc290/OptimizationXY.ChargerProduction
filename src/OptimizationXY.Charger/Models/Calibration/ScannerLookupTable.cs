namespace OptimizationXY.Charger.Models.Calibration;

/// <summary>
/// Complete scanner calibration ("lookup table") used at run time by
/// LookupTableInterpolator to turn each detected laser point (image column,
/// laser row) into a real radius in millimetres. Saved as
/// Config\Calibration\scanner-lookup-table.json.
/// <para>
/// The table also records a fingerprint of the setup it was made with
/// (camera identity, SHA-256 hashes of the camera settings and of the
/// crop/flip/overlap geometry). The program refuses to use the table if that
/// fingerprint no longer matches the current setup.
/// </para>
/// The file is machine-specific and must be regenerated after a
/// camera, lens, laser, mount, crop, orientation, seam, or camera-configuration change.
/// </summary>
public sealed class ScannerLookupTable
{

    // SchemaVersion: file format version; anything below 3 is rejected by IsValid.
    public int SchemaVersion { get; set; } = 3;

    // CalibrationId: unique id of this calibration (shown in logs and saved with each scan).
    public string CalibrationId { get; set; } = string.Empty;

    // CreatedUtc is the UTC timestamp when the scanner lookup table was created.
    public DateTime CreatedUtc { get; set; }

    // OperatorName is the name of the operator who performed the calibration.
    public string OperatorName { get; set; } = string.Empty;

    // MachineName is the name of the machine on which the calibration was performed.
    public string MachineName { get; set; } = string.Empty;

    //  SoftwareVersion is the version of the software used for the calibration.
    public string SoftwareVersion { get; set; } = string.Empty;

    // HeadCameraServerName: Sapera server name of the head camera when calibrated (part of the fingerprint).
    public string HeadCameraServerName { get; set; } = string.Empty;

    // TailCameraServerName: Sapera server name of the tail camera when calibrated (part of the fingerprint).
    public string TailCameraServerName { get; set; } = string.Empty;

    // HeadCameraConfigurationSha256: SHA-256 hash of the head camera settings used for calibration.
    public string HeadCameraConfigurationSha256 { get; set; } = string.Empty;

    // TailCameraConfigurationSha256: SHA-256 hash of the tail camera settings used for calibration.
    public string TailCameraConfigurationSha256 { get; set; } = string.Empty;

    // AcquisitionGeometrySha256: SHA-256 hash of the image-joining geometry (crop, flip, overlap, offset, ...) used for calibration.
    public string AcquisitionGeometrySha256 { get; set; } = string.Empty;

    // ImageWidth / ImageHeight: joined-image size (pixels) the table was built for. Scan images must be the same size.
    public int ImageWidth { get; set; }

    public int ImageHeight { get; set; }

    // LengthPixelsPerMM: axial scale - image columns per millimetre along the log.
    public double LengthPixelsPerMM { get; set; }

    // MinimumCalibratedRadiusMM is the minimum calibrated radius in millimeters.
    public double MinimumCalibratedRadiusMM { get; set; }

    // MaximumCalibratedRadiusMM: largest calibrated radius (mm). A laser point a little past the largest
    // reference is extrapolated (up to RotatingScan.MaximumCalibrationExtrapolationPixels); further out the
    // point is rejected (see LookupTableInterpolator).
    public double MaximumCalibratedRadiusMM { get; set; }

    // References is a list of calibration reference summaries used in the calibration process.
    public List<CalibrationReferenceSummary> References { get; set; } = [];

    // Quality is a summary of the calibration quality, including various metrics and statistics.
    public CalibrationQualitySummary Quality { get; set; } = new();

    // Points is a list of lookup points that map scanner image columns and detected laser Y positions to known physical radii.
    public List<LookupPoint> Points { get; set; } = [];


    /// <summary>
    /// True when the table is complete and self-consistent: schema 3 or later,
    /// an id, three valid SHA-256 hashes, a positive image size and axial scale,
    /// at least two points from at least two different radius bands, every
    /// point inside the image with a finite non-negative radius, and the
    /// recorded min/max radius matching the points (within 0.01 mm).
    /// </summary>
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
                // 0 is allowed: a home-position reference (the cable on the
                // spindle axis) is a measured band at radius 0.
                MinimumCalibratedRadiusMM < 0 ||
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
                point.RadiusMM < 0))
            {
                return false;
            }

            // Count distinct reference bands. Points with a band id are grouped by id;
            // older points without one are grouped by their radius rounded to 0.0001 mm.
            // At least two bands are needed to interpolate between radii.
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

