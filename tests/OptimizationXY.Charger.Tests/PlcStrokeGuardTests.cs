using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Plc;
using Xunit;

namespace OptimizationXY.Charger.Tests;

/// <summary>
/// The PLC stroke guard against the XY PLC's own solutions recorded on this
/// charger by the Rebuild program (Rebuild.Tests KinematicZoneTests and
/// CylinderModelTests): the values sent, the PLC's moves after rotation
/// (correction offsets ON at head (-0.33, 0) tail (-0.33, 0), zone limits
/// 135 / 315 that day) and the cylinder targets the PLC reported.
/// </summary>
public class PlcStrokeGuardTests
{
    private const double L = 135, R = 315;

    private sealed record Record(int Block, double[] Sent, double[] MovesAfterRotation, double[] Targets);

    /// <summary>2026-09-17: blocks over the cylinder stroke and some that were not.</summary>
    private static readonly Record[] Records =
    [
        new(40, [-2.089087, 0.707119, 0.8070471, -0.2576075], [-1.890, 1.560, 0.279, -0.589], [-4.267, -0.110, 0.992, 0.347]),
        new(48, [-2.077578, 1.328616, -0.2127458, 0.1052226], [-1.872, 1.924, -0.498, 0.168], [-4.607, -0.486, -0.884, 0.244]),
        new(65, [-2.405035, 0.6825004, 0.4251259, 0.107632], [-2.098, 1.768, 0.094, -0.113], [-4.772, -0.176, 0.248, 0.033]),
        new(69, [1.471254, -0.06352417, 1.870441, -0.2561761], [1.141, -0.064, 1.540, -0.256], [1.716, -0.974, 2.489, -1.176]),
        new(85, [-0.3992815, 0.2885687, 2.190408, -1.205037], [-0.678, 0.348, 1.641, -1.538], [-1.324, 0.207, 3.917, 0.003]),
        new(97, [-1.106927, 0.541352, 2.496379, -0.1345133], [-1.201, 0.871, 1.988, -0.937], [-2.596, 0.085, 3.823, -0.962]),
        new(141, [-2.271602, 1.043947, -2.005608, 0.4333245], [-1.674, 2.108, -1.781, 1.451], [-4.508, -0.809, -4.003, -0.078]),
        new(215, [-2.200242, 1.186985, 0.03596275, -0.1154942], [2.170, -0.028, -0.416, 0.086], [3.180, -2.071, -0.684, 0.259]),
        new(296, [0.05103498, 0.436795, -2.111692, 0.7964526], [-0.104, 0.377, -1.926, 1.596], [-0.526, -0.289, -4.355, -0.123])
    ];

    /// <summary>09-21 / 09-22: blocks the PLC refused for an X after rotation of 1.78 to 2.16 in.</summary>
    private static readonly Record[] XRefused =
    [
        new(238, [1.386292, 0.6917081, 2.114578, -0.4146636], [1.056, 0.692, 1.785, -0.415], [0.837, -1.647, 3.004, -1.269]),
        new(247, [0.3267899, -0.019609, 2.113811, -0.415784], [-0.003, -0.020, 1.784, -0.416], [0.015, 0.022, 3.004, -1.267]),
        new(321, [0.388217, 0.6747153, 2.474894, 0.1118122], [0.058, 0.675, 2.145, 0.112], [-0.591, -0.724, 3.003, -2.181]),
        new(303, [1.216436, 0.3550533, 2.260875, 0.1152305], [0.886, 0.355, 1.931, 0.115], [0.927, -1.149, 2.687, -1.954]),
        new(1048, [2.288949, 0.6140131, 2.274906, -0.1803993], [1.959, 0.614, 1.945, -0.180], [2.230, -2.485, 3.003, -1.673]),
        new(1053, [0.4481385, -0.123642, 2.486047, 0.2637605], [0.118, -0.124, 2.156, 0.264], [0.294, 0.022, 2.868, -2.346]),
        new(1086, [2.321694, 0.9272202, 0.9270973, -0.2563067], [1.992, 0.927, 0.597, -0.256], [1.964, -2.833, 1.119, -0.269]),
        new(1174, [0.2311844, 0.1262593, 2.473787, 0.3610802], [-0.099, 0.126, 2.144, 0.361], [-0.269, -0.043, 2.752, -2.429])
    ];

