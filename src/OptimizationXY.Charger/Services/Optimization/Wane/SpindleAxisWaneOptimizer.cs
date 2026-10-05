
using OptimizationXY.Charger.Models.Geometry;

namespace OptimizationXY.Charger.Services.Optimization.Wane;

public static class SpindleAxisWaneOptimizer
{

    /// <summary>
    /// Computes the axis-adjusted wane score for a given binned radius field and spindle axis defined by tail and head coordinates.
    /// </summary>
    /// <param name="field">The binned radius field points.</param>
    /// <param name="tailXMM">The X coordinate of the tail of the spindle axis in millimeters.</param>
    /// <param name="tailYMM">The Y coordinate of the tail of the spindle axis in millimeters.</param>
    /// <param name="headXMM">The X coordinate of the head of the spindle axis in millimeters.</param>
    /// <param name="headYMM">The Y coordinate of the head of the spindle axis in millimeters.</param>
    /// <param name="minAngleBinsPerSlice">The minimum number of angle bins required per slice.</param>
    /// <returns>The axis-adjusted wane score.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the binned radius field is empty.</exception>
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

                    // Reconstruct the measured surface point in the field's
                    // coordinate frame, then measure its exact Euclidean
                    // distance from the candidate spindle axis. The prior
                    // r - axis dot u formula was only a first-order estimate.
                    double surfaceX =
                        p.MeanRadiusMM * Math.Cos(angleRad);
                    double surfaceY =
                        p.MeanRadiusMM * Math.Sin(angleRad);

                    double dx = surfaceX - axisX;
                    double dy = surfaceY - axisY;

                    return Math.Sqrt(dx * dx + dy * dy);
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
