using OptimizationXY.Charger.Models.Core;
using OptimizationXY.Charger.Models.Geometry;
using OptimizationXY.Charger.Models.Settings;

namespace OptimizationXY.Charger.Services.Acquisition;

/// <summary>
/// Describes how rotation angles were assigned to the frames of one scan.
/// </summary>
public sealed class RevolutionAngleResult
{
    /// <summary>Total frames handed to the assigner (valid or not).</summary>
    public int TotalFrames { get; set; }

    /// <summary>List position of the frame that represents 0 degrees.</summary>
    public int FirstRotatingFrame { get; set; }

    /// <summary>
    /// List position of the first frame that is no longer part of the revolution
    /// (the frame that represents 360 degrees). Exclusive.
    /// </summary>
    public int EndFrameExclusive { get; set; }

    /// <summary>Number of frame slots that were spread across 360 degrees.</summary>
    public int FramesPerRevolution { get; set; }

    /// <summary>
    /// Length of the revolution in frames, including the fraction of a frame
    /// found by RotatingScan.SubFrameRevolutionClosure (for example 84.6).
    /// Equal to FramesPerRevolution when the closure was not refined.
    /// </summary>
    public double RevolutionFrames { get; set; }

    /// <summary>
    /// Fraction of a frame (-1..1) by which the true 360 degree point lies
    /// after (+) or before (-) the closing frame. 0 when not refined.
    /// </summary>
    public double ClosureFrameOffset { get; set; }

    /// <summary>Angle assigned between two consecutive frame slots.</summary>
    public double AngleStepDeg { get; set; }

    /// <summary>
    /// True when the surface profile measurably returned to the profile seen at
    /// the start of the revolution, which proves a full 360 degree turn.
    /// </summary>
    public bool RevolutionVerified { get; set; }

    /// <summary>RMS difference between the start profile and the closing profile.</summary>
    public double ClosureErrorMM { get; set; } = double.NaN;

    /// <summary>Frames excluded because the log had not started turning yet.</summary>
    public int FramesExcludedBeforeRotation { get; set; }

    /// <summary>Frames excluded because the revolution was already complete.</summary>
    public int FramesExcludedAfterRotation { get; set; }

    /// <summary>Short name of the rule that produced the angles.</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Operator/engineering readable explanation.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Assigns the rotation angle of every scan frame from what the frames actually
/// show, instead of assuming a fixed number of degrees per frame.
///
/// The cameras deliver frames at a steady rate that is not tied to the log
/// rotation. A capture therefore normally contains: frames of the resting log,
/// one revolution, then more frames of the resting log. This class:
///   1. finds the last resting frame before the log starts to turn (0 degrees),
///   2. finds the frame where the surface profile returns to that starting
///      profile (360 degrees),
///   3. spreads the frame slots in between evenly across 360 degrees, and
///   4. excludes the resting frames before and after so they are not smeared
///      around the log.
///
/// Frames that are invalid for another reason (for example outside the
/// calibrated radius range) keep their slot, so they do not shift the angle of
/// the frames around them.
///
/// When the return to the starting profile cannot be found, the frames between
/// rotation start and rotation stop are divided evenly over 360 degrees and the
/// result is reported as not verified.
/// </summary>
public static class RevolutionAngleAssigner
{
    private const int MinimumCommonColumns = 50;

    public static RevolutionAngleResult Assign(
        List<ScanFrame> frames,
        RotatingScanSettings settings)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(settings);

        int count = frames.Count;

        var result = new RevolutionAngleResult
        {
            TotalFrames = count
        };

        if (count == 0)
        {
            result.Method = "NoFrames";
            result.Message = "No frames were available for angle assignment.";
            return result;
        }

        double stationaryToleranceMM =
            Math.Max(0.01, settings.StationaryProfileChangeMM);
        double closureToleranceMM =
            Math.Max(stationaryToleranceMM, settings.RevolutionClosureToleranceMM);
        int minimumFrames =
            Math.Max(3, settings.MinimumFramesPerRevolution);