    /// <summary>Blocks the PLC executed with an X after rotation of 1.73 or less.</summary>
    private static readonly Record[] XExecuted =
    [
        new(1120, [-0.5654607, -0.7499037, 0.5137707, 1.954672], [-0.994, 0.664, 1.534, -0.780], [-2.093, 0.136, 3.004, -0.646]),
        new(1158, [1.984674, -0.4341685, -1.619759, 1.151205], [1.555, -0.759, -1.735, 1.405], [3.013, -0.689, -3.892, -0.064]),
        new(313, [-1.784172, 0.9355134, 0.139272, -0.2292614], [1.095, -1.425, -0.395, 0.260], [3.009, 0.433, -0.829, 0.068])
    ];

    /// <summary>The offsets the PLC had on that day, the end-move cap off so only the stroke model acts.</summary>
    private static PlcStrokeGuardSettings Settings(double maxX = 0, double maxNegative = 3.9, double maxPositive = 3.0, bool offsets = true) => new()
    {
        Enabled = true,
        MaxEndMoveInches = 0,
        MaxCylinderNegativeInches = maxNegative,
        MaxCylinderPositiveInches = maxPositive,
        MaxXAfterRotationInches = maxX,
        ZoneLeftLimitDegrees = L,
        ZoneRightLimitDegrees = R,
        CorrectionOffsetsEnabled = offsets,
        HeadXOffsetInches = -0.33,
        TailXOffsetInches = -0.33
    };

    private static PlcStrokeGuardResult Apply(double[] v, PlcStrokeGuardSettings s) =>
        PlcStrokeGuard.Apply(v[0], v[1], v[2], v[3], s);

    [Theory]
    // The PLC's own outputs of 2026-09-14 (SpindleRotateCalc, new dog / tail angle).
    [InlineData(-1.297507, 0.4901847, 0.7506393, -0.3777358, 0.568, 315.0, 129.0)]
    [InlineData(-1.216916, 0.5188028, 0.5568191, -0.1917769, 1.061, 135.0, 319.1)]
    [InlineData(-1.272461, 0.4113593, 0.5234476, -0.2710099, 0.575, 315.0, 125.5)]
    public void PredictReproducesThePlcsRotation(double dx, double dy, double tx, double ty, double src, double newDog, double newTail)
    {
        KinematicZonePrediction p = PlcStrokeGuard.Predict(dx, dy, tx, ty, L, R, false);

        Assert.Equal(src, p.SpindleRotateCalc, 0.0006);
        Assert.Equal(newDog, p.NewDogAngle, 0.06);
        Assert.Equal(newTail, p.NewTailAngle, 0.06);

        // Rotation keeps each end's length.
        Assert.Equal(Math.Sqrt(dx * dx + dy * dy), Math.Sqrt(p.DogX * p.DogX + p.DogY * p.DogY), 6);
        Assert.Equal(Math.Sqrt(tx * tx + ty * ty), Math.Sqrt(p.TailX * p.TailX + p.TailY * p.TailY), 6);
    }

    [Fact]
    public void AngleMatchesTheRoutine()
    {
        Assert.Equal(45.0, PlcStrokeGuard.Angle(1, 1), 0.01);
        Assert.Equal(135.0, PlcStrokeGuard.Angle(-1, 1), 0.01);
        Assert.Equal(225.0, PlcStrokeGuard.Angle(-1, -1), 0.01);
        Assert.Equal(315.0, PlcStrokeGuard.Angle(1, -1), 0.01);
        Assert.Equal(90.0, PlcStrokeGuard.Angle(0, 2));
        Assert.Equal(180.0, PlcStrokeGuard.Angle(-2, 0));
        Assert.Equal(270.0, PlcStrokeGuard.Angle(0, -2));
        Assert.Equal(0.0, PlcStrokeGuard.Angle(2, 0));
        Assert.True(double.IsNaN(PlcStrokeGuard.Angle(0, 0)));
    }

