using MathNet.Numerics.Statistics;
using OptimizationXY.Charger.Models.Geometry;

namespace OptimizationXY.Charger.Services.Geometry;

public static class RadiusFieldBinner
{

    /// <summary>
    /// Bins a list of RadiusFieldPoint objects into BinnedRadiusFieldPoint objects based on specified z and angle bin sizes.
    /// </summary>
    /// <param name="rawField">The list of RadiusFieldPoint objects to bin.</param>
    /// <param name="zBinSizeMM">The size of each z-axis bin in millimeters.</param>
    /// <param name="angleBinSizeDeg">The size of each angle bin in degrees.</param>
    /// <param name="minPointsPerBin">The minimum number of points required per bin.</param>
    /// <returns>A list of BinnedRadiusFieldPoint objects representing the binned radius field.</returns>
    public static List<BinnedRadiusFieldPoint> Bin(
 List<RadiusFieldPoint> rawField,
 double zBinSizeMM,
 double angleBinSizeDeg,
 int minPointsPerBin,
 int ignoreLowestReadings = 0)
    {
        if (rawField == null || rawField.Count == 0)
            return [];

        var groups = rawField
            .Where(p =>
                p.RadiusMM > 0 &&
                !double.IsNaN(p.RadiusMM) &&
                !double.IsInfinity(p.RadiusMM))
            .GroupBy(p => new
            {
                ZBin = Math.Floor(p.Z / zBinSizeMM) * zBinSizeMM,
                AngleBin = NormalizeAngleBin(
                    Math.Floor(p.AngleDeg / angleBinSizeDeg) * angleBinSizeDeg)
            });

        List<BinnedRadiusFieldPoint> binned = [];

        foreach (var g in groups)
        {
            var radii = g
                .Select(p => p.RadiusMM)
                .OrderBy(v => v)
                .ToList();

            if (radii.Count < minPointsPerBin)
                continue;

            double stdDev =
                radii.Count >= 2
                    ? radii.StandardDeviation()
                    : 0.0;

            if (double.IsNaN(stdDev) || double.IsInfinity(stdDev))
                stdDev = 0.0;

            binned.Add(new BinnedRadiusFieldPoint
            {
                ZBinMM = g.Key.ZBin,
                AngleBinDeg = g.Key.AngleBin,

                MeanRadiusMM = radii.Mean(),
                MedianRadiusMM = radii.Median(),
                MinRadiusMM = RobustMinimum(radii, ignoreLowestReadings),
                MaxRadiusMM = radii.Maximum(),
                StdDevRadiusMM = stdDev,

                Count = radii.Count
            });
        }

        return [.. binned
            .OrderBy(p => p.ZBinMM)
            .ThenBy(p => p.AngleBinDeg)];
    }

    /// <summary>
    /// The lowest reading of a cell, optionally ignoring the N lowest readings.
    /// N = 0 is the plain minimum. Readings are only ignored when the cell has at
    /// least 2 x N + 3 of them, so a sparse cell keeps its true minimum.
    /// </summary>
    /// <param name="sortedRadii">The cell's radii, sorted ascending.</param>
    internal static double RobustMinimum(List<double> sortedRadii, int ignoreLowestReadings)
    {
        if (ignoreLowestReadings <= 0 ||
            sortedRadii.Count < 2 * ignoreLowestReadings + 3)
        {
            return sortedRadii.Minimum();
        }

        return sortedRadii[ignoreLowestReadings];
    }

    /// <summary>
    /// Normalizes an angle to the range [0, 360) degrees.
    /// </summary>
    /// <param name="angle">The angle in degrees to normalize.</param>
    /// <returns>The normalized angle in the range [0, 360) degrees.</returns>
    private static double NormalizeAngleBin(double angle)
    {
        angle %= 360.0;

        if (angle < 0)
            angle += 360.0;

        return angle;
    }
}
