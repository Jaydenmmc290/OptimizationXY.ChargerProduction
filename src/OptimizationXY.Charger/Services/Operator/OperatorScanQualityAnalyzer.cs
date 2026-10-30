using OptimizationXY.Charger.Models.Operator;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Core;
using OptimizationXY.Charger.UI.Controls;

namespace OptimizationXY.Charger.Services.Operator;

/// <summary>
/// Analyzes the quality of a scan based on the pipeline state and settings, producing a result that indicates whether
/// the scan is good, warning, or failed, along with relevant metrics and messages for the operator.
/// <para>
/// It judges whether the BLOCK WAS SCANNED COMPLETELY, not whether the log is
/// good wood. Uses the centreline-relative binned radius field:
/// frame percent (valid frames / nominal 36), profile coverage (Z slices with
/// any data / all slices between the ends), valid-slice percent (slices with
/// at least MinimumAngularCoveragePercentPerSlice of their angle bins), the
/// longest run of slices with no valid data (mm), and the largest arc of the
/// revolution with no data at all. Limits are in OperatorViewSettings. The
/// result is the scan-quality gate for the PLC write (called by MainForm after
/// processing).
/// </para>
/// </summary>
public sealed class OperatorScanQualityAnalyzer
{
    /// <summary>
    /// Analyzes the scan quality based on the provided pipeline state and returns an OperatorScanQualityResult containing
    /// the Good/Warning/Failed status, the numbers behind it and the side-view profile points.
    /// </summary>
    /// <param name="state">The current state of the pipeline.</param>
    /// <returns>An OperatorScanQualityResult containing the analysis of the scan quality.</returns>
    public static OperatorScanQualityResult Analyze(PipelineState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        AppSettings settings = state.Settings;
        OperatorViewSettings view = settings.OperatorView;
        double zBinMM = settings.Geometry.RadiusFieldZBinSizeMM;
        double angleBinDeg = settings.Geometry.RadiusFieldAngleBinSizeDeg;
        int expectedFrames = settings.Acquisition.TargetFramePairsPerLog;
        int validFrames = state.ScanFrames.Count(frame => frame.IsValid);
        double framePercent = expectedFrames <= 0
            ? 0.0
            : 100.0 * validFrames / expectedFrames;

        var validCells = state.BinnedRadiusField
            .Where(cell =>
                cell.Count > 0 &&
                double.IsFinite(cell.ZBinMM) &&
                double.IsFinite(cell.AngleBinDeg) &&
                double.IsFinite(cell.MedianRadiusMM) &&
                cell.MedianRadiusMM > 0.0)
            .ToList();

        if (validCells.Count == 0)
        {
            return Failed(
                validFrames,
                expectedFrames,
                "No usable radius profile was produced.",
                "The scan did not create any valid binned radius measurements. Check the laser, camera exposure, and block position, then rescan.");
        }

        int expectedAngularBins = Math.Max(
            1,
            (int)Math.Round(360.0 / angleBinDeg));
        int minimumAngularBins = Math.Max(
            3,
            (int)Math.Ceiling(
                expectedAngularBins *
                view.MinimumAngularCoveragePercentPerSlice / 100.0));

        // One summary per Z slice: median radius, min/max radius and how many
        // distinct angle bins have data.
        var grouped = validCells
            .GroupBy(cell => (int)Math.Round(cell.ZBinMM / zBinMM))
            .ToDictionary(
                group => group.Key,
                group => new SliceSummary(
                    ZMM: group.Average(cell => cell.ZBinMM),
                    RadiusMM: Median(group.Select(cell => cell.MedianRadiusMM)),
                    MinRadiusMM: group.Min(cell => cell.MedianRadiusMM),
                    MaxRadiusMM: group.Max(cell => cell.MedianRadiusMM),
                    AngularBinCount: group
                        .Select(cell => (int)Math.Round(cell.AngleBinDeg / angleBinDeg))
                        .Distinct()
                        .Count()));

        int minimumIndex = grouped.Keys.Min();
        int maximumIndex = grouped.Keys.Max();
        int expectedSliceCount = maximumIndex - minimumIndex + 1;
        double fallbackRadiusMM = Median(grouped.Values.Select(slice => slice.RadiusMM));

        var profile = new List<LogProfilePoint>(expectedSliceCount);
        var validDiametersMM = new List<double>(expectedSliceCount);
        int validSliceCount = 0;
        int measuredSliceCount = 0;
        int currentGapBins = 0;
        int largestGapBins = 0;

        // Walk every slice position between the two ends (including empty ones)
        // to count measured/valid slices and the longest run of invalid slices.
        for (int index = minimumIndex; index <= maximumIndex; index++)
        {
            bool hasSlice = grouped.TryGetValue(index, out SliceSummary slice);
            bool isValid = hasSlice &&
                slice.AngularBinCount >= minimumAngularBins &&
                double.IsFinite(slice.RadiusMM) &&
                slice.RadiusMM > 0.0;

            double zMM = hasSlice ? slice.ZMM : index * zBinMM;
            double radiusMM = hasSlice ? slice.RadiusMM : fallbackRadiusMM;

            if (hasSlice)
                measuredSliceCount++;

            if (isValid)
            {
                validSliceCount++;
                validDiametersMM.Add(radiusMM * 2.0);
                largestGapBins = Math.Max(largestGapBins, currentGapBins);
                currentGapBins = 0;
            }
            else
            {
                currentGapBins++;
            }

            // Extra detail for the log profile picture: the narrowest and widest
            // radius around the slice, and where the fitted block centreline sits
            // relative to the spindle axis at this slice.
            double minRadiusMM = hasSlice ? slice.MinRadiusMM : 0.0;
            double maxRadiusMM = hasSlice ? slice.MaxRadiusMM : 0.0;

            bool hasCenter = false;
            double centerXMM = 0.0;
            double centerYMM = 0.0;

            var centerline = state.Centerline;
            if (centerline is not null && Math.Abs(centerline.DirectionZ) > 1e-9)
            {
                double along = (zMM - centerline.OriginZ) / centerline.DirectionZ;
                centerXMM = centerline.OriginX + centerline.DirectionX * along;
                centerYMM = centerline.OriginY + centerline.DirectionY * along;
                hasCenter = double.IsFinite(centerXMM) && double.IsFinite(centerYMM);
            }

            profile.Add(new LogProfilePoint(
                PositionInches: (zMM - minimumIndex * zBinMM) / 25.4,
                RadiusInches: radiusMM / 25.4,
                IsValid: isValid,
                MinRadiusInches: minRadiusMM / 25.4,
                MaxRadiusInches: maxRadiusMM / 25.4,
                CenterXInches: hasCenter ? centerXMM / 25.4 : 0.0,
                CenterYInches: hasCenter ? centerYMM / 25.4 : 0.0,
                HasCenter: hasCenter));
        }

        largestGapBins = Math.Max(largestGapBins, currentGapBins);

        if (validDiametersMM.Count == 0)
        {
            return Failed(
                validFrames,
                expectedFrames,
                "The radius profile is too incomplete to use.",
                $"No axial slice reached the minimum angular coverage of {view.MinimumAngularCoveragePercentPerSlice:F0}%.",
                profile);
        }

        double profileCoveragePercent = expectedSliceCount <= 0
            ? 0.0
            : 100.0 * measuredSliceCount / expectedSliceCount;
        double validSlicePercent = expectedSliceCount <= 0
            ? 0.0
            : 100.0 * validSliceCount / expectedSliceCount;
        double largestGapMM = largestGapBins * zBinMM;
        double lengthMM = expectedSliceCount * zBinMM;
        // Real elapsed processing time. Adding up TimingEntries overstated it
        // several times over, because frames are processed in parallel and
        // nested steps were counted inside their parent stage as well.
        double processingSeconds = state.ProcessingElapsedSeconds > 0.0
            ? state.ProcessingElapsedSeconds
            : state.TimingEntries.Where(entry => entry.Depth == 0).Sum(entry => entry.ElapsedSeconds);

        // Profile coverage above is along the length of the block only. Frames
        // that are lost in a row leave an arc of the revolution with no data at
        // any position, which that number does not show, so measure it here.
        int[] occupiedAngularBins = [.. validCells
            .Select(cell =>
                (((int)Math.Round(cell.AngleBinDeg / angleBinDeg)) % expectedAngularBins + expectedAngularBins) %
                expectedAngularBins)
            .Distinct()
            .OrderBy(bin => bin)];
        double largestAngularGapDeg = LargestAngularGapDeg(
            occupiedAngularBins,
            expectedAngularBins,
            angleBinDeg);
        bool angularGapTooLarge =
            largestAngularGapDeg > view.MaximumAngularGapDeg;

        bool good =
            framePercent >= view.GoodMinimumFramePercent &&
            profileCoveragePercent >= view.GoodMinimumProfileCoveragePercent &&
            validSlicePercent >= view.GoodMinimumValidSlicePercent &&
            largestGapMM <= view.GoodMaximumMissingGapMM;

        bool warning =
            framePercent >= view.WarningMinimumFramePercent &&
            profileCoveragePercent >= view.WarningMinimumProfileCoveragePercent &&
            validSlicePercent >= view.WarningMinimumValidSlicePercent &&
            largestGapMM <= view.WarningMaximumMissingGapMM;

        // A large missing arc always fails; otherwise Good if every Good limit is
        // met, Warning if every Warning limit is met, else Failed.
        ScanQualityStatus status = angularGapTooLarge
            ? ScanQualityStatus.Failed
            : good
            ? ScanQualityStatus.Good
            : warning
                ? ScanQualityStatus.Warning
                : ScanQualityStatus.Failed;

        string message = status switch
        {
            ScanQualityStatus.Good =>
                "Full block profile captured. Continue with the lathe cycle.",
            ScanQualityStatus.Warning when settings.Plc.RequireGoodQuality =>
                "Scan has profile gaps. Rescan is required before automatic lathe result delivery.",
            ScanQualityStatus.Warning =>
                "Scan is usable, but review the highlighted profile gaps.",
            _ =>
                "Scan data is incomplete. Rescan the block before using the result."
        };

        string detail = BuildDetail(
            framePercent,
            profileCoveragePercent,
            validSlicePercent,
            largestGapMM,
            minimumAngularBins,
            expectedAngularBins,
            occupiedAngularBins.Length,
            largestAngularGapDeg);

        return new OperatorScanQualityResult
        {
            Status = status,
            OperatorMessage = message,
            DetailMessage = detail,
            FramesUsed = validFrames,
            ExpectedFrames = expectedFrames,
            ProfileCoveragePercent = profileCoveragePercent,
            ValidSlicePercent = validSlicePercent,
            LogLengthInches = lengthMM / 25.4,
            AverageDiameterInches = validDiametersMM.Average() / 25.4,
            MinimumDiameterInches = validDiametersMM.Min() / 25.4,
            MaximumDiameterInches = validDiametersMM.Max() / 25.4,
            LargestMissingGapInches = largestGapMM / 25.4,
            AngularBinsCovered = occupiedAngularBins.Length,
            ExpectedAngularBins = expectedAngularBins,
            LargestAngularGapDeg = largestAngularGapDeg,
            ProcessingSeconds = processingSeconds,
            ProfilePoints = profile
        };
    }