        double[]?[] profiles = BuildProfiles(frames);

        List<int> profiled = [];
        for (int index = 0; index < count; index++)
        {
            if (profiles[index] is not null)
                profiled.Add(index);
        }

        if (profiled.Count < 3)
        {
            ApplyAngles(frames, 0, count, settings, result);
            result.Method = "AllFrames";
            result.Message =
                "Too few usable frames to detect rotation. All frames were divided evenly over 360 degrees.";
            return result;
        }

        // ------------------------------------------------------------
        // 1. Leading rest: the last frame that still matches the first
        //    usable frame is the 0 degree frame.
        // ------------------------------------------------------------
        int referenceFrame = profiled[0];
        int startPosition = 0;

        for (int position = 1; position < profiled.Count; position++)
        {
            bool same = IsWithin(
                profiles[referenceFrame],
                profiles[profiled[position]],
                stationaryToleranceMM);

            if (same)
            {
                startPosition = position;
                continue;
            }

            // One noisy frame must not end the resting period. Rotation has
            // started only when the following frame has moved as well.
            bool nextSame =
                position + 1 < profiled.Count &&
                IsWithin(
                    profiles[referenceFrame],
                    profiles[profiled[position + 1]],
                    stationaryToleranceMM);

            if (!nextSame)
                break;
        }

        int start = profiled[startPosition];

        if (profiled.Count - 1 - startPosition < minimumFrames)
        {
            // Every frame looks the same (round, centred block or no rotation).
            ApplyAngles(frames, 0, count, settings, result);
            result.Method = "AllFrames";
            result.Message =
                "No measurable change between frames, so rotation start and stop could not be detected. " +
                "All frames were divided evenly over 360 degrees.";
            return result;
        }

        // ------------------------------------------------------------
        // 2. Closure: the first frame, far enough into the scan, whose
        //    profile matches the 0 degree frame again. A match that is
        //    followed by a resting log (or the end of the capture) is
        //    preferred because the PLC cycle is one revolution then stop.
        // ------------------------------------------------------------
        int closurePosition = -1;
        double closureErrorMM = double.NaN;
        int firstCandidatePosition = -1;
        double firstCandidateErrorMM = double.NaN;

        for (int position = startPosition + 1; position < profiled.Count; position++)
        {
            if (profiled[position] - start < minimumFrames)
                continue;

            double difference = Difference(
                profiles[start],
                profiles[profiled[position]]);

            if (!(difference <= closureToleranceMM))
                continue;

            // Walk forward to the best match inside this dip.
            int best = position;
            double bestDifference = difference;

            while (best + 1 < profiled.Count)
            {
                double next = Difference(
                    profiles[start],
                    profiles[profiled[best + 1]]);

                if (next < bestDifference)
                {
                    best++;
                    bestDifference = next;
                }
                else
                {
                    break;
                }
            }

            if (firstCandidatePosition < 0)
            {
                firstCandidatePosition = best;
                firstCandidateErrorMM = bestDifference;
            }

            if (IsRestingAfter(
                    profiles,
                    profiled,
                    start,
                    best,
                    3.0 * closureToleranceMM))
            {
                closurePosition = best;
                closureErrorMM = bestDifference;
                break;
            }

            // Skip the remainder of this dip before looking for another one.
            position = best;
            while (position + 1 < profiled.Count &&
                   Difference(
                       profiles[start],
                       profiles[profiled[position + 1]]) <= closureToleranceMM)
            {
                position++;
            }
        }

        if (closurePosition < 0 && firstCandidatePosition >= 0)
        {
            closurePosition = firstCandidatePosition;
            closureErrorMM = firstCandidateErrorMM;
        }

