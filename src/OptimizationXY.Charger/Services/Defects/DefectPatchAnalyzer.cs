using OptimizationXY.Charger.Models.Defects;

namespace OptimizationXY.Charger.Services.Defects;

/// <summary>
/// Provides methods for analyzing defect patches in a list of radius defect patches.
/// </summary>
public static class DefectPatchAnalyzer
{
    /// <summary>
    /// Analyzes a list of radius defect patches and returns a summary of the analysis.
    /// </summary>
    /// <param name="patches">The list of radius defect patches to analyze.</param>
    /// <returns>A <see cref="DefectPatchAnalysis"/> containing the results of the analysis.</returns>
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

        // Radius errors are negative for missing wood, so ordering ascending puts
        // the deepest patch first, and Min gives the deepest error.
        result.WorstPatch =
            patches
                .OrderBy(p => p.WorstErrorMM)
                .FirstOrDefault();

        result.WorstDepthMM =
            patches.Min(p => p.WorstErrorMM);

        result.AverageDepthMM =
            patches.Average(p => p.AverageErrorMM);

        // Severity = cells x |average error| per patch (see RadiusDefectPatchBuilder).
        result.TotalSeverityScore =
            patches.Sum(p => p.SeverityScore);

        result.AverageSeverityScore =
            patches.Average(p => p.SeverityScore);

        result.WorstSeverityScore =
            patches.Max(p => p.SeverityScore);

        return result;
    }
}