    /// <summary>
    /// Creates a failed OperatorScanQualityResult with the specified parameters.
    /// </summary>
    /// <param name="validFrames">The number of valid frames used in the scan.</param>
    /// <param name="expectedFrames">The number of expected frames for the scan.</param>
    /// <param name="message">The operator message describing the failure.</param>
    /// <param name="detail">The detailed message providing additional information about the failure.</param>
    /// <param name="profile">The list of log profile points associated with the scan.</param>
    /// <returns>An OperatorScanQualityResult representing a failed scan quality.</returns>
    private static OperatorScanQualityResult Failed(
        int validFrames,
        int expectedFrames,
        string message,
        string detail,
        IReadOnlyList<LogProfilePoint>? profile = null)
    {
        return new OperatorScanQualityResult
        {
            Status = ScanQualityStatus.Failed,
            OperatorMessage = message,
            DetailMessage = detail,
            FramesUsed = validFrames,
            ExpectedFrames = expectedFrames,
            ProfilePoints = profile ?? []
        };
    }

    /// <summary>
    /// Builds a detailed message summarizing the scan quality metrics, including frame percentage, profile
    /// coverage, valid slice percentage, largest gap, and slice requirements.
    /// </summary>
    /// <param name="framePercent">The percentage of frames used in the scan.</param>
    /// <param name="coveragePercent">The percentage of profile coverage in the scan.</param>
    /// <param name="validSlicePercent">The percentage of valid slices in the scan.</param>
    /// <param name="largestGapMM">The largest gap in the scan, measured in millimeters.</param>
    /// <param name="minimumAngularBins">The minimum number of angular bins required.</param>
    /// <param name="expectedAngularBins">The expected number of angular bins.</param>
    /// <returns>A detailed message summarizing the scan quality metrics.</returns>
    private static string BuildDetail(
        double framePercent,
        double coveragePercent,
        double validSlicePercent,
        double largestGapMM,
        int minimumAngularBins,
        int expectedAngularBins,
        int angularBinsCovered,
        double largestAngularGapDeg)
    {
        return
            $"Frames {framePercent:F1}% | profile coverage {coveragePercent:F1}% | " +
            $"valid slices {validSlicePercent:F1}% | largest gap {largestGapMM / 25.4:F2} in | " +
            $"slice requirement {minimumAngularBins}/{expectedAngularBins} angular bins | " +
            $"revolution covered {angularBinsCovered}/{expectedAngularBins} angle bins, " +
            $"largest angle gap {largestAngularGapDeg:F0} deg.";
    }

