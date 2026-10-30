
using OptimizationXY.Charger.Models.Geometry;

namespace OptimizationXY.Charger.Services.Optimization.Wane;

/// <summary>
/// "Wane score" of a candidate spindle axis (legacy penalty search, and
/// report-only numbers in the other modes): for each Z slice, the difference
/// between the largest and smallest distance from the axis to the surface,
/// averaged over all slices. 0 would be a perfectly round log centred on the
/// axis; bigger = the axis is further off-centre or the log is less round.
/// Lower is better. Returns double.MaxValue if no slice has enough angle bins.
/// </summary>
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
            // Position of this slice along the log, 0 = tail end, 1 = head end; the
            // candidate axis is interpolated in a straight line between its two ends.
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

            // Radius spread around the axis in this slice.
            double imbalance = adjusted.Max() - adjusted.Min();

            sliceScores.Add(imbalance);
        }

        if (sliceScores.Count == 0)
            return double.MaxValue;

        return sliceScores.Average();
    }
}