        if (closurePosition >= 0)
        {
            int end = profiled[closurePosition];
            double offset = 0.0;
            string refinedText = string.Empty;

            if (settings.SubFrameRevolutionClosure)
            {
                offset = RefineClosureOffset(
                    profiles,
                    start,
                    end,
                    out double refinedErrorMM);

                if (offset != 0.0)
                {
                    refinedText =
                        $" Refined between frames: the start profile is matched {offset:+0.00;-0.00} frame from frame {end} " +
                        $"({refinedErrorMM:F2} mm RMS).";
                }
            }

            ApplyAngles(frames, start, end - start + offset, settings, result);
            result.ClosureFrameOffset = offset;
            result.RevolutionVerified = true;
            result.ClosureErrorMM = closureErrorMM;
            result.Method = "ProfileClosure";
            result.Message =
                $"One revolution found from frame {start} to frame {end} " +
                $"({result.RevolutionFrames:0.##} frames, {result.AngleStepDeg:F3} deg/frame). " +
                $"The surface returned to the starting profile within {closureErrorMM:F2} mm RMS." +
                refinedText;
            return result;
        }

        // ------------------------------------------------------------
        // 3. Fallback: divide the frames between rotation start and
        //    rotation stop evenly over 360 degrees.
        // ------------------------------------------------------------
        // The final rest begins at the earliest frame from which the log stays
        // close to its last measured position. A looser tolerance and a majority
        // rule are used because the block can settle slightly after it stops.
        int lastFrame = profiled[^1];
        int restPosition = profiled.Count - 1;
        double restToleranceMM = 3.0 * closureToleranceMM;

        for (int position = startPosition + 1; position < profiled.Count - 1; position++)
        {
            if (!IsWithin(
                    profiles[lastFrame],
                    profiles[profiled[position]],
                    restToleranceMM))
            {
                continue;
            }

            int resting = 0;
            int total = 0;

            for (int later = position; later < profiled.Count; later++)
            {
                total++;

                if (IsWithin(
                        profiles[lastFrame],
                        profiles[profiled[later]],
                        restToleranceMM))
                {
                    resting++;
                }
            }

            if (resting * 10 >= total * 8)
            {
                restPosition = position;
                break;
            }
        }

        int fallbackEnd =
            restPosition == profiled.Count - 1
                ? count
                : profiled[restPosition];

        if (fallbackEnd - start < minimumFrames)
        {
            start = 0;
            fallbackEnd = count;
        }

        ApplyAngles(frames, start, fallbackEnd, settings, result);
        result.RevolutionVerified = false;
        result.Method = "RotatingFramesEvenly";
        result.Message =
            $"The surface did not return to the starting profile, so a full revolution could not be verified. " +
            $"Frames {start} to {fallbackEnd - 1} ({result.FramesPerRevolution} frames) were divided evenly over 360 degrees.";
        return result;
    }

    /// <summary>
    /// Restores the legacy fixed-step angles (frame index x step). Used only when
    /// measured revolution angles are switched off in settings.
    /// </summary>
    public static RevolutionAngleResult AssignFixedStep(
        List<ScanFrame> frames,
        double stepDeg)
    {
        ArgumentNullException.ThrowIfNull(frames);

        for (int index = 0; index < frames.Count; index++)
        {
            frames[index].AngleDeg = index * stepDeg;
            frames[index].ExcludedFromRevolution = false;
        }

        return new RevolutionAngleResult
        {
            TotalFrames = frames.Count,
            FirstRotatingFrame = 0,
            EndFrameExclusive = frames.Count,
            FramesPerRevolution = stepDeg > 0 ? (int)Math.Round(360.0 / stepDeg) : 0,
            RevolutionFrames = stepDeg > 0 ? 360.0 / stepDeg : 0,
            AngleStepDeg = stepDeg,
            RevolutionVerified = false,
            Method = "FixedStep",
            Message =
                $"Measured revolution angles are disabled. Every frame was assigned a fixed {stepDeg:F3} degree step."
        };
    }

    private static void ApplyAngles(
        List<ScanFrame> frames,
        int start,
        int endExclusive,
        RotatingScanSettings settings,
        RevolutionAngleResult result)
    {
        int count = frames.Count;
        start = Math.Clamp(start, 0, Math.Max(0, count - 1));
        endExclusive = Math.Clamp(endExclusive, start + 1, count);

        ApplyAngles(frames, start, (double)(endExclusive - start), settings, result);
    }

