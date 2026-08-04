using MathNet.Numerics.Statistics;
using OptimizationXY.Charger.Models.Calibration;
using OptimizationXY.Charger.Models.Geometry;
using OptimizationXY.Charger.Services.Calibration;

namespace OptimizationXY.Charger.Services.Profile
{
    public static class LaserRadiusProfileBuilder
    {

        /// <summary>
        /// Builds a log profile from a laser profile using a lookup table and optional smoothing.
        /// </summary>
        public static List<LogProfileColumn> BuildFromLookupTable(List<LaserProfileColumn> laserProfile,ScannerLookupTable lookupTable,int smoothingWindow = 9)
        {
            if (laserProfile == null || laserProfile.Count == 0)
                return new List<LogProfileColumn>();

            if (lookupTable == null || lookupTable.Points.Count < 2)
                throw new InvalidOperationException("Lookup table needs at least two calibration points.");

            var orderedLaser = laserProfile
                .OrderBy(p => p.X)
                .ToList();

            List<LogProfileColumn> profile = new();

            foreach (var p in orderedLaser)
            {
                double radiusMM = LookupTableInterpolator.GetRadiusMM(
                    lookupTable,
                    p.X,
                    p.LaserCenterY);

                profile.Add(new LogProfileColumn
                {
                    X = p.X,
                    CenterY = p.LaserCenterY,
                    RadiusMM = radiusMM,
                    DiameterMM = radiusMM * 2.0
                });
            }

            if (smoothingWindow > 1)
                profile = Smooth(profile, smoothingWindow);

            return profile;
        }

        // Applies a median filter to smooth the profile.
        // Each column's radius is replaced by the median of the neighboring columns within the specified window.
        private static List<LogProfileColumn> Smooth(List<LogProfileColumn> profile,int window)
        {
            int half = window / 2;
            List<LogProfileColumn> smoothed = new();

            for (int i = 0; i < profile.Count; i++)
            {
                int start = Math.Max(0, i - half);
                int end = Math.Min(profile.Count - 1, i + half);

                var group = profile
                    .Skip(start)
                    .Take(end - start + 1)
                    .ToList();

                var radii = group
                    .Select(p => p.RadiusMM)
                    .OrderBy(v => v)
                    .ToList();

                double radius = radii.Median();

                smoothed.Add(new LogProfileColumn
                {
                    X = profile[i].X,
                    CenterY = profile[i].CenterY,
                    RadiusMM = radius,
                    DiameterMM = radius * 2.0
                });
            }

            return smoothed;
        }
    }
}
