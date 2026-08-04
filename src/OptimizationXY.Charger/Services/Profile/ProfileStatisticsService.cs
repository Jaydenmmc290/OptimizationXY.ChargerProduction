using OptimizationXY.Charger.Models.Geometry;

namespace OptimizationXY.Charger.Services.Profile
{

    // ProfileStatisticsService provides methods for calculating statistical
    // properties of a log profile, such as average, minimum, and maximum
    // diameter in millimeters. These statistics are calculated from a list
    // of LogProfileColumn objects, which represent the diameter measurements
    // at different positions along the log length. The methods handle empty
    // profiles by returning 0 to avoid errors in calculations.
    public static class ProfileStatisticsService
    {

        // Calculates the average diameter in millimeters from a list of log profile columns. If the profile is empty, returns 0.
        public static double AverageDiameterMM(List<LogProfileColumn> profile)=> profile.Count == 0 ? 0 : profile.Average(p => p.DiameterMM);

        // Calculates the minimum diameter in millimeters from a list of log profile columns. If the profile is empty, returns 0.
        public static double MinDiameterMM(List<LogProfileColumn> profile)=> profile.Count == 0 ? 0 : profile.Min(p => p.DiameterMM);

        // Calculates the maximum diameter in millimeters from a list of log profile columns. If the profile is empty, returns 0.
        public static double MaxDiameterMM(List<LogProfileColumn> profile)=> profile.Count == 0 ? 0 : profile.Max(p => p.DiameterMM);

    }
}
