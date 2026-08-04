using OptimizationXY.Charger.Models.Operator;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Core;
using OptimizationXY.Charger.UI.Controls;

namespace OptimizationXY.Charger.Services.Operator;

/// <summary>
/// Converts the production pipeline's existing rotating radius field into a
/// simple operator profile and a scan-completeness decision.
/// </summary>
public sealed class OperatorScanQualityAnalyzer
{
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

        var grouped = validCells
            .GroupBy(cell => (int)Math.Round(cell.ZBinMM / zBinMM))
            .ToDictionary(
                group => group.Key,
                group => new SliceSummary(
                    ZMM: group.Average(cell => cell.ZBinMM),
                    RadiusMM: Median(group.Select(cell => cell.MedianRadiusMM)),
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

            profile.Add(new LogProfilePoint(
                PositionInches: (zMM - minimumIndex * zBinMM) / 25.4,
                RadiusInches: radiusMM / 25.4,
                IsValid: isValid));
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
        double processingSeconds = state.TimingEntries.Sum(entry => entry.ElapsedSeconds);

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

        ScanQualityStatus status = good
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
            expectedAngularBins);

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
            ProcessingSeconds = processingSeconds,
            ProfilePoints = profile
        };
    }

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

    private static string BuildDetail(
        double framePercent,
        double coveragePercent,
        double validSlicePercent,
        double largestGapMM,
        int minimumAngularBins,
        int expectedAngularBins)
    {
        return
            $"Frames {framePercent:F1}% | profile coverage {coveragePercent:F1}% | " +
            $"valid slices {validSlicePercent:F1}% | largest gap {largestGapMM / 25.4:F2} in | " +
            $"slice requirement {minimumAngularBins}/{expectedAngularBins} angular bins.";
    }

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

    private readonly record struct SliceSummary(
        double ZMM,
        double RadiusMM,
        int AngularBinCount);
}
