using OptimizationXY.Charger.Models.Calibration;


/// <summary>
/// Provides interpolation of radius values from a scanner lookup table based on image X and laser Y coordinates.
/// </summary>
namespace OptimizationXY.Charger.Services.Calibration;

public static class LookupTableInterpolator
{
    private const int MaximumNearbyPoints = 600;
    private const int NearbyXWindowPixels = 24;
    private const double BoundaryTolerancePixels = 2.0;


    /// <summary>
    /// Interpolates the radius in millimeters from a scanner lookup table based on the provided image X coordinate and laser Y coordinate.
    /// </summary>
    /// <param name="table">The scanner lookup table.</param>
    /// <param name="x">The image X coordinate.</param>
    /// <param name="laserY">The laser Y coordinate.</param>
    /// <returns>The interpolated radius in millimeters.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the lookup table is invalid or does not contain enough data for interpolation.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the provided coordinates are outside the valid range.</exception>
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

        List<LookupPoint> nearbyByX = [.. table.Points
            .Where(point => Math.Abs(point.X - x) <= NearbyXWindowPixels)
            .OrderBy(point => Math.Abs(point.X - x))
            .Take(MaximumNearbyPoints)];

        if (nearbyByX.Count < 2)
        {
            List<LookupPoint> value = [.. table.Points
                .OrderBy(point => Math.Abs(point.X - x))
                .Take(MaximumNearbyPoints)];
            nearbyByX = value;
        }

        List<CalibrationBand> bands = [.. nearbyByX
            .GroupBy(point => point.RadiusBandId > 0
                ? point.RadiusBandId
                : StableLegacyBandId(point.RadiusMM))
            .Select(group => new CalibrationBand(
                LaserY: Median(group.Select(point => point.LaserY)),
                RadiusMM: group.Average(point => point.RadiusMM)))
            .OrderBy(band => band.LaserY)];

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


    /// <summary>
    /// Calculates a stable legacy band ID from a radius in millimeters.
    /// </summary>
    /// <param name="radiusMM">The radius in millimeters.</param>
    /// <returns>The stable legacy band ID.</returns>
    private static int StableLegacyBandId(double radiusMM) =>
        checked((int)Math.Round(radiusMM * 1000.0));
    
    /// <summary>
    /// Calculates the median of a sequence of double values.
    /// </summary>
    /// <param name="values">The sequence of double values.</param>
    /// <returns>The median value.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the sequence is empty.</exception>
    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = [.. values.OrderBy(value => value)];

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

    /// <summary>
    /// Represents a calibration band with a laser Y coordinate and a corresponding radius in millimeters.
    /// </summary>
    /// <param name="LaserY">The laser Y coordinate.</param>
    /// <param name="RadiusMM">The radius in millimeters.</param>
    private sealed record CalibrationBand(
        double LaserY,
        double RadiusMM);
}
