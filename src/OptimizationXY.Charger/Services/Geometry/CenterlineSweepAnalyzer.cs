using OptimizationXY.Charger.Models.Analysis;
using OptimizationXY.Charger.Models.Geometry;
using OptimizationXY.Charger.Models.Settings;

namespace OptimizationXY.Charger.Services.Geometry;

/// <summary>
/// Measures how crooked the log is from its local centre slices: the
/// end-to-end shift of the centre (sweep) and how far the middle of the log
/// bends away from a straight end-to-end line (bow). Diagnostic only - the
/// numbers are reported and saved, they do not move the spindles.
/// </summary>
public static class CenterlineSweepAnalyzer
{
    /// <summary>
    /// Analyzes a list of LocalCenterlineSlice objects to determine the sweep characteristics of the centerline.
    /// </summary>
    /// <param name="slices">The list of LocalCenterlineSlice objects to analyze.</param>
    /// <param name="settings">The sweep settings to use for the analysis.</param>
    /// <returns>A CenterlineSweepResult object containing the sweep analysis results.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the input list of slices is null or contains fewer than two slices.</exception>
    public static CenterlineSweepResult Analyze(List<LocalCenterlineSlice> slices, SweepSettings settings)
    {
        if (slices == null || slices.Count < 2)
            throw new InvalidOperationException("Need at least two centerline slices.");

        // Sort slices along the log (Z). Lowest Z is treated as the "tail" end and
        // highest Z as the "head" end for this analysis.
        var ordered = slices.OrderBy(s => s.ZMM).ToList();

        // How many slices to average at each end: EndSliceFraction of all slices,
        // but never fewer than MinimumEndSlices. Averaging several slices stops one
        // noisy slice from deciding the end position.
        int endCount = Math.Max(
  settings.MinimumEndSlices,
  (int)(ordered.Count * settings.EndSliceFraction));

        var tailGroup = ordered.Take(endCount).ToList();
        var headGroup = ordered.Skip(ordered.Count - endCount).ToList();

        double tailX = tailGroup.Average(s => s.CenterXMM);
        double tailY = tailGroup.Average(s => s.CenterYMM);
        double tailZ = tailGroup.Average(s => s.ZMM);

        double headX = headGroup.Average(s => s.CenterXMM);
        double headY = headGroup.Average(s => s.CenterYMM);
        double headZ = headGroup.Average(s => s.ZMM);

        // Sweep = signed change of the centre from tail end to head end (mm).
        double xSweep = headX - tailX;
        double ySweep = headY - tailY;

        double maxBow = 0.0;
        double totalBow = 0.0;
        double worstBowZ = tailZ;

        // Bow: distance of every slice centre from the straight tail-to-head line,
        // measured in the X/Y plane (looking down the log). Track the largest
        // distance, where along the log it happened, and the running total for
        // the average.
        // NOTE: because this uses the distance to the infinite 2D line through
        // the two end centres (Z is ignored), a bend that lies in the same
        // direction as the end-to-end shift is not counted as bow. A full 3D
        // point-to-segment distance would also catch that case.
        foreach (var s in ordered)
        {
            double bow = DistancePointToLine2D(
                s.CenterXMM,
                s.CenterYMM,
                tailX,
                tailY,
                headX,
                headY);

            totalBow += bow;

            if (bow > maxBow)
            {
                maxBow = bow;
                worstBowZ = s.ZMM;
            }
        }

        return new CenterlineSweepResult
        {
            TailCenterXMM = tailX,
            TailCenterYMM = tailY,

            HeadCenterXMM = headX,
            HeadCenterYMM = headY,

            XSweepMM = xSweep,
            YSweepMM = ySweep,

            TotalSweepMM = Math.Sqrt(xSweep * xSweep + ySweep * ySweep),

            MaxBowSweepMM = maxBow,
            AverageBowSweepMM = totalBow / ordered.Count,
            WorstBowZMM = worstBowZ
        };
    }

    /// <summary>
    /// Calculates the perpendicular distance from a point (px, py) to a line defined by two points (ax, ay) and (bx, by) in 2D space.
    /// </summary>
    /// <param name="px">The X-coordinate of the point.</param>
    /// <param name="py">The Y-coordinate of the point.</param>
    /// <param name="ax">The X-coordinate of the first point defining the line.</param>
    /// <param name="ay">The Y-coordinate of the first point defining the line.</param>
    /// <param name="bx">The X-coordinate of the second point defining the line.</param>
    /// <param name="by">The Y-coordinate of the second point defining the line.</param>
    /// <returns>The perpendicular distance from the point to the line.</returns>
    private static double DistancePointToLine2D(
        double px,
        double py,
        double ax,
        double ay,
        double bx,
        double by)
    {
        double abx = bx - ax;
        double aby = by - ay;

        double apx = px - ax;
        double apy = py - ay;

        double abLength = Math.Sqrt(abx * abx + aby * aby);

        // Both end centres are (almost) the same point: no line exists, so return
        // the plain distance from that point.
        if (abLength <= 0.000001)
            return Math.Sqrt(apx * apx + apy * apy);

        // |AB x AP| is twice the area of triangle A-B-P; dividing by |AB| (the
        // base) gives the triangle's height = perpendicular distance.
        double cross = Math.Abs(abx * apy - aby * apx);

        return cross / abLength;
    }
}
