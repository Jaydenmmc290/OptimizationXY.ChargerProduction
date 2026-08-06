using OptimizationXY.Charger.Models.Analysis;
using OptimizationXY.Charger.Models.Geometry;
using OptimizationXY.Charger.Models.Settings;

namespace OptimizationXY.Charger.Services.Geometry;

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

        var ordered = slices.OrderBy(s => s.ZMM).ToList();

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

        double xSweep = headX - tailX;
        double ySweep = headY - tailY;

        double maxBow = 0.0;
        double totalBow = 0.0;
        double worstBowZ = tailZ;

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

        if (abLength <= 0.000001)
            return Math.Sqrt(apx * apx + apy * apy);

        double cross = Math.Abs(abx * apy - aby * apx);

        return cross / abLength;
    }
}
