using OptimizationXY.Charger.Models;
using OptimizationXY.Charger.Models.Geometry;

namespace OptimizationXY.Charger.Services.Optimization.Wane
{
    public static class SpindleAxisWaneOptimizer
    {

        /// <summary>
        /// Computes the axis-adjusted wane score for a given binned radius field and spindle axis defined by tail and head coordinates.
        /// </summary>
        public static double ComputeAxisAdjustedWaneScore(
     List<BinnedRadiusFieldPoint> field,
            double tailXMM,
            double tailYMM,
            double headXMM,
            double headYMM,
            int minAngleBinsPerSlice)
        {
            if (field == null || field.Count == 0)
                throw new InvalidOperationException("Binned radius field is empty.");

            double minZ = field.Min(p => p.ZBinMM);
            double maxZ = field.Max(p => p.ZBinMM);
            double length = Math.Max(maxZ - minZ, 0.001);

            var sliceScores = new List<double>();

            foreach (var slice in field.GroupBy(p => p.ZBinMM))
            {
                double z = slice.Key;
                double t = (z - minZ) / length;

                double axisX = tailXMM + (headXMM - tailXMM) * t;
                double axisY = tailYMM + (headYMM - tailYMM) * t;

                var adjusted = slice
                    .Where(p => p.MeanRadiusMM > 0)
                    .Select(p =>
                    {
                        double angleRad = p.AngleBinDeg * Math.PI / 180.0;

                        double radialX = Math.Cos(angleRad);
                        double radialY = Math.Sin(angleRad);

                        double spindleShiftAlongRadius =
                            axisX * radialX + axisY * radialY;

                        return p.MeanRadiusMM - spindleShiftAlongRadius;
                    })
                    .ToList();

                if (adjusted.Count < minAngleBinsPerSlice)
                    continue;

                double imbalance = adjusted.Max() - adjusted.Min();

                sliceScores.Add(imbalance);
            }

            if (sliceScores.Count == 0)
                return double.MaxValue;

            return sliceScores.Average();
        }
    }
}
