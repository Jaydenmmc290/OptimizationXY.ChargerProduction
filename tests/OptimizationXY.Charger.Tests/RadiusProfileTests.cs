using OptimizationXY.Charger.Models.Calibration;
using OptimizationXY.Charger.Models.Geometry;
using OptimizationXY.Charger.Services.Calibration;
using OptimizationXY.Charger.Services.Profile;
using Xunit;

namespace OptimizationXY.Charger.Tests;

/// <summary>
/// The calibration table is now validated once per frame instead of once per
/// stripe column. These tests prove the radii are identical and that an
/// invalid table is still refused.
/// </summary>
public class RadiusProfileTests
{
    private const int Width = 952;
    private const int Height = 480;

    private static ScannerLookupTable ValidTable()
    {
        var points = new List<LookupPoint>();
        double[] radii = [150.0, 225.0, 304.23];

        for (int band = 0; band < radii.Length; band++)
        {
            for (int x = 0; x < Width; x += 2)
            {
                // Laser Y falls as the radius grows, with a gentle tilt along X.
                double laserY = 400.0 - (radii[band] - 150.0) * 1.6 + 0.01 * x + 0.3 * Math.Sin(x / 37.0);

                points.Add(new LookupPoint
                {
                    X = x,
                    LaserY = laserY,
                    RadiusMM = radii[band],
                    RadiusBandId = band + 1,
                    SourceName = $"Ref{band + 1}"
                });
            }
        }

        string sha = new('a', 64);

        return new ScannerLookupTable
        {
            SchemaVersion = 3,
            CalibrationId = "TEST",
            HeadCameraConfigurationSha256 = sha,
            TailCameraConfigurationSha256 = sha,
            AcquisitionGeometrySha256 = sha,
            ImageWidth = Width,
            ImageHeight = Height,
            LengthPixelsPerMM = 0.53,
            MinimumCalibratedRadiusMM = 150.0,
            MaximumCalibratedRadiusMM = 304.23,
            Points = points
        };
    }

    private static List<LaserProfileColumn> Stripe(int seed)
    {
        var random = new Random(seed);
        var stripe = new List<LaserProfileColumn>();

        for (int x = 90; x < 840; x++)
        {
            double y = 230.0 + 25.0 * Math.Sin(x / 120.0) + (random.NextDouble() - 0.5);
            stripe.Add(new LaserProfileColumn
            {
                X = x,
                TopY = (int)y - 4,
                BottomY = (int)y + 4,
                StripeHeightPixels = 9,
                LaserCenterY = y,
                Confidence = 1.0 / 9
            });
        }

        return stripe;
    }

    [Fact]
    public void TableUsedByTheTestsIsValid()
    {
        Assert.True(ValidTable().IsValid);
    }

    [Fact]
    public void ProfileRadiiAreIdenticalToPerPointValidatedLookup()
    {
        ScannerLookupTable table = ValidTable();

        foreach (int seed in new[] { 1, 2, 3 })
        {
            List<LaserProfileColumn> stripe = Stripe(seed);

            // Window 1 = no smoothing, so each radius maps to one stripe point.
            List<LogProfileColumn> profile = LaserRadiusProfileBuilder.BuildFromLookupTable(
                stripe,
                table,
                smoothingWindow: 1,
                maximumLargeRadiusExtrapolationPixels: 15);

            Assert.Equal(stripe.Count, profile.Count);

            for (int i = 0; i < stripe.Count; i++)
            {
                double expected = LookupTableInterpolator.GetRadiusMM(
                    table,
                    stripe[i].X,
                    stripe[i].LaserCenterY,
                    15);

                Assert.Equal(expected, profile[i].RadiusMM);
            }
        }
    }

    [Fact]
    public void InvalidTableIsStillRefused()
    {
        ScannerLookupTable table = ValidTable();
        table.CalibrationId = string.Empty;

        Assert.False(table.IsValid);
        Assert.Throws<InvalidOperationException>(() =>
            LaserRadiusProfileBuilder.BuildFromLookupTable(Stripe(1), table));
    }
}
