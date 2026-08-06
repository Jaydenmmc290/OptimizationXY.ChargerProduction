using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using OptimizationXY.Charger.Models.Geometry;


namespace OptimizationXY.Charger.Visualization;

/// <summary>
/// Builds a visual overlay of the rotating radius section from binned field data.
/// </summary>
public static class RotatingRadiusSectionOverlayBuilder
{

    /// <summary>
    /// Builds a visual overlay of the rotating radius section from binned field data.
    /// </summary>
    /// <param name="binnedField">The list of binned radius field points.</param>
    /// <param name="width">The width of the resulting overlay image.</param>
    /// <param name="height">The height of the resulting overlay image.</param>
    /// <returns>A Mat object representing the visual overlay.</returns>
    public static Mat BuildTailMiddleHeadFromBinnedField(
        List<BinnedRadiusFieldPoint> binnedField,
        int width = 500,
        int height = 500)
    {
        Mat canvas = new(height, width, DepthType.Cv8U, 3);
        canvas.SetTo(new MCvScalar(0, 0, 0));

        if (binnedField == null || binnedField.Count == 0)
            return canvas;

        var zBins = binnedField
            .Select(p => p.ZBinMM)
            .Distinct()
            .OrderBy(z => z)
            .ToList();

        if (zBins.Count < 3)
            return canvas;

        double tailZ = zBins[(int)(zBins.Count * 0.15)];
        double midZ = zBins[(int)(zBins.Count * 0.50)];
        double headZ = zBins[(int)(zBins.Count * 0.85)];

        double maxRadius = binnedField
            .Where(p => p.MeanRadiusMM > 0)
            .Max(p => p.MeanRadiusMM);

        if (maxRadius <= 0)
            maxRadius = 1.0;

        int centerX = width / 2;
        int centerY = height / 2;

        double scale = Math.Min(width, height) * 0.40 / maxRadius;

        DrawGrid(canvas, centerX, centerY, scale, maxRadius);

        DrawSection(
            canvas,
            GetNearestSlice(binnedField, tailZ),
            centerX,
            centerY,
            scale,
            new MCvScalar(0, 0, 255),
            "TAIL");

        DrawSection(
            canvas,
            GetNearestSlice(binnedField, midZ),
            centerX,
            centerY,
            scale,
            new MCvScalar(0, 255, 0),
            "MIDDLE");

        DrawSection(
            canvas,
            GetNearestSlice(binnedField, headZ),
            centerX,
            centerY,
            scale,
            new MCvScalar(255, 0, 0),
            "HEAD");

        CvInvoke.Circle(
            canvas,
            new Point(centerX, centerY),
            3,
            new MCvScalar(255, 255, 255),
            -1);

        CvInvoke.PutText(
            canvas,
            "BINNED ROTATING SCAN CROSS-SECTIONS",
            new Point(10, 20),
            FontFace.HersheySimplex,
            0.45,
            new MCvScalar(255, 255, 255),
            1);

        CvInvoke.PutText(canvas, "TAIL", new Point(10, 45),
            FontFace.HersheySimplex, 0.45,
            new MCvScalar(0, 0, 255), 1);

        CvInvoke.PutText(canvas, "MIDDLE", new Point(10, 65),
            FontFace.HersheySimplex, 0.45,
            new MCvScalar(0, 255, 0), 1);

        CvInvoke.PutText(canvas, "HEAD", new Point(10, 85),
            FontFace.HersheySimplex, 0.45,
            new MCvScalar(255, 0, 0), 1);

        return canvas;
    }

    /// <summary>
    /// Gets the nearest slice of binned radius field points for a given target Z value.
    /// </summary>
    /// <param name="field">The list of binned radius field points.</param>
    /// <param name="targetZ">The target Z value to find the nearest slice for.</param>
    /// <returns>A list of binned radius field points representing the nearest slice.</returns>
    private static List<BinnedRadiusFieldPoint> GetNearestSlice(
        List<BinnedRadiusFieldPoint> field,
        double targetZ)
    {
        double nearestZ = field
            .Select(p => p.ZBinMM)
            .Distinct()
            .OrderBy(z => Math.Abs(z - targetZ))
            .First();

        return [.. field
            .Where(p => Math.Abs(p.ZBinMM - nearestZ) < 0.001)
            .Where(p => p.MeanRadiusMM > 0)
            .OrderBy(p => p.AngleBinDeg)];
    }

    /// <summary>
    /// Draws a section of the binned radius field on the canvas.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="slice">The slice of binned radius field points to draw.</param>
    /// <param name="centerX">The X coordinate of the center of the overlay.</param>
    /// <param name="centerY">The Y coordinate of the center of the overlay.</param>
    /// <param name="scale">The scale factor for converting radius to screen coordinates.</param>
    /// <param name="color">The color to use for drawing the section.</param>
    /// <param name="label">The label for the section.</param>
    private static void DrawSection(
            Mat canvas,
            List<BinnedRadiusFieldPoint> slice,
            int centerX,
            int centerY,
            double scale,
            MCvScalar color,
            string label)
    {
        if (slice == null || slice.Count < 6)
            return;

        // Build visual-only points.
        // This does not change the real binned field.
        var points = BuildVisualSectionPoints(
            slice,
            centerX,
            centerY,
            scale,
            normalAngleStepDeg: 10.0,
            maxFillGapDeg: 30.0);

        if (points.Count < 6)
            return;

        // Draw visual points.
        foreach (var p in points)
        {
            CvInvoke.Circle(
                canvas,
                p.Screen,
                p.IsEstimated ? 1 : 2,
                color,
                -1);
        }

        // Draw connected visual outline.
        for (int i = 1; i < points.Count; i++)
        {
            double gap = Math.Abs(points[i].Angle - points[i - 1].Angle);

            if (gap <= 15.0)
            {
                CvInvoke.Line(
                    canvas,
                    points[i - 1].Screen,
                    points[i].Screen,
                    color,
                    1);
            }
        }

        double wrapGap = 360.0 - points.Last().Angle + points.First().Angle;

        if (wrapGap <= 15.0)
        {
            CvInvoke.Line(
                canvas,
                points.Last().Screen,
                points.First().Screen,
                color,
                1);
        }
    }

