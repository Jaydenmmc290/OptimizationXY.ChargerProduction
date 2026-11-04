using System.Globalization;
using OptimizationXY.Charger.Models.Settings;

namespace OptimizationXY.Charger.Services.Plc;

// =============================================================================
// PlcStrokeGuard
// -----------------------------------------------------------------------------
// The last step before the head/tail move goes into the PLC payload. It does
// NOT change how the XY position is found: the optimizer and the virtual peel
// run exactly as before. It only makes sure the four values sent are ones the
// XY PLC will actually execute.
//
// Why: the PLC's Kinematic_Solution routine rotates the block out of the arm
// zone and turns each end's move into cylinder targets. When a target is past
// what the cylinders can do, the PLC refuses the whole solution and moves
// NOTHING, without a fault. Measured on this charger (Rebuild.Core
// CylinderModel / RepositionLimit, F-63 / F-68, Sept 2026):
//   - an A cylinder target beyond about -4.0 in is refused (-3.98 executed,
//     -4.22 refused); the A cylinders stop at +3.04 in;
//   - an X move after the rotation beyond about 1.775 in at either end is
//     refused (1.774 executed, 1.779 refused);
//   - no block on this charger has needed more than about 2 in of move.
//
// What it does (same rule as the Rebuild program):
//   1. Each end's move vector is capped at MaxEndMoveInches (direction kept).
//   2. The PLC routine's rotation is predicted (KinematicZonePrediction, a
//      line-for-line port of the routine's steps 4 and 6), the cylinder
//      targets are predicted from the moves after rotation, and an end whose
//      targets or X-after-rotation would be refused is scaled down, direction
//      kept, to the largest part that the PLC executes. Scaling a vector keeps
//      its angle, so the PLC's rotation choice does not change.
// Every change is reported in Note so it is logged and saved with the scan.
//
// All values here are INCHES in the PLC's sign convention, i.e. exactly the
// four head X, head Y, tail X, tail Y values that go into ScannedBlockData.
// =============================================================================

/// <summary>
/// The four values after the stroke guard, with what the guard saw.
/// </summary>
public sealed class PlcStrokeGuardResult
{
    /// <summary>Head X, head Y, tail X, tail Y as computed (inches).</summary>
    public double[] Computed { get; init; } = new double[4];

    /// <summary>Head X, head Y, tail X, tail Y to send (inches).</summary>
    public double[] Sent { get; init; } = new double[4];

    /// <summary>Predicted cylinder targets A1, B1, A2, B2 for the computed values (inches); NaN where an end could not be evaluated.</summary>
    public double[] TargetsBefore { get; init; } = [double.NaN, double.NaN, double.NaN, double.NaN];

    /// <summary>Predicted cylinder targets for the values sent.</summary>
    public double[] TargetsAfter { get; init; } = [double.NaN, double.NaN, double.NaN, double.NaN];

    /// <summary>Head and tail X move after the PLC's rotation (offsets in) for the computed values.</summary>
    public double[] XAfterRotationBefore { get; init; } = [double.NaN, double.NaN];

    /// <summary>The same for the values sent.</summary>
    public double[] XAfterRotationAfter { get; init; } = [double.NaN, double.NaN];

    /// <summary>The predicted PLC rotation for the values sent.</summary>
    public KinematicZonePrediction? Prediction { get; set; }

    /// <summary>Part of the head / tail move that is sent, 0 to 1.</summary>
    public double HeadScale { get; set; } = 1.0;
    public double TailScale { get; set; } = 1.0;

    /// <summary>What the end-move cap did, or null.</summary>
    public string? ClampNote { get; set; }

    /// <summary>What the cylinder-stroke limit did, or null.</summary>
    public string? StrokeNote { get; set; }

    /// <summary>False when the guard is switched off in settings.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>True when any value sent differs from the value computed.</summary>
    public bool Limited => ClampNote is not null || StrokeNote is not null;

