using OptimizationXY.Charger.Models.Defects;

namespace OptimizationXY.Charger.Services.Optimization.Scoring;
/// <summary>
/// This class provides a method to compute a penalty score for a list of radius defect patches,
/// taking into account the spindle axis adjustment based on the provided tail and head coordinates.
/// The penalty is calculated based on the depth, cell count, severity score, and spindle relief weight,
/// with adjustments made for the position of the patches along the spindle axis.
/// </summary>
public static class PatchAwareWanePenalty
{
    /// <summary>
    /// Computes the penalty score for a list of radius defect patches, adjusted for the spindle axis defined by the tail and head coordinates.
    /// </summary>
    /// <param name="patches">The list of radius defect patches.</param>
    /// <param name="tailXMM">The X coordinate of the tail of the spindle axis in millimeters.</param>
    /// <param name="tailYMM">The Y coordinate of the tail of the spindle axis in millimeters.</param>
    /// <param name="headXMM">The X coordinate of the head of the spindle axis in millimeters.</param>
    /// <param name="headYMM">The Y coordinate of the head of the spindle axis in millimeters.</param>
    /// <param name="depthWeight">The weight for the depth component of the penalty.</param>
    /// <param name="cellWeight">The weight for the cell count component of the penalty.</param>
    /// <param name="severityWeight">The weight for the severity score component of the penalty.</param>
    /// <param name="spindleReliefWeight">The weight for the spindle relief component of the penalty.</param>
    /// <returns>The computed penalty score.</returns>
    public static double ComputeSpindleAxisAdjusted(
List<RadiusDefectPatch> patches,
double tailXMM,
double tailYMM,
double headXMM,
double headYMM,
double depthWeight = 0.25,
double cellWeight = 0.002,
double severityWeight = 0.002,
double spindleReliefWeight = 0.03)
    {
        if (patches == null || patches.Count == 0)
            return 0.0;

        double minZ = patches.Min(p => p.StartZMM);
        double maxZ = patches.Max(p => p.EndZMM);
        double zRange = Math.Max(maxZ - minZ, 0.001);

        double total = 0.0;

        foreach (var patch in patches)
        {
            double patchCenterZ = (patch.StartZMM + patch.EndZMM) / 2.0;
            double zNorm = (patchCenterZ - minZ) / zRange;

            double localX = tailXMM + (headXMM - tailXMM) * zNorm;
            double localY = tailYMM + (headYMM - tailYMM) * zNorm;

            double spindleRelief =
                Math.Sqrt(localX * localX + localY * localY) * spindleReliefWeight;

            double basePenalty =
                Math.Abs(patch.WorstErrorMM) * depthWeight +
                patch.CellCount * cellWeight +
                patch.SeverityScore * severityWeight;

            double adjustedPenalty =
                Math.Max(0.0, basePenalty - spindleRelief);

            total += adjustedPenalty;
        }

        return total;
    }

}
