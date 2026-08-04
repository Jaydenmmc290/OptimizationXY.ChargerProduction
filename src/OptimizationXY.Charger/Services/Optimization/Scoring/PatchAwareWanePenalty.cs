using OptimizationXY.Charger.Models.Defects;

namespace OptimizationXY.Charger.Services.Optimization.Scoring
{
    public static class PatchAwareWanePenalty
    {

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

}