    [Fact]
    public void ModelReproducesThePlcTargetsFromTheValuesSent()
    {
        foreach (Record r in Records)
        {
            KinematicZonePrediction p = PlcStrokeGuard.Predict(r.Sent[0], r.Sent[1], r.Sent[2], r.Sent[3], L, R, false);

            Assert.Equal(r.MovesAfterRotation[0], p.DogX - 0.33, 0.01);
            Assert.Equal(r.MovesAfterRotation[1], p.DogY, 0.01);
            Assert.Equal(r.MovesAfterRotation[2], p.TailX - 0.33, 0.01);
            Assert.Equal(r.MovesAfterRotation[3], p.TailY, 0.01);

            Assert.Equal(r.Targets[0], PlcStrokeGuard.Target(0, p.DogX - 0.33, p.DogY), 0.03);
            Assert.Equal(r.Targets[2], PlcStrokeGuard.Target(2, p.TailX - 0.33, p.TailY), 0.03);
        }
    }

    [Fact]
    public void Block65HeadScaledUntilA1IsAtTheNegativeLimitTailUntouched()
    {
        Record r = Records[2];
        PlcStrokeGuardResult x = Apply(r.Sent, Settings());

        Assert.True(x.Limited);
        Assert.Equal(-4.78, x.TargetsBefore[0], 0.03);
        Assert.Equal(-3.9, x.TargetsAfter[0], 0.01);
        Assert.Equal(0.796, x.HeadScale, 0.01);
        Assert.Equal(1.0, x.TailScale);
        Assert.Equal(r.Sent[2], x.Sent[2]);
        Assert.Equal(r.Sent[3], x.Sent[3]);
        Assert.Equal(r.Sent[0] * x.HeadScale, x.Sent[0], 6);
        Assert.Equal(r.Sent[1] * x.HeadScale, x.Sent[1], 6);
        Assert.StartsWith("A1 would need -4.78 in (limit -3.9), sent 80% of the head move", x.StrokeNote);

        // Direction kept, so the PLC still rotates the dog to 135.
        KinematicZonePrediction after = PlcStrokeGuard.Predict(x.Sent[0], x.Sent[1], x.Sent[2], x.Sent[3], L, R, false);
        Assert.Equal(135.0, after.NewDogAngle, 0.01);
    }

    [Fact]
    public void Block85TailScaledUntilA2IsAtThePositiveLimit()
    {
        PlcStrokeGuardResult x = Apply(Records[4].Sent, Settings());

        Assert.True(x.Limited);
        Assert.Equal(3.917, x.TargetsBefore[2], 0.03);
        Assert.Equal(3.0, x.TargetsAfter[2], 0.01);
        Assert.Equal(1.0, x.HeadScale);
        Assert.Equal(0.79, x.TailScale, 0.02);
    }

    [Fact]
    public void EveryOverLimitBlockIsBroughtWithinTheLimitsAndTheRestUntouched()
    {
        foreach (Record r in Records)
        {
            PlcStrokeGuardResult x = Apply(r.Sent, Settings());
            bool over = r.Targets.Any(t => t < -3.9 || t > 3.0);

            foreach (double t in x.TargetsAfter)
                Assert.InRange(t, -3.9 - 0.01, 3.0 + 0.01);

            Assert.Equal(over, x.Limited);

            if (!over)
                Assert.Equal(r.Sent, x.Sent);
            else
                Assert.True(x.HeadScale > 0.5 && x.TailScale > 0.5, $"block {r.Block} keeps most of its move");
        }
    }

    [Fact]
    public void XLimitEveryRefusedBlockEndsWithinTheLimitExecutedOnesKeepTheirMove()
    {
        foreach (Record r in XRefused)
        {
            PlcStrokeGuardResult x = Apply(r.Sent, Settings(maxX: 1.7));

            Assert.True(x.Limited, $"block {r.Block}");
            Assert.True(Math.Abs(x.XAfterRotationAfter[0]) <= 1.705 && Math.Abs(x.XAfterRotationAfter[1]) <= 1.705, $"block {r.Block}");
            Assert.True(x.HeadScale >= 0.78 && x.TailScale >= 0.78, $"block {r.Block} keeps most of its move");

            foreach (double t in x.TargetsAfter)
                Assert.InRange(t, -3.9 - 0.01, 3.0 + 0.01);
        }

        foreach (Record r in XExecuted)
        {
            PlcStrokeGuardResult x = Apply(r.Sent, Settings(maxX: 1.7));
            Assert.True(x.HeadScale >= 0.97 && x.TailScale >= 0.97, $"block {r.Block} all but untouched");
        }

        PlcStrokeGuardResult b238 = Apply(XRefused[0].Sent, Settings(maxX: 1.7));
        Assert.Equal(1.785, b238.XAfterRotationBefore[1], 0.01);
        Assert.Equal(1.7, b238.XAfterRotationAfter[1], 0.005);
        Assert.Equal(1.0, b238.HeadScale);
        Assert.Equal(0.96, b238.TailScale, 0.01);
    }