    /// <summary>One line for the log and the diagnostics report.</summary>
    public string Summary()
    {
        CultureInfo ci = CultureInfo.InvariantCulture;

        if (!Enabled)
            return "PLC stroke guard OFF (PlcStrokeGuard.Enabled = false); values sent as computed.";

        string values(double[] v) =>
            "[" + string.Join(", ", v.Select(x => x.ToString("0.0000", ci))) + "]";

        string targets(double[] v) =>
            "[" + string.Join(", ", v.Select(x => double.IsNaN(x) ? "?" : x.ToString("+0.000;-0.000", ci))) + "]";

        string xs(double[] v) =>
            "[" + string.Join(", ", v.Select(x => double.IsNaN(x) ? "?" : x.ToString("+0.00;-0.00", ci))) + "]";

        if (!Limited)
        {
            return $"PLC stroke guard: within limits. Sent {values(Sent)} in; predicted cylinder targets {targets(TargetsAfter)}, X after rotation {xs(XAfterRotationAfter)}.";
        }

        string why = string.Join("; ", new[] { ClampNote, StrokeNote }.Where(n => n is not null));

        return $"PLC stroke guard LIMITED the move ({why}). Computed {values(Computed)} in, sending {values(Sent)} in; " +
               $"predicted cylinder targets {targets(TargetsBefore)} -> {targets(TargetsAfter)}, " +
               $"X after rotation {xs(XAfterRotationBefore)} -> {xs(XAfterRotationAfter)}.";
    }
}

/// <summary>
/// What the PLC's Kinematic_Solution routine will do with the four values sent:
/// each end's vector angle, whether it is in the arm zone, and the rotation it
/// chooses to bring both ends out.
/// </summary>
public sealed class KinematicZonePrediction
{
    /// <summary>Angle of the head (dog) vector, 0 to 360; NaN for a zero vector.</summary>
    public double DogAngle { get; set; }
    public double TailAngle { get; set; }
    public bool DogInZone { get; set; }
    public bool TailInZone { get; set; }
    public double NewDogAngle { get; set; }
    public double NewTailAngle { get; set; }

    /// <summary>The routine's SpindleRotateCalc: 1 for no rotation.</summary>
    public double SpindleRotateCalc { get; set; } = 1.0;

    /// <summary>Head / tail moves after the rotation, before correction offsets (inches).</summary>
    public double DogX { get; set; }
    public double DogY { get; set; }
    public double TailX { get; set; }
    public double TailY { get; set; }
}

/// <summary>
/// Predicts the PLC's rotation and cylinder targets and limits the four values
/// sent so the PLC executes them. See the file header.
/// </summary>
public static class PlcStrokeGuard
{
    /// <summary>
    /// Target = X coefficient * X + Y coefficient * Y + constant, per cylinder
    /// (A1 head, B1 head, A2 tail, B2 tail), X and Y being that end's move after
    /// the rotation with the correction offsets in. Fitted on 95 records of
    /// 2026-09-17 (A within 0.01 in rms, B within 0.06) by the Rebuild program.
    /// </summary>
    private static readonly double[][] Coefficients =
    [
        [1.441, -0.996, 0.004],
        [-0.846, -1.042, -0.040],
        [1.448, -0.991, 0.005],
        [-0.921, -1.090, -0.052]
    ];

    private static readonly string[] CylinderNames = ["A1", "B1", "A2", "B2"];

    /// <summary>The routine's 57.2958 (it uses 57.2957795 on the way back).</summary>
    private const double DegreesPerRadian = 57.2958;

