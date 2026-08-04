using OptimizationXY.Charger.Models.Defects;

namespace OptimizationXY.Charger.Services.Defects
{
    public static class DefectPatchAnalyzer
    {

        /// <summary>
        /// Analyzes a list of radius defect patches and computes various statistics about them.
        /// </summary>
        public static DefectPatchAnalysis Analyze(List<RadiusDefectPatch> patches)
        {
            var result = new DefectPatchAnalysis();

            if (patches == null || patches.Count == 0)
                return result;

            result.PatchCount = patches.Count;

            result.TotalDefectCells =
                patches.Sum(p => p.CellCount);

            result.AveragePatchSizeCells =
                patches.Average(p => p.CellCount);

            result.LargestPatch =
                patches
                    .OrderByDescending(p => p.CellCount)
                    .FirstOrDefault();

            result.WorstPatch =
                patches
                    .OrderBy(p => p.WorstErrorMM)
                    .FirstOrDefault();

            result.WorstDepthMM =
                patches.Min(p => p.WorstErrorMM);

            result.AverageDepthMM =
                patches.Average(p => p.AverageErrorMM);

            result.TotalSeverityScore =
                patches.Sum(p => p.SeverityScore);

            result.AverageSeverityScore =
                patches.Average(p => p.SeverityScore);

            result.WorstSeverityScore =
                patches.Max(p => p.SeverityScore);

            return result;
        }
    }
}
