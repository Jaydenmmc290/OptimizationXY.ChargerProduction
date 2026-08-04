using OptimizationXY.Charger.Models;
using OptimizationXY.Charger.Models.Defects;
using OptimizationXY.Charger.Models.Geometry;

namespace OptimizationXY.Charger.Services.Defects
{
    public static class RadiusDefectMapBuilder
    {
        /// <summary>
        /// Builds a taper-aware radius defect map from binned radius field data.
        /// Expected radius is calculated per Z slice, so normal log taper is not treated as a defect.
        /// Defects are classified using both absolute mm thresholds and percent-of-local-radius thresholds.
        /// </summary>
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
}