    /// <summary>
    /// Applies the guard to head X, head Y, tail X, tail Y (inches, PLC sign
    /// convention). Never throws for finite input; non-finite ends are left as
    /// they are (the payload builder refuses them on its own).
    /// </summary>
    public static PlcStrokeGuardResult Apply(
        double headX,
        double headY,
        double tailX,
        double tailY,
        PlcStrokeGuardSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        double[] computed = [headX, headY, tailX, tailY];

        if (!settings.Enabled)
        {
            return new PlcStrokeGuardResult
            {
                Enabled = false,
                Computed = computed,
                Sent = (double[])computed.Clone()
            };
        }

        var result = new PlcStrokeGuardResult
        {
            Computed = computed,
            Sent = (double[])computed.Clone()
        };

        bool headOk = double.IsFinite(headX) && double.IsFinite(headY);
        bool tailOk = double.IsFinite(tailX) && double.IsFinite(tailY);

        if (!headOk && !tailOk)
            return result;

        // 1. Cap each end's move vector (Rebuild RepositionLimit, F-63).
        double[] moves = (double[])computed.Clone();
        result.ClampNote = ClampEndMoves(moves, settings.MaxEndMoveInches, headOk, tailOk);

        // 2. Predict the PLC's rotation and the cylinder targets (F-68).
        double left = settings.ZoneLeftLimitDegrees;
        double right = settings.ZoneRightLimitDegrees;
        bool bypass = settings.BypassRotationZone;

        double hx = 0, hy = 0, tx = 0, ty = 0;

        if (settings.CorrectionOffsetsEnabled)
        {
            hx = settings.HeadXOffsetInches;
            hy = settings.HeadYOffsetInches;
            tx = settings.TailXOffsetInches;
            ty = settings.TailYOffsetInches;
        }

        KinematicZonePrediction before = Predict(
            headOk ? moves[0] : 0, headOk ? moves[1] : 0,
            tailOk ? moves[2] : 0, tailOk ? moves[3] : 0,
            left, right, bypass);

        // Targets for the values as COMPUTED (before the end-move cap), for the log.
        KinematicZonePrediction computedPrediction = Predict(
            headOk ? computed[0] : 0, headOk ? computed[1] : 0,
            tailOk ? computed[2] : 0, tailOk ? computed[3] : 0,
            left, right, bypass);

        if (headOk)
        {
            result.TargetsBefore[0] = Target(0, computedPrediction.DogX + hx, computedPrediction.DogY + hy);
            result.TargetsBefore[1] = Target(1, computedPrediction.DogX + hx, computedPrediction.DogY + hy);
            result.XAfterRotationBefore[0] = computedPrediction.DogX + hx;
        }

        if (tailOk)
        {
            result.TargetsBefore[2] = Target(2, computedPrediction.TailX + tx, computedPrediction.TailY + ty);
            result.TargetsBefore[3] = Target(3, computedPrediction.TailX + tx, computedPrediction.TailY + ty);
            result.XAfterRotationBefore[1] = computedPrediction.TailX + tx;
        }

        string? headNote = null, tailNote = null;

        if (headOk)
        {
            result.HeadScale = Scale(0, before.DogX, before.DogY, hx, hy, settings, out headNote);
            moves[0] *= result.HeadScale;
            moves[1] *= result.HeadScale;
        }

        if (tailOk)
        {
            result.TailScale = Scale(2, before.TailX, before.TailY, tx, ty, settings, out tailNote);
            moves[2] *= result.TailScale;
            moves[3] *= result.TailScale;
        }

        if (headNote is not null || tailNote is not null)
            result.StrokeNote = string.Join("; ", new[] { headNote, tailNote }.Where(n => n is not null));

        for (int i = 0; i < 4; i++)
        {
            if ((i < 2 && headOk) || (i >= 2 && tailOk))
                result.Sent[i] = moves[i];
        }

        // Targets for what goes out: the routine run again on the sent values.
        KinematicZonePrediction after = Predict(
            headOk ? moves[0] : 0, headOk ? moves[1] : 0,
            tailOk ? moves[2] : 0, tailOk ? moves[3] : 0,
            left, right, bypass);

        result.Prediction = after;

        if (headOk)
        {
            result.TargetsAfter[0] = Target(0, after.DogX + hx, after.DogY + hy);
            result.TargetsAfter[1] = Target(1, after.DogX + hx, after.DogY + hy);
            result.XAfterRotationAfter[0] = after.DogX + hx;
        }

        if (tailOk)
        {
            result.TargetsAfter[2] = Target(2, after.TailX + tx, after.TailY + ty);
            result.TargetsAfter[3] = Target(3, after.TailX + tx, after.TailY + ty);
            result.XAfterRotationAfter[1] = after.TailX + tx;
        }

        return result;
    }

    /// <summary>Predicted target of one cylinder (0 A1, 1 B1, 2 A2, 3 B2) for its end's move after rotation, offsets in.</summary>
    public static double Target(int cylinder, double x, double y)
    {
        double[] c = Coefficients[cylinder];
        return c[0] * x + c[1] * y + c[2];
    }

