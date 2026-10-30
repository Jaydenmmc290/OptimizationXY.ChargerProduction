using Emgu.CV;
using Emgu.CV.Structure;
using OptimizationXY.Charger.Models.Geometry;
using OptimizationXY.Charger.Models.Optimization.Geometry;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Geometry;
using OptimizationXY.Charger.Services.Optimization.Geometry;
using OptimizationXY.Charger.Services.PointCloud;
using OptimizationXY.Charger.Services.Preprocessing;
using Xunit;

namespace OptimizationXY.Charger.Tests;

/// <summary>
/// The new accuracy options are off by default. These tests check that the
/// defaults are unchanged and that each option does what it says.
/// </summary>
public class AccuracyOptionTests
{
    [Fact]
    public void AllNewOptionsAreOffByDefault()
    {
        var settings = new AppSettings();

        Assert.False(settings.RotatingScan.KeepAllStripeSegments);
        Assert.False(settings.RotatingScan.SubPixelStripeCenter);
        Assert.Equal(0, settings.Geometry.OptimizationCellIgnoreLowestReadings);
        Assert.False(settings.InscribedGeometry.UseCertifiedCylinderSearch);
    }

    // ------------------------------------------------------------
    // Robust cell minimum
    // ------------------------------------------------------------

    [Fact]
    public void RobustMinimumZeroIsThePlainMinimum()
    {
        List<double> radii = [180.0, 200.0, 201.0, 202.0, 203.0];
        Assert.Equal(180.0, RadiusFieldBinner.RobustMinimum(radii, 0));
    }

    [Fact]
    public void RobustMinimumIgnoresLowestReadingsOnlyInWellFilledCells()
    {
        List<double> five = [180.0, 200.0, 201.0, 202.0, 203.0];
        Assert.Equal(200.0, RadiusFieldBinner.RobustMinimum(five, 1));   // 5 >= 2*1+3

        List<double> four = [180.0, 200.0, 201.0, 202.0];
        Assert.Equal(180.0, RadiusFieldBinner.RobustMinimum(four, 1));   // too few: true minimum kept
    }

    // ------------------------------------------------------------
    // Laser stripe options
    // ------------------------------------------------------------

    /// <summary>
    /// Grey test frame: a Gaussian stripe centred at <paramref name="centerY"/>,
    /// broken between x = 300 and 340 (a knot), plus a short reflection 60 px
    /// higher at x = 620-700.
    /// </summary>
    private static Image<Gray, byte> BrokenStripe(double centerY)
    {
        var image = new Image<Gray, byte>(760, 240);

        void Stripe(int fromX, int toX, double cy)
        {
            for (int x = fromX; x < toX; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    double d = y - cy;
                    double value = 10.0 + 220.0 * Math.Exp(-d * d / (2.0 * 2.0 * 2.0));
                    image.Data[y, x, 0] = (byte)Math.Clamp(Math.Round(value), 0, 255);
                }
            }
        }

