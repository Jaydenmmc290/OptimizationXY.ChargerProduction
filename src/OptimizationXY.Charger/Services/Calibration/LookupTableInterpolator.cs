using OptimizationXY.Charger.Models.Calibration;

namespace OptimizationXY.Charger.Services.Calibration;

public static class LookupTableInterpolator
{
    private const int MaximumNearbyPoints = 600;
    private const int NearbyXWindowPixels = 24;
    private const double BoundaryTolerancePixels = 2.0;

    public static double GetRadiusMM(
        ScannerLookupTable table,
        int x,
        double laserY)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (!table.IsValid)
        {
            throw new InvalidOperationException(
                "Lookup table is invalid.");
        }

        if (x < 0 || x >= table.ImageWidth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Image X must be between 0 and {table.ImageWidth - 1} for the active calibration.");
        }

        if (!double.IsFinite(laserY) || laserY < 0 || laserY >= table.ImageHeight)
        {
            throw new ArgumentOutOfRangeException(
                nameof(laserY),
                $"Laser Y must be finite and between 0 and {table.ImageHeight - 1} for the active calibration.");
        }

        List<LookupPoint> nearbyByX = table.Points
            .Where(point => Math.Abs(point.X - x) <= NearbyXWindowPixels)
            .OrderBy(point => Math.Abs(point.X - x))
            .Take(MaximumNearbyPoints)
            .ToList();

        if (nearbyByX.Count < 2)
        {
            nearbyByX = table.Points
                .OrderBy(point => Math.Abs(point.X - x))
                .Take(MaximumNearbyPoints)
                .ToList();
        }

        List<CalibrationBand> bands = nearbyByX
            .GroupBy(point => point.RadiusBandId > 0
                ? point.RadiusBandId
                : StableLegacyBandId(point.RadiusMM))
            .Select(group => new CalibrationBand(
                LaserY: Median(group.Select(point => point.LaserY)),
                RadiusMM: group.Average(point => point.RadiusMM)))
            .OrderBy(band => band.LaserY)
            .ToList();

        if (bands.Count < 2)
        {
            throw new InvalidOperationException(
                "Lookup table needs at least two physical radius bands near the requested image column.");
        }

        double minimumCalibratedLaserY = bands[0].LaserY;
        double maximumCalibratedLaserY = bands[^1].LaserY;

        if (laserY < minimumCalibratedLaserY - BoundaryTolerancePixels ||
            laserY > maximumCalibratedLaserY + BoundaryTolerancePixels)
        {
            throw new InvalidOperationException(
                $"Detected laser Y={laserY:F3} at image X={x} is outside the active calibration range " +
                $"{minimumCalibratedLaserY:F3} to {maximumCalibratedLaserY:F3}. " +
                "Use a reference range that covers this block or recalibrate before production processing.");
        }

        double clampedLaserY = Math.Clamp(
            laserY,
            minimumCalibratedLaserY,
            maximumCalibratedLaserY);

        CalibrationBand lower = bands[0];
        CalibrationBand upper = bands[^1];

        for (int index = 1; index < bands.Count; index++)
        {
            if (bands[index].LaserY >= clampedLaserY)
            {
                lower = bands[index - 1];
                upper = bands[index];
                break;
            }
        }

        double deltaY = upper.LaserY - lower.LaserY;
        if (Math.Abs(deltaY) < 0.0001)
        {
            return (lower.RadiusMM + upper.RadiusMM) / 2.0;
        }

        double interpolation =
            (clampedLaserY - lower.LaserY) / deltaY;

        return lower.RadiusMM +
               interpolation * (upper.RadiusMM - lower.RadiusMM);
    }

    private static int StableLegacyBandId(double radiusMM) =>
        checked((int)Math.Round(radiusMM * 1000.0));

    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();

        if (sorted.Length == 0)
        {
            throw new InvalidOperationException(
                "Cannot calculate a median from an empty calibration band.");
        }

        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2.0
            : sorted[middle];
    }

    private sealed record CalibrationBand(
        double LaserY,
        double RadiusMM);
}