    /// <summary>
    /// Shortens each end's move vector to <paramref name="maxInches"/>, keeping
    /// its direction. Returns what was shortened, or null. 0 or less disables.
    /// </summary>
    internal static string? ClampEndMoves(double[] moves, double maxInches, bool headOk, bool tailOk)
    {
        if (maxInches <= 0)
            return null;

        CultureInfo ci = CultureInfo.InvariantCulture;
        var notes = new List<string>();

        for (int end = 0; end < 2; end++)
        {
            if ((end == 0 && !headOk) || (end == 1 && !tailOk))
                continue;

            int i = end * 2;
            double length = Math.Sqrt(moves[i] * moves[i] + moves[i + 1] * moves[i + 1]);

            if (length > maxInches)
            {
                double s = maxInches / length;
                moves[i] *= s;
                moves[i + 1] *= s;
                notes.Add($"{(end == 0 ? "head" : "tail")} move {length.ToString("0.00", ci)} in capped at {maxInches.ToString("0.0#", ci)} in");
            }
        }

        return notes.Count == 0 ? null : string.Join("; ", notes);
    }

    /// <summary>
    /// The largest part, 0 to 1, of one end's move for which its X after
    /// rotation and both of its cylinder targets stay within the limits. Every
    /// quantity is linear in the scale.
    /// </summary>
    private static double Scale(
        int firstCylinder,
        double rotatedX,
        double rotatedY,
        double offsetX,
        double offsetY,
        PlcStrokeGuardSettings settings,
        out string? note)
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        double scale = 1.0;
        note = null;
        string end = firstCylinder == 0 ? "head" : "tail";

        double maxX = settings.MaxXAfterRotationInches;

        if (maxX > 0)
        {
            double fullX = rotatedX + offsetX;
            double xLimit = fullX > maxX ? maxX : fullX < -maxX ? -maxX : 0;

            if (xLimit != 0)
            {
                double s = (xLimit > 0 && rotatedX > 0) || (xLimit < 0 && rotatedX < 0)
                    ? (xLimit - offsetX) / rotatedX
                    : 0;
                s = Math.Clamp(s, 0, 1);

                if (s < scale)
                {
                    scale = s;
                    note = $"{end} X after rotation would be {fullX.ToString("+0.00;-0.00", ci)} in (limit {xLimit.ToString("0.0#;-0.0#", ci)}), sent {Math.Round(scale * 100).ToString(ci)}% of the {end} move";
                }
            }
        }

        double maxNegative = settings.MaxCylinderNegativeInches;
        double maxPositive = settings.MaxCylinderPositiveInches;

        for (int i = firstCylinder; i < firstCylinder + 2; i++)
        {
            double[] c = Coefficients[i];
            double m = c[0] * rotatedX + c[1] * rotatedY;
            double k = c[0] * offsetX + c[1] * offsetY + c[2];
            double full = m + k;
            double s;
            double limit;

            if (maxPositive > 0 && full > maxPositive)
            {
                limit = maxPositive;
                s = m > 0 ? (maxPositive - k) / m : 0;
            }
            else if (maxNegative > 0 && full < -maxNegative)
            {
                limit = -maxNegative;
                s = m < 0 ? (-maxNegative - k) / m : 0;
            }
            else
            {
                continue;
            }

            s = Math.Clamp(s, 0, 1);

            if (s < scale)
            {
                scale = s;
                note = $"{CylinderNames[i]} would need {full.ToString("+0.00;-0.00", ci)} in (limit {limit.ToString("+0.0#;-0.0#", ci)}), sent {Math.Round(scale * 100).ToString(ci)}% of the {end} move";
            }
        }

        return scale;
    }

    /// <summary>
    /// The vector angle resolved to 0..360 the way the PLC routine does it.
    /// A zero vector has no angle (NaN, counts as out of the zone).
    /// </summary>
    public static double Angle(double x, double y)
    {
        if (x > 0 && y > 0) return DegreesPerRadian * Math.Atan(y / x);
        if (x < 0 && y > 0) return 180 + DegreesPerRadian * Math.Atan(y / x);
        if (x < 0 && y < 0) return 180 + DegreesPerRadian * Math.Atan(y / x);
        if (x > 0 && y < 0) return 360 + DegreesPerRadian * Math.Atan(y / x);
        if (x == 0 && y > 0) return 90;
        if (x < 0 && y == 0) return 180;
        if (x == 0 && y < 0) return 270;
        if (x > 0 && y == 0) return 0;
        return double.NaN;
    }

    private static bool InZone(double angle, double left, double right) =>
        !double.IsNaN(angle) && angle > left && angle < right;

