using MathNet.Numerics.Statistics;
using OptimizationXY.Charger.Models.Calibration;
using OptimizationXY.Charger.Models.Geometry;
using OptimizationXY.Charger.Services.Calibration;

namespace OptimizationXY.Charger.Services.Profile;

/// <summary>
/// Provides functionality to build a log profile from a laser profile using a lookup table and optional smoothing.
/// </summary>
public static class LaserRadiusProfileBuilder
{

    /// <summary>
    /// Builds a log profile from a laser profile using the provided lookup table and optional smoothing.
    /// </summary>
    /// <param name="laserProfile">The laser profile to build the log profile from.</param>
    /// <param name="lookupTable">The lookup table to use for converting laser measurements to radius values.</param>
    /// <param name="smoothingWindow">The window size for the optional median smoothing filter.</param>
    /// <returns>A list of log profile columns representing the smoothed log profile.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the lookup table is invalid or has fewer than two points.</exception>
    public static List<LogProfileColumn> BuildFromLookupTable(List<LaserProfileColumn> laserProfile,ScannerLookupTable lookupTable,int smoothingWindow = 9,double maximumLargeRadiusExtrapolationPixels = 0.0)
    {
        if (laserProfile == null || laserProfile.Count == 0)
            return [];

        if (lookupTable == null || lookupTable.Points.Count < 2)
            throw new InvalidOperationException("Lookup table needs at least two calibration points.");

        var orderedLaser = laserProfile
            .OrderBy(p => p.X)
            .ToList();

        List<LogProfileColumn> profile = [];

        foreach (var p in orderedLaser)
        {
            double radiusMM = LookupTableInterpolator.GetRadiusMM(
                lookupTable,
                p.X,
                p.LaserCenterY,
                maximumLargeRadiusExtrapolationPixels);

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

    /// <summary>
    /// Applies a median smoothing filter to the provided log profile using the specified window size.
    /// </summary>
    /// <param name="profile">The log profile to smooth.</param>
    /// <param name="window">The size of the smoothing window.</param>
    /// <returns>A new list of log profile columns representing the smoothed profile.</returns>
    private static List<LogProfileColumn> Smooth(List<LogProfileColumn> profile,int window)
    {
        int half = window / 2;
        List<LogProfileColumn> smoothed = [];

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
