using OptimizationXY.Charger.Models.Defects;
using OptimizationXY.Charger.Models.Geometry;

namespace OptimizationXY.Charger.Services.Defects;

public static class RadiusDefectMapBuilder
{
    /// <summary>
    /// Builds a radius defect map from a list of binned radius field points.
    /// </summary>
    /// <param name="radiusField">The list of binned radius field points to analyze.</param>
    /// <param name="lowThresholdMM">The low threshold in millimeters for defect classification.</param>
    /// <param name="severeLowThresholdMM">The severe low threshold in millimeters for defect classification.</param>
    /// <param name="highThresholdMM">The high threshold in millimeters for defect classification.</param>
    /// <param name="lowThresholdPercent">The low threshold as a percentage for defect classification.</param>
    /// <param name="severeLowThresholdPercent">The severe low threshold as a percentage for defect classification.</param>
    /// <param name="highThresholdPercent">The high threshold as a percentage for defect classification.</param>
    /// <returns>A <see cref="RadiusDefectMap"/> containing the classified defect points.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the radius field is empty.</exception>
    public static RadiusDefectMap Build(
        List<BinnedRadiusFieldPoint> radiusField,
        double lowThresholdMM = -6.0,
        double severeLowThresholdMM = -10.0,
        double highThresholdMM = 6.0,
        double lowThresholdPercent = -0.045,
        double severeLowThresholdPercent = -0.075,
        double highThresholdPercent = 0.045)
    {
        if (radiusField == null || radiusField.Count == 0)
            throw new InvalidOperationException("Radius field is empty.");

        RadiusDefectMap map = new();

        var slices = radiusField
            .Where(p => p.MeanRadiusMM > 0)
            .GroupBy(p => p.ZBinMM)
            .OrderBy(g => g.Key)
            .ToList();

        foreach (var slice in slices)
        {
            var points = slice
                .Where(p => p.MeanRadiusMM > 0)
                .ToList();

            if (points.Count < 12)
                continue;

            double expectedRadius = GetExpectedRadius(points);

            if (expectedRadius <= 0)
                continue;

            foreach (var p in points)
            {
                double errorMM = p.MeanRadiusMM - expectedRadius;
                double errorPercent = errorMM / expectedRadius;

                RadiusDefectType type =
                    Classify(
                        errorMM,
                        errorPercent,
                        lowThresholdMM,
                        severeLowThresholdMM,
                        highThresholdMM,
                        lowThresholdPercent,
                        severeLowThresholdPercent,
                        highThresholdPercent);

                map.Points.Add(new RadiusDefectPoint
                {
                    ZBinMM = p.ZBinMM,
                    AngleBinDeg = p.AngleBinDeg,
                    MeasuredRadiusMM = p.MeanRadiusMM,
                    ExpectedRadiusMM = expectedRadius,
                    RadiusErrorMM = errorMM,
                    DefectType = type
                });
            }
        }

        return map;
    }

    /// <summary>
    /// Calculates the expected radius for a given slice of binned radius field points.
    /// </summary>
    /// <param name="slice">The slice of binned radius field points to analyze.</param>
    /// <returns>The expected radius for the given slice.</returns>
    private static double GetExpectedRadius(List<BinnedRadiusFieldPoint> slice)
    {
        var radii = slice
            .Where(p => p.MeanRadiusMM > 0)
            .Select(p => p.MeanRadiusMM)
            .OrderBy(r => r)
            .ToList();

        if (radii.Count == 0)
            return 0;

        int mid = radii.Count / 2;

        if (radii.Count % 2 == 1)
            return radii[mid];

        return (radii[mid - 1] + radii[mid]) / 2.0;
    }

    /// <summary>
    /// Classifies a radius defect based on the measured error in millimeters and percentage, using the provided thresholds.
    /// </summary>
    /// <param name="errorMM">The measured error in millimeters.</param>
    /// <param name="errorPercent">The measured error as a percentage of the expected radius.</param>
    /// <param name="lowThresholdMM">The threshold for a low radius defect in millimeters.</param>
    /// <param name="severeLowThresholdMM">The threshold for a severe low radius defect in millimeters.</param>
    /// <param name="highThresholdMM">The threshold for a high radius defect in millimeters.</param>
    /// <param name="lowThresholdPercent">The threshold for a low radius defect as a percentage.</param>
    /// <param name="severeLowThresholdPercent">The threshold for a severe low radius defect as a percentage.</param>
    /// <param name="highThresholdPercent">The threshold for a high radius defect as a percentage.</param>
    /// <returns>The classified radius defect type.</returns>
    private static RadiusDefectType Classify(
        double errorMM,
        double errorPercent,
        double lowThresholdMM,
        double severeLowThresholdMM,
        double highThresholdMM,
        double lowThresholdPercent,
        double severeLowThresholdPercent,
        double highThresholdPercent)
    {
        bool severeLow =
            errorMM <= severeLowThresholdMM &&
            errorPercent <= severeLowThresholdPercent;

        bool low =
            errorMM <= lowThresholdMM &&
            errorPercent <= lowThresholdPercent;

        bool high =
            errorMM >= highThresholdMM &&
            errorPercent >= highThresholdPercent;

        if (severeLow)
            return RadiusDefectType.SevereLowRadius;

        if (low)
            return RadiusDefectType.LowRadius;

        if (high)
            return RadiusDefectType.HighRadius;

        return RadiusDefectType.Good;
    }
}