    [Fact]
    public void DefaultsAreTheMeasuredLimits()
    {
        var s = new PlcStrokeGuardSettings();

        Assert.True(s.Enabled);
        Assert.Equal(3.0, s.MaxEndMoveInches);
        Assert.Equal(3.9, s.MaxCylinderNegativeInches);
        Assert.Equal(3.0, s.MaxCylinderPositiveInches);
        Assert.Equal(1.7, s.MaxXAfterRotationInches);
        Assert.Equal(135.0, s.ZoneLeftLimitDegrees);
        Assert.Equal(315.0, s.ZoneRightLimitDegrees);
        Assert.False(s.CorrectionOffsetsEnabled);
    }

    [Fact]
    public void EndMoveCapShortensAMoveLongerThanTheLimitDirectionKept()
    {
        var s = new PlcStrokeGuardSettings { MaxCylinderNegativeInches = 0, MaxCylinderPositiveInches = 0, MaxXAfterRotationInches = 0 };
        PlcStrokeGuardResult x = PlcStrokeGuard.Apply(0, 4.0, 0.3, 0.4, s);

        Assert.NotNull(x.ClampNote);
        Assert.Equal(0, x.Sent[0], 9);
        Assert.Equal(3.0, x.Sent[1], 9);
        Assert.Equal(0.3, x.Sent[2], 9);
        Assert.Equal(0.4, x.Sent[3], 9);
    }

    [Fact]
    public void SmallMoveIsSentUnchanged()
    {
        PlcStrokeGuardResult x = PlcStrokeGuard.Apply(0.25, -0.10, 0.40, 0.05, new PlcStrokeGuardSettings());

        Assert.False(x.Limited);
        Assert.Equal(new[] { 0.25, -0.10, 0.40, 0.05 }, x.Sent);
        Assert.Contains("within limits", x.Summary());
    }

    [Fact]
    public void DisabledSendsTheValuesAsComputed()
    {
        PlcStrokeGuardResult x = PlcStrokeGuard.Apply(2.5, 0, 2.5, 0, new PlcStrokeGuardSettings { Enabled = false });

        Assert.False(x.Enabled);
        Assert.False(x.Limited);
        Assert.Equal(new[] { 2.5, 0, 2.5, 0 }, x.Sent);
    }

    [Fact]
    public void NaNEndIsLeftAloneTheOtherEndStillGuarded()
    {
        PlcStrokeGuardResult x = PlcStrokeGuard.Apply(double.NaN, double.NaN, 2.5, 0, new PlcStrokeGuardSettings());

        Assert.True(double.IsNaN(x.Sent[0]));
        Assert.True(double.IsNaN(x.TargetsBefore[0]));
        Assert.Equal(1.0, x.HeadScale);
        Assert.True(x.TailScale < 1);
        Assert.True(Math.Abs(x.XAfterRotationAfter[1]) <= 1.705);
    }
}

public class PlcResultWindowTests
{
    [Theory]
    [InlineData(4.2, false, "(PLC waits 18 s)")]
    [InlineData(15.5, true, "close to the PLC's 18 s wait")]
    [InlineData(21.0, true, "fallback may already have positioned")]
    public void DescribeSaysHowCloseTheResultCameToThePlcsWait(double seconds, bool late, string expected)
    {
        string text = PlcResultWindow.Describe(seconds, 15.0, 18.0, out bool isLate);

        Assert.Equal(late, isLate);
        Assert.Contains(expected, text);
    }
}