    /// <summary>
    /// Builds a list of visual section points from a slice of binned radius field points.
    /// </summary>
    /// <param name="slice">The slice of binned radius field points.</param>
    /// <param name="centerX">The X coordinate of the center of the overlay.</param>
    /// <param name="centerY">The Y coordinate of the center of the overlay.</param>
    /// <param name="scale">The scale factor for converting radius to screen coordinates.</param>
    /// <param name="normalAngleStepDeg">The normal angle step in degrees.</param>
    /// <param name="maxFillGapDeg">The maximum fill gap in degrees.</param>
    /// <returns>A list of visual section points.</returns>
    private static List<(Point Screen, double Angle, double Radius, bool IsEstimated)> BuildVisualSectionPoints(
List<BinnedRadiusFieldPoint> slice,
int centerX,
int centerY,
double scale,
double normalAngleStepDeg,
double maxFillGapDeg)
    {
        var measured = slice
            .Where(p => p.MeanRadiusMM > 0)
            .OrderBy(p => p.AngleBinDeg)
            .Select(p => new
            {
                Angle = NormalizeAngle(p.AngleBinDeg),
                Radius = p.MeanRadiusMM
            })
            .OrderBy(p => p.Angle)
            .ToList();

        var visual = new List<(double Angle, double Radius, bool IsEstimated)>();

        if (measured.Count == 0)
            return [];

        for (int i = 0; i < measured.Count; i++)
        {
            var current = measured[i];

            visual.Add((current.Angle, current.Radius, false));

            var next = i == measured.Count - 1
                ? measured[0]
                : measured[i + 1];

            double nextAngle = next.Angle;

            if (i == measured.Count - 1)
                nextAngle += 360.0;

            double gap = nextAngle - current.Angle;

            // Only fill small missing gaps.
            // Do not invent a large missing section.
            if (gap <= normalAngleStepDeg || gap > maxFillGapDeg)
                continue;

            int missingSteps = (int)Math.Round(gap / normalAngleStepDeg) - 1;

            for (int step = 1; step <= missingSteps; step++)
            {
                double t = (double)step / (missingSteps + 1);

                double filledAngle = current.Angle + normalAngleStepDeg * step;
                double filledRadius = current.Radius + (next.Radius - current.Radius) * t;

                visual.Add((NormalizeAngle(filledAngle), filledRadius, true));
            }
        }

        return [.. visual
            .OrderBy(p => p.Angle)
            .Select(p =>
            {
                double theta = p.Angle * Math.PI / 180.0;

                int x = centerX + (int)(p.Radius * Math.Cos(theta) * scale);
                int y = centerY - (int)(p.Radius * Math.Sin(theta) * scale);

                return (
                    Screen: new Point(x, y),
                     p.Angle,
                     p.Radius,
                     p.IsEstimated
                );
            })];
    }

    /// <summary>
    /// Normalizes an angle to the range [0, 360) degrees.
    /// </summary>
    /// <param name="angle">The angle in degrees to normalize.</param>
    /// <returns>The normalized angle in the range [0, 360) degrees.</returns>
    private static double NormalizeAngle(double angle)
    {
        angle %= 360.0;

        if (angle < 0)
            angle += 360.0;

        return angle;
    }

    /// <summary>
    /// Draws a grid on the given canvas.
    /// </summary>
    /// <param name="canvas">The canvas on which to draw the grid.</param>
    /// <param name="centerX">The X coordinate of the center of the grid.</param>
    /// <param name="centerY">The Y coordinate of the center of the grid.</param>
    /// <param name="scale">The scale factor for converting radius to screen coordinates.</param>
    /// <param name="maxRadius">The maximum radius of the grid.</param>
    private static void DrawGrid(
        Mat canvas,
        int centerX,
        int centerY,
        double scale,
        double maxRadius)
    {
        for (double r = 25.0; r <= maxRadius; r += 25.0)
        {
            CvInvoke.Circle(
                canvas,
                new Point(centerX, centerY),
                (int)(r * scale),
                new MCvScalar(35, 35, 35),
                1);
        }

        CvInvoke.Line(
            canvas,
            new Point(0, centerY),
            new Point(canvas.Width, centerY),
            new MCvScalar(60, 60, 60),
            1);

        CvInvoke.Line(
            canvas,
            new Point(centerX, 0),
            new Point(centerX, canvas.Height),
            new MCvScalar(60, 60, 60),
            1);
    }
}