    /// <summary>
    /// Spreads 360 degrees over a revolution that is <paramref name="revolutionFrames"/>
    /// frames long (it may include a fraction of a frame). Every frame whose angle
    /// is below 360 degrees is used; the rest are excluded.
    /// </summary>
    private static void ApplyAngles(
        List<ScanFrame> frames,
        int start,
        double revolutionFrames,
        RotatingScanSettings settings,
        RevolutionAngleResult result)
    {
        int count = frames.Count;
        start = Math.Clamp(start, 0, Math.Max(0, count - 1));

        if (!double.IsFinite(revolutionFrames) || revolutionFrames < 1.0)
            revolutionFrames = 1.0;

        // Frames start .. start + ceil(revolutionFrames) - 1 lie below 360 degrees.
        int endExclusive = Math.Clamp(
            start + (int)Math.Ceiling(revolutionFrames - 1e-9),
            start + 1,
            count);

        int slots = endExclusive - start;
        double stepDeg = 360.0 / revolutionFrames;
        double direction = settings.ReverseRotationDirection ? -1.0 : 1.0;
        double offsetDeg = double.IsFinite(settings.LaserAngleOffsetDeg)
            ? settings.LaserAngleOffsetDeg
            : 0.0;

        int excludedBefore = 0;
        int excludedAfter = 0;

        for (int index = 0; index < count; index++)
        {
            ScanFrame frame = frames[index];

            if (index >= start && index < endExclusive)
            {
                frame.AngleDeg = Normalize(
                    offsetDeg + direction * (index - start) * stepDeg);
                frame.ExcludedFromRevolution = false;
                continue;
            }

            // Keep a finite angle so saved records stay valid. These frames
            // are never used to build the point cloud.
            frame.AngleDeg = Normalize(offsetDeg);
            frame.ExcludedFromRevolution = true;

            if (index < start)
                excludedBefore++;
            else
                excludedAfter++;

            if (frame.IsValid)
            {
                frame.IsValid = false;
                frame.StatusMessage = index < start
                    ? "Excluded: log was not rotating yet."
                    : "Excluded: revolution was already complete.";
            }
        }

        result.FirstRotatingFrame = start;
        result.EndFrameExclusive = endExclusive;
        result.FramesPerRevolution = slots;
        result.RevolutionFrames = revolutionFrames;
        result.AngleStepDeg = stepDeg;
        result.FramesExcludedBeforeRotation = excludedBefore;
        result.FramesExcludedAfterRotation = excludedAfter;
    }

    /// <summary>
    /// The closing frame is the frame nearest to 360 degrees, but the true 360
    /// degree point normally lies between two frames. Treating the profile as
    /// changing linearly between neighbouring frames, this finds the fraction t
    /// (-1..1) at which it best matches the start profile:
    ///   minimise | p(end) + t (p(end +/- 1) - p(end)) - p(start) |.
    /// Returns 0 when neither neighbour gives a better match.
    /// </summary>
    internal static double RefineClosureOffset(
        double[]?[] profiles,
        int start,
        int end,
        out double refinedErrorMM)
    {
        double[]? reference = profiles[start];
        double[]? closing = profiles[end];
        refinedErrorMM = Difference(reference, closing);

        if (reference is null || closing is null || !double.IsFinite(refinedErrorMM))
            return 0.0;

        double bestOffset = 0.0;
        double bestError = refinedErrorMM;

        foreach (int direction in new[] { -1, 1 })
        {
            int neighbour = end + direction;

            if (neighbour <= start || neighbour >= profiles.Length)
                continue;

            double[]? next = profiles[neighbour];
            if (next is null)
                continue;

            int length = Math.Min(reference.Length, Math.Min(closing.Length, next.Length));
            double dotRD = 0.0;
            double dotDD = 0.0;
            int shared = 0;

            for (int x = 0; x < length; x++)
            {
                double r = closing[x] - reference[x];
                double d = next[x] - closing[x];

                if (double.IsNaN(r) || double.IsNaN(d))
                    continue;

                dotRD += r * d;
                dotDD += d * d;
                shared++;
            }

            if (shared < MinimumCommonColumns || dotDD <= 1e-12)
                continue;

            double t = Math.Clamp(-dotRD / dotDD, 0.0, 1.0);

            double sum = 0.0;
            for (int x = 0; x < length; x++)
            {
                double r = closing[x] - reference[x];
                double d = next[x] - closing[x];

                if (double.IsNaN(r) || double.IsNaN(d))
                    continue;

                double residual = r + t * d;
                sum += residual * residual;
            }

            double error = Math.Sqrt(sum / shared);

            if (error < bestError - 1e-9)
            {
                bestError = error;
                bestOffset = direction * t;
            }
        }

        // Never move the closure a whole frame: that frame would have been
        // chosen as the closing frame already.
        bestOffset = Math.Clamp(bestOffset, -0.99, 0.99);
        refinedErrorMM = bestError;
        return bestOffset;
    }