    /// <summary>
    /// Largest continuous arc, in degrees, between occupied angle bins around
    /// the full circle (the arc from the last bin back to the first counts too).
    /// </summary>
    /// <param name="occupiedBins">Distinct occupied bin indices, sorted ascending, each 0..expectedBins-1.</param>
    /// <param name="expectedBins">Number of bins in a full revolution.</param>
    /// <param name="binDeg">Size of one bin in degrees.</param>
    /// <returns>The missing arc in degrees; 360 when no bin is occupied.</returns>
    private static double LargestAngularGapDeg(
        int[] occupiedBins,
        int expectedBins,
        double binDeg)
    {
        if (occupiedBins.Length == 0)
            return 360.0;

        int largestMissing = 0;

        for (int i = 0; i < occupiedBins.Length; i++)
        {
            int current = occupiedBins[i];
            int next = i + 1 < occupiedBins.Length
                ? occupiedBins[i + 1]
                : occupiedBins[0] + expectedBins;

            largestMissing = Math.Max(largestMissing, next - current - 1);
        }

        return Math.Min(360.0, largestMissing * binDeg);
    }

    /// <summary>
    /// Calculates the median of a collection of double values, ignoring any non-finite values (NaN or Infinity).
    /// </summary>
    /// <param name="values">The collection of double values.</param>
    /// <returns>The median of the finite values in the collection, or 0.0 if there are no finite values.</returns>
    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = [.. values
            .Where(double.IsFinite)
            .OrderBy(value => value)];

        if (sorted.Length == 0)
            return 0.0;

        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2.0
            : sorted[middle];
    }

    /// <summary>
    /// Represents a summary of a slice in the binned radius field, including its Z position, radius, and angular bin count.
    /// </summary>
    /// <param name="ZMM">The Z position of the slice in millimeters.</param>
    /// <param name="RadiusMM">The radius of the slice in millimeters.</param>
    /// <param name="AngularBinCount">The number of angular bins in the slice.</param>
    private readonly record struct SliceSummary(
        double ZMM,
        double RadiusMM,
        double MinRadiusMM,
        double MaxRadiusMM,
        int AngularBinCount);
}
