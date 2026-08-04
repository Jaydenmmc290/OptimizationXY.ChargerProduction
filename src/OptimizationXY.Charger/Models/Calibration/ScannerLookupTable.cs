namespace OptimizationXY.Charger.Models.Calibration;

/// <summary>
/// Complete scanner calibration package used by the runtime lookup
/// interpolator. The file is machine-specific and must be regenerated after a
/// camera, lens, laser, mount, crop, orientation, seam, or camera-configuration change.
/// </summary>
public sealed class ScannerLookupTable
{
    public int SchemaVersion { get; set; } = 3;
    public string CalibrationId { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string SoftwareVersion { get; set; } = string.Empty;
    public string HeadCameraServerName { get; set; } = string.Empty;
    public string TailCameraServerName { get; set; } = string.Empty;
    public string HeadCameraConfigurationSha256 { get; set; } = string.Empty;
    public string TailCameraConfigurationSha256 { get; set; } = string.Empty;
    public string AcquisitionGeometrySha256 { get; set; } = string.Empty;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public double LengthPixelsPerMM { get; set; }
    public double MinimumCalibratedRadiusMM { get; set; }
    public double MaximumCalibratedRadiusMM { get; set; }
    public List<CalibrationReferenceSummary> References { get; set; } = new();
    public CalibrationQualitySummary Quality { get; set; } = new();
    public List<LookupPoint> Points { get; set; } = new();

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

    private static bool IsSha256(string value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

