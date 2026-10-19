using OptimizationXY.Charger.Models.Calibration;
using OptimizationXY.Charger.Services.Calibration;
using Xunit;

namespace OptimizationXY.Charger.Tests;

/// <summary>
/// A calibration may include the home-position cable as a measured band at
/// radius 0 (the spindle axis). These tests check that such a table is
/// accepted and interpolates correctly, and that negative radii are still refused.
/// </summary>
public class HomeAxisCalibrationTests
{
    private const int Width = 962;
    private const int Height = 480;

    // Laser rows close to the manager's reference images.
    private const double HomeRow = 270.0;
    private const double SmallRow = 213.5;
    private const double LargeRow = 157.0;
    private const double SmallRadius = 12.5 * 25.4 / 2.0;
    private const double LargeRadius = 23.955 * 25.4 / 2.0;

    private static ScannerLookupTable Table(double homeRadius = 0.0)
    {
        var points = new List<LookupPoint>();

        void Band(int id, double row, double radius)
        {
            for (int x = 0; x < Width; x++)
            {
                points.Add(new LookupPoint
                {
                    X = x,
                    LaserY = row,
                    RadiusMM = radius,
                    RadiusBandId = id,
                    SourceName = $"Band{id}"
                });
            }
        }

        Band(1, SmallRow, SmallRadius);
        Band(2, LargeRow, LargeRadius);
        Band(3, HomeRow, homeRadius);

        string sha = new('a', 64);

        return new ScannerLookupTable
        {
            SchemaVersion = 3,
            CalibrationId = "HOME-TEST",
            HeadCameraConfigurationSha256 = sha,
            TailCameraConfigurationSha256 = sha,
            AcquisitionGeometrySha256 = sha,
            ImageWidth = Width,
            ImageHeight = Height,
            LengthPixelsPerMM = 0.53,
            MinimumCalibratedRadiusMM = Math.Min(homeRadius, SmallRadius),
            MaximumCalibratedRadiusMM = LargeRadius,
            Points = points
        };
    }

    [Fact]
    public void TableWithHomeBandAtRadiusZeroIsValid()
    {
        Assert.True(Table().IsValid);
    }

    [Fact]
    public void NegativeRadiusIsStillRefused()
    {
        Assert.False(Table(homeRadius: -1.0).IsValid);
    }

    [Theory]
    [InlineData(HomeRow, 0.0)]
    [InlineData(SmallRow, SmallRadius)]
    [InlineData(LargeRow, LargeRadius)]
    public void ReferenceRowsReturnTheirKnownRadius(double row, double expected)
    {
        double radius = LookupTableInterpolator.GetRadiusMM(Table(), Width / 2, row);
        Assert.Equal(expected, radius, 6);
    }

    [Fact]
    public void RowBetweenHomeAndSmallTubeInterpolates()
    {
        double row = (HomeRow + SmallRow) / 2.0;
        double radius = LookupTableInterpolator.GetRadiusMM(Table(), 100, row);
        Assert.Equal(SmallRadius / 2.0, radius, 6);
    }
}