    /// <summary>
    /// Steps 4 and 6 of the PLC's Kinematic_Solution routine on the four values
    /// as loaded (a line-for-line port, including the routine's own quirks).
    /// </summary>
    public static KinematicZonePrediction Predict(
        double dogX,
        double dogY,
        double tailX,
        double tailY,
        double left,
        double right,
        bool bypass)
    {
        var p = new KinematicZonePrediction
        {
            DogAngle = Angle(dogX, dogY),
            TailAngle = Angle(tailX, tailY)
        };

        p.DogInZone = !bypass && InZone(p.DogAngle, left, right);
        p.TailInZone = !bypass && InZone(p.TailAngle, left, right);
        p.NewDogAngle = p.DogAngle;
        p.NewTailAngle = p.TailAngle;
        p.SpindleRotateCalc = 1.0;

        double dog = p.DogAngle, tail = p.TailAngle;
        double dogLength = Math.Sqrt(dogX * dogX + dogY * dogY);
        double tailLength = Math.Sqrt(tailX * tailX + tailY * tailY);

        if (p.DogInZone && p.TailInZone)
        {
            double dogFromLeft = dog - left, tailFromLeft = tail - left;
            double dogFromRight = right - dog, tailFromRight = right - tail;

            if (dog > tail && dogFromRight > tailFromLeft)
            {
                p.NewDogAngle = left;
                p.NewTailAngle = Wrap(tail - dogFromLeft);
                p.SpindleRotateCalc = 1 + dogFromLeft / 360;
            }
            else if (dog > tail)
            {
                p.NewDogAngle = Wrap(dog + tailFromRight);
                p.NewTailAngle = right;
                p.SpindleRotateCalc = 1 - tailFromRight / 360;
            }
            else if (dog < tail && dogFromLeft > tailFromRight)
            {
                p.NewDogAngle = right;
                p.NewTailAngle = Wrap(tail + dogFromRight);
                p.SpindleRotateCalc = 1 - dogFromRight / 360;
            }
            else if (dog < tail)
            {
                p.NewDogAngle = Wrap(dog - tailFromLeft);
                p.NewTailAngle = left;
                p.SpindleRotateCalc = 1 + tailFromLeft / 360;
            }
            else if (dog > 240)
            {
                p.NewDogAngle = right;
                p.NewTailAngle = right;
                p.SpindleRotateCalc = 1 - dogFromRight / 360;
            }
            else
            {
                p.NewDogAngle = left;
                p.NewTailAngle = left;
                p.SpindleRotateCalc = 1 + dogFromLeft / 360;
            }
        }
        else if (p.DogInZone)
        {
            double angleA = dog - left;
            double angleB = tail >= right ? tail - right : tail + 360 - right;

            if (angleA <= angleB)
            {
                p.NewTailAngle = Wrap(tail - angleA);
                p.NewDogAngle = left;
                p.SpindleRotateCalc = 1 + angleA / 360;
            }
            else
            {
                p.NewTailAngle = Wrap(tail + (180 - angleA));
                p.NewDogAngle = right;
                p.SpindleRotateCalc = 1 - (180 - angleA) / 360;
            }
        }
        else if (p.TailInZone)
        {
            double angleA = tail + 180 - right;
            if (angleA > 360) angleA -= 360;
            double angleB = dog + 180 - left;
            if (angleB > 360) angleB -= 360;

            if (angleA <= angleB)
            {
                p.NewDogAngle = Wrap(dog - (tail - left));
                p.NewTailAngle = left;
                p.SpindleRotateCalc = 1 + angleA / 360;
            }
            else
            {
                p.NewDogAngle = Wrap(dog + (180 - angleA));
                p.NewTailAngle = right;
                p.SpindleRotateCalc = 1 - (180 - angleA) / 360;
            }
        }

        if (p.SpindleRotateCalc == 1.0)
        {
            p.DogX = dogX;
            p.DogY = dogY;
            p.TailX = tailX;
            p.TailY = tailY;
        }
        else
        {
            p.DogX = dogLength * Math.Cos(p.NewDogAngle / 57.2957795);
            p.DogY = dogLength * Math.Sin(p.NewDogAngle / 57.2957795);
            p.TailX = tailLength * Math.Cos(p.NewTailAngle / 57.2957795);
            p.TailY = tailLength * Math.Sin(p.NewTailAngle / 57.2957795);
        }

        return p;
    }

    private static double Wrap(double degrees)
    {
        if (degrees < 0) return degrees + 360;
        if (degrees >= 360) return degrees - 360;
        return degrees;
    }
}