    private static double Normalize(double angleDeg)
    {
        angleDeg %= 360.0;

        if (angleDeg < 0.0)
            angleDeg += 360.0;

        return angleDeg;
    }

    private static double[]?[] BuildProfiles(List<ScanFrame> frames)
    {
        int maxX = -1;

        foreach (ScanFrame frame in frames)
        {
            if (!frame.IsValid || frame.Profile is null)
                continue;

            foreach (LogProfileColumn column in frame.Profile)
            {
                if (column.X > maxX)
                    maxX = column.X;
            }
        }

        double[]?[] profiles = new double[]?[frames.Count];

        if (maxX < 0)
            return profiles;

        for (int index = 0; index < frames.Count; index++)
        {
            ScanFrame frame = frames[index];

            if (!frame.IsValid ||
                frame.Profile is null ||
                frame.Profile.Count < MinimumCommonColumns)
            {
                continue;
            }

            double[] values = new double[maxX + 1];
            Array.Fill(values, double.NaN);

            foreach (LogProfileColumn column in frame.Profile)
            {
                if (column.X < 0 ||
                    column.X > maxX ||
                    !double.IsFinite(column.RadiusMM) ||
                    column.RadiusMM <= 0.0)
                {
                    continue;
                }

                values[column.X] = column.RadiusMM;
            }

            profiles[index] = values;
        }

        return profiles;
    }

    /// <summary>
    /// RMS radius difference between two frame profiles over the image columns
    /// they share. Returns NaN when they do not share enough columns.
    /// </summary>
    private static double Difference(double[]? left, double[]? right)
    {
        if (left is null || right is null)
            return double.NaN;

        int length = Math.Min(left.Length, right.Length);
        double sum = 0.0;
        int shared = 0;

        for (int x = 0; x < length; x++)
        {
            double a = left[x];
            double b = right[x];

            if (double.IsNaN(a) || double.IsNaN(b))
                continue;

            double delta = a - b;
            sum += delta * delta;
            shared++;
        }

        return shared >= MinimumCommonColumns
            ? Math.Sqrt(sum / shared)
            : double.NaN;
    }

    private static bool IsWithin(
        double[]? left,
        double[]? right,
        double toleranceMM)
    {
        // NaN compares false, so profiles that cannot be compared count as different.
        return Difference(left, right) <= toleranceMM;
    }

    private static bool IsRestingAfter(
        double[]?[] profiles,
        List<int> profiled,
        int startFrame,
        int closurePosition,
        double toleranceMM)
    {
        List<double> later = [];

        for (int position = closurePosition + 1; position < profiled.Count; position++)
        {
            double difference = Difference(
                profiles[startFrame],
                profiles[profiled[position]]);

            later.Add(double.IsNaN(difference) ? double.MaxValue : difference);
        }

        if (later.Count == 0)
            return true;

        later.Sort();
        double median = later[later.Count / 2];

        return median <= toleranceMM;
    }
}