        Stripe(50, 300, centerY);
        Stripe(340, 600, centerY);
        Stripe(620, 700, centerY - 60.0);
        return image;
    }

    [Fact]
    public void DefaultKeepsOnlyTheLongestPiece()
    {
        using var gray = BrokenStripe(100.0);
        using Mat binary = ImagePreprocessingService.BuildBinary(gray.Mat);

        List<LaserProfileColumn> profile = LaserStripeDetector.Detect(binary);

        Assert.All(profile, column => Assert.InRange(column.X, 340, 599));
    }

    [Fact]
    public void KeepAllAlignedSegmentsKeepsBothPiecesAndRejectsTheReflection()
    {
        using var gray = BrokenStripe(100.0);
        using Mat binary = ImagePreprocessingService.BuildBinary(gray.Mat);

        List<LaserProfileColumn> profile = LaserStripeDetector.Detect(
            binary, gray.Mat, keepAllAlignedSegments: true, subPixelCenter: false);

        Assert.Contains(profile, column => column.X < 300);
        Assert.Contains(profile, column => column.X >= 340 && column.X < 600);
        Assert.DoesNotContain(profile, column => column.X >= 620);
    }

    [Fact]
    public void SubPixelCenterFindsTheStripeCentreToATenthOfAPixel()
    {
        const double trueCenter = 100.3;
        using var gray = BrokenStripe(trueCenter);
        using Mat binary = ImagePreprocessingService.BuildBinary(gray.Mat);

        List<LaserProfileColumn> plain = LaserStripeDetector.Detect(binary);
        List<LaserProfileColumn> refined = LaserStripeDetector.Detect(
            binary, gray.Mat, keepAllAlignedSegments: false, subPixelCenter: true);

        double refinedError = refined.Average(column => Math.Abs(column.LaserCenterY - trueCenter));
        double plainError = plain.Average(column => Math.Abs(column.LaserCenterY - trueCenter));

        Assert.True(refinedError < 0.1, $"sub-pixel error {refinedError:F3} px");
        Assert.True(refinedError < plainError, $"sub-pixel {refinedError:F3} px vs plain {plainError:F3} px");
        Assert.Equal(plain.Select(c => c.X), refined.Select(c => c.X));
    }

    // ------------------------------------------------------------
    // Certified cylinder search
    // ------------------------------------------------------------

    /// <summary>
    /// Round block of radius 200 mm whose true axis runs from (6, -4) mm at the
    /// tail to (15, 9) mm at the head, measured the way the scanner does.
    /// </summary>
    private static List<BinnedRadiusFieldPoint> TiltedRoundBlock()
    {
        var points = new List<Point3D>();
        const double length = 1400.0;

        for (int a = 0; a < 360; a += 3)
        {
            double angle = a * Math.PI / 180.0;

            for (double z = 0.0; z <= length; z += 5.0)
            {
                double t = z / length;
                double cx = 6.0 + 9.0 * t;
                double cy = -4.0 + 13.0 * t;

                points.Add(new Point3D
                {
                    X = cx + 200.0 * Math.Cos(angle),
                    Y = cy + 200.0 * Math.Sin(angle),
                    Z = z
                });
            }
        }

        List<RadiusFieldPoint> field = RotatingScanPointCloudBuilder.BuildMachineFrameRadiusField(points);
        return RadiusFieldBinner.Bin(field, 10.0, 10.0, 1);
    }

    private static OptimizationSettings Search() => new()
    {
        CoarseMaxMoveMM = 20,
        CoarseStepMM = 10,
        MediumSearchRadiusMM = 5,
        MediumStepMM = 2.5,
        FineSearchRadiusMM = 2.5,
        FineStepMM = 1.5
    };

    private static MachineLimitSettings Limits() => new()
    {
        MaxTailMoveXMM = 50.8,
        MaxTailMoveYMM = 50.8,
        MaxHeadMoveXMM = 50.8,
        MaxHeadMoveYMM = 50.8,
        WarningMoveMM = 50.8
    };

    [Fact]
    public void CertifiedSearchFindsTheTrueAxisAndIsNeverWorseThanTheGrid()
    {
        List<BinnedRadiusFieldPoint> field = TiltedRoundBlock();

        var grid = new InscribedGeometrySettings { CenterlinePullWeight = 0.0 };
        var certified = new InscribedGeometrySettings
        {
            CenterlinePullWeight = 0.0,
            UseCertifiedCylinderSearch = true,
            CertifiedSearchToleranceMM = 0.05,
            CertifiedSearchMaxEvaluations = 400000
        };

        InscribedGeometryResult gridResult = InscribedGeometryOptimizer.FindLargestCylinder(
            field, Search(), grid, Limits(), CancellationToken.None);
        InscribedGeometryResult certifiedResult = InscribedGeometryOptimizer.FindLargestCylinder(
            field, Search(), certified, Limits(), CancellationToken.None);

        Assert.Null(gridResult.CertifiedGapMM);
        Assert.NotNull(certifiedResult.CertifiedGapMM);

        Assert.True(
            certifiedResult.GuaranteedCircularRadiusMM >= gridResult.GuaranteedCircularRadiusMM - 0.01,
            $"certified {certifiedResult.GuaranteedCircularRadiusMM:F3} vs grid {gridResult.GuaranteedCircularRadiusMM:F3}");

        var move = certifiedResult.SpindleMovement;
        double tailError = Math.Sqrt(Math.Pow(move.TailMoveXMM - 6.0, 2) + Math.Pow(move.TailMoveYMM + 4.0, 2));
        double headError = Math.Sqrt(Math.Pow(move.HeadMoveXMM - 15.0, 2) + Math.Pow(move.HeadMoveYMM - 9.0, 2));

        Assert.True(
            Math.Max(tailError, headError) <= 0.75,
            $"axis error tail {tailError:F2} mm, head {headError:F2} mm; gap {certifiedResult.CertifiedGapMM:F3} mm, {certifiedResult.CertifiedEvaluations} evaluations");
    }
}
