using OptimizationXY.Charger.Models.Analysis;
using OptimizationXY.Charger.Models.Geometry;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Core;
using OptimizationXY.Charger.Visualization;
using Xunit;

namespace OptimizationXY.Charger.Tests;

/// <summary>
/// The spindle decision now uses UnwrappedPeelViewBuilder.ComputeMetrics
/// (numbers only) instead of BuildSimulation (numbers + image). These tests
/// prove both give exactly the same numbers, so the chosen position cannot
/// change because of it.
/// </summary>
public class PeelMetricsTests
{
    private static readonly PeelViewSettings PeelSettings = new()
    {
        VeneerLengthPerRevolutionInches = 0.5,
        VeneerMarkerSpacingInches = 50,
        MinimumUsableSheetCoveragePercent = 98,
        MaximumUsableSheetSevereWanePercent = 0.5,
        LengthBins = 520,
        AngleBins = 360,
        PixelsPerMM = 2,
        TopMargin = 95,
        LeftMargin = 95,
        RightMargin = 80,
        BottomMargin = 85,
        LowSpotVisualWarningMM = 6,
        LowSpotVisualMaxRevolutions = 4,
        OuterRadiusPercentile = 0.98,
        SurfaceSmoothPasses = 1,
        EnableTruncatedVisualization = true
    };

    private static readonly UsableRadiusSettings Usable = new()
    {
        CoreDiameterInches = 4,
        VeneerThicknessMM = 3.2
    };

    private static readonly WaneSettings Wane = new()
    {
        WaneToleranceMM = 1,
        SevereWaneMM = 3
    };

    /// <summary>
    /// Synthetic block: elliptical, tapered, bent, off-centre, with noise and a
    /// few missing patches, measured as 36 frames x 600 columns like a scan.
    /// </summary>
    private static List<Point3D> SyntheticBlock(int seed, double radiusMM, double ovality, double offsetXMM, double offsetYMM)
    {
        var random = new Random(seed);
        var points = new List<Point3D>();
        const double lengthMM = 1600.0;

        for (int frame = 0; frame < 36; frame++)
        {
            double angle = frame * 10.0 * Math.PI / 180.0;

            for (int column = 0; column < 600; column++)
            {
                double z = column * lengthMM / 599.0;

                // Missing patches (bark gaps, knots).
                if ((frame == 7 && column > 200 && column < 260) ||
                    (frame == 21 && column > 420 && column < 470))
                {
                    continue;
                }

                double taper = 1.0 - 0.08 * z / lengthMM;
                double bend = 6.0 * Math.Sin(Math.PI * z / lengthMM);
                double r = radiusMM * taper *
                           (1.0 + ovality * Math.Cos(2.0 * angle)) +
                           (random.NextDouble() - 0.5) * 1.2;

                points.Add(new Point3D
                {
                    X = offsetXMM + bend + r * Math.Cos(angle),
                    Y = offsetYMM + r * Math.Sin(angle),
                    Z = z
                });
            }
        }

        return points;
    }

    public static IEnumerable<object[]> Cases()
    {
        // block radius, ovality, block offset X/Y, axis tail X/Y, axis head X/Y
        yield return [150.0, 0.00, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0];
        yield return [150.0, 0.06, 4.0, -3.0, 0.0, 0.0, 0.0, 0.0];
        yield return [180.0, 0.10, 12.0, 8.0, 10.0, 7.5, 13.0, 9.0];
        yield return [230.0, 0.04, -20.0, 15.0, -18.0, 12.0, -22.0, 16.5];
        yield return [120.0, 0.12, 25.0, -25.0, 0.0, 0.0, 0.0, 0.0];
        yield return [45.0, 0.00, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0];   // at / below the 4 in core
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void MetricsOnlyMatchesDrawnSimulationExactly(
        double radiusMM,
        double ovality,
        double offsetXMM,
        double offsetYMM,
        double tailXMM,
        double tailYMM,
        double headXMM,
        double headYMM)
    {
        List<Point3D> cloud = SyntheticBlock(
            seed: (int)(radiusMM * 10 + ovality * 1000),
            radiusMM,
            ovality,
            offsetXMM,
            offsetYMM);

        PeelSimulationResult drawn = UnwrappedPeelViewBuilder.BuildSimulation(
            cloud, PeelSettings, Usable, Wane, tailXMM, tailYMM, headXMM, headYMM);

        PeelSimulationResult metrics = UnwrappedPeelViewBuilder.ComputeMetrics(
            cloud, PeelSettings, Usable, Wane, tailXMM, tailYMM, headXMM, headYMM);

        try
        {
            Assert.Equal(drawn.TotalFullSheets, metrics.TotalFullSheets);
            Assert.Equal(drawn.UsableFullSheets, metrics.UsableFullSheets);
            Assert.Equal(drawn.RejectedFullSheets, metrics.RejectedFullSheets);
            Assert.Equal(drawn.Revolutions, metrics.Revolutions);
            Assert.Equal(drawn.TotalVeneerLengthMM, metrics.TotalVeneerLengthMM);
            Assert.Equal(drawn.UsableVeneerLengthMM, metrics.UsableVeneerLengthMM);
            Assert.Equal(drawn.PartialSheetLengthMM, metrics.PartialSheetLengthMM);
            Assert.Equal(drawn.AverageUsableCoveragePercent, metrics.AverageUsableCoveragePercent);
            Assert.Equal(drawn.TotalWaneAreaPercent, metrics.TotalWaneAreaPercent);
            Assert.Equal(drawn.SevereWaneAreaPercent, metrics.SevereWaneAreaPercent);
            Assert.Equal(drawn.OuterDiameterMM, metrics.OuterDiameterMM);
            Assert.Equal(drawn.CoreDiameterMM, metrics.CoreDiameterMM);
            Assert.Equal(drawn.LogLengthMM, metrics.LogLengthMM);
            Assert.Equal(string.Empty, DeferredPeelViewRenderer.Compare(metrics, drawn));

            Assert.True(metrics.Image.IsEmpty, "Metrics-only result must not allocate an image.");
            Assert.False(drawn.Image.IsEmpty, "BuildSimulation must still draw the image.");
        }
        finally
        {
            drawn.Image.Dispose();
            metrics.Image.Dispose();
        }
    }

    [Fact]
    public void TestCasesCoverUsableAndRejectedSheets()
    {
        // Guard against a comparison that passes only because everything is 0.
        int usable = 0;
        int rejected = 0;

        foreach (object[] c in Cases())
        {
            List<Point3D> cloud = SyntheticBlock(
                (int)((double)c[0] * 10 + (double)c[1] * 1000),
                (double)c[0], (double)c[1], (double)c[2], (double)c[3]);

            PeelSimulationResult m = UnwrappedPeelViewBuilder.ComputeMetrics(
                cloud, PeelSettings, Usable, Wane,
                (double)c[4], (double)c[5], (double)c[6], (double)c[7]);

            usable += m.UsableFullSheets;
            rejected += m.RejectedFullSheets;
        }

        Assert.True(usable > 0, "Expected some usable sheets in the test blocks.");
        Assert.True(rejected > 0, "Expected some rejected sheets in the test blocks.");
    }
}
