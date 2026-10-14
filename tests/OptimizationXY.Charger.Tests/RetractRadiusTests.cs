using OptimizationXY.Charger.Models.Geometry;
using OptimizationXY.Charger.Services.Production;
using Xunit;

namespace OptimizationXY.Charger.Tests;

/// <summary>
/// The safe carriage retract radius must cover the block as it turns around
/// the commanded spindle axis, and must never be smaller than before.
/// </summary>
public class RetractRadiusTests
{
    private static List<Point3D> RoundBlock(double radiusMM, double centerXMM, double centerYMM)
    {
        var points = new List<Point3D>();

        for (int a = 0; a < 360; a += 5)
        {
            double angle = a * Math.PI / 180.0;

            for (int z = 0; z <= 1500; z += 50)
            {
                points.Add(new Point3D
                {
                    X = centerXMM + radiusMM * Math.Cos(angle),
                    Y = centerYMM + radiusMM * Math.Sin(angle),
                    Z = z
                });
            }
        }

        return points;
    }

    [Fact]
    public void NoMove_EqualsLargestRadiusFromCurrentAxis()
    {
        var cloud = RoundBlock(100.0, 0.0, 0.0);

        double swing = PlcPayloadBuilder.LargestSwingRadiusMM(cloud, 0, 0, 0, 0);

        Assert.Equal(100.0, swing, 6);
    }

    [Fact]
    public void Move_CoversDistanceFromCommandedAxis()
    {
        // Block centred on the current axis. Command +10 mm X at both ends puts
        // the new axis at -10 mm in point-cloud coordinates, so the far side of
        // the block is 110 mm from it.
        var cloud = RoundBlock(100.0, 0.0, 0.0);

        double swing = PlcPayloadBuilder.LargestSwingRadiusMM(cloud, 10, 0, 10, 0);

        Assert.Equal(110.0, swing, 6);
    }

    [Fact]
    public void Move_ToCentre_NeverSmallerThanCurrentAxisRadius()
    {
        // Block 15 mm off the current axis; moving it to centre would make the
        // commanded-axis radius 100 mm, but the block still swings 115 mm around
        // the current axis before the move, so 115 mm is kept.
        var cloud = RoundBlock(100.0, 15.0, 0.0);

        double swing = PlcPayloadBuilder.LargestSwingRadiusMM(cloud, -15, 0, -15, 0);

        Assert.Equal(115.0, swing, 6);
    }

    [Fact]
    public void EmptyCloud_ReturnsNaN()
    {
        Assert.True(double.IsNaN(PlcPayloadBuilder.LargestSwingRadiusMM([], 0, 0, 0, 0)));
        Assert.True(double.IsNaN(PlcPayloadBuilder.LargestSwingRadiusMM(null, 0, 0, 0, 0)));
    }
}
