

namespace OptimizationXY.Charger.Models.Settings
{
    public sealed class PeelViewSettings
    {

        // Length of veneer per revolution.
        public double VeneerLengthPerRevolutionInches { get; set; } = 0.5;

        public double VeneerMarkerSpacingInches { get; set; } = 50.0;

        /// <summary>
        /// A full 50-inch sheet is usable when at least this percentage of its
        /// simulated cells contain wood.
        /// </summary>
        public double MinimumUsableSheetCoveragePercent { get; set; } = 98.0;

        /// <summary>
        /// Maximum severe-wane cell percentage allowed in a usable sheet.
        /// </summary>
        public double MaximumUsableSheetSevereWanePercent { get; set; } = 0.5;

        // Vertical resolution along log length.
        public int LengthBins { get; set; } = 520;

        // Angular resolution around log.
        public int AngleBins { get; set; } = 360;

        // Display scaling. Auto-reduced if image gets too wide.
        public double PixelsPerMM { get; set; } = 2.0;

        public int TopMargin { get; set; } = 95;
        public int LeftMargin { get; set; } = 95;
        public int RightMargin { get; set; } = 80;
        public int BottomMargin { get; set; } = 85;

        // Warning thresholds for thin veneer and low spots.

                        //public double ThinCleanupWarningMM { get; set; } = 1.0;
        public double LowSpotVisualWarningMM { get; set; } = 6.0;
        public int LowSpotVisualMaxRevolutions { get; set; } = 4;

        // Wane logic.
        // If peel radius is farther out than the scanned surface by this much,
        // the veneer is missing/thin at that spot.

        // Use percentile instead of absolute max so one bad point does not set the whole peel diameter.
        public double OuterRadiusPercentile { get; set; } = 0.98;

        // Smoothing for noisy scan cells.
        public int SurfaceSmoothPasses { get; set; } = 1;
    }
}
