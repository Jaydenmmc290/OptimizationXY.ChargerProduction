using MathNet.Numerics.Statistics;
using OptimizationXY.Charger.Models.Geometry;
using OptimizationXY.Charger.Models.Settings;

namespace OptimizationXY.Charger.Services.Geometry
{
    public static class RadiusFieldBinner
    {

        /// <summary>
        /// Bins raw radius field points into specified Z and angle bins, calculating statistics for each bin.
        /// Points with invalid radius values (<= 0, NaN, Infinity) are excluded from binning.
        /// </summary>
        public static List<BinnedRadiusFieldPoint> Bin(
     List<RadiusFieldPoint> rawField,
     double zBinSizeMM,
     double angleBinSizeDeg,
     int minPointsPerBin)
        {
            if (rawField == null || rawField.Count == 0)
                return new List<BinnedRadiusFieldPoint>();

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

            List<BinnedRadiusFieldPoint> binned = new();

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
                    MinRadiusMM = radii.Minimum(),
                    MaxRadiusMM = radii.Maximum(),
                    StdDevRadiusMM = stdDev,

                    Count = radii.Count
                });
            }

            return binned
                .OrderBy(p => p.ZBinMM)
                .ThenBy(p => p.AngleBinDeg)
                .ToList();
        }

        /// <summary>
        /// Normalizes an angle to the range [0, 360) degrees.
        /// This ensures that angles are consistently binned regardless of their original range.
        /// For example, an angle of -10 degrees would be normalized to 350 degrees, and an angle of 370 degrees would be normalized to 10 degrees.
        /// This is important for correct binning, as angles that differ by multiples of 360 degrees should be treated as equivalent.
        /// </summary>
        private static double NormalizeAngleBin(double angle)
        {
            angle %= 360.0;

            if (angle < 0)
                angle += 360.0;

            return angle;
        }
    }
}
