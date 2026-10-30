using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using OptimizationXY.Charger.Models.Defects;


namespace OptimizationXY.Charger.Visualization;
/// <summary>
/// Builds a heatmap visualization of radius defects based on a RadiusDefectMap.
/// <para>
/// X axis = position along the block (Z, mm); Y axis = angle (0–360°). Each
/// cell is coloured by its radius error (measured radius minus the expected
/// smooth radius): blue at -4 mm or lower, cyan between -4 and -2 mm, dark grey
/// otherwise. Only LOW spots (dents, missing wood) are coloured; high spots
/// are drawn grey like good wood. Diagnostic picture only.
/// </para>
/// </summary>
public static class RadiusDefectHeatmapBuilder
{
    /// <summary>
    /// Builds a heatmap visualization of radius defects based on a RadiusDefectMap.
    /// </summary>
    /// <param name="map">The RadiusDefectMap containing defect data.</param>
    /// <param name="width">The width of the resulting heatmap image.</param>
    /// <param name="height">The height of the resulting heatmap image.</param>
    /// <returns>A Mat object representing the heatmap visualization.</returns>
    public static Mat Build(
        RadiusDefectMap map,
        int width = 1100,
        int height = 650)
    {
        Mat canvas = new(height, width, DepthType.Cv8U, 3);
        canvas.SetTo(new MCvScalar(0, 0, 0));

        if (map == null || map.Points == null || map.Points.Count == 0)
            return canvas;

        int marginLeft = 80;
        int marginRight = 160;
        int marginTop = 60;
        int marginBottom = 70;

        int plotW = width - marginLeft - marginRight;
        int plotH = height - marginTop - marginBottom;

        double minZ = map.Points.Min(p => p.ZBinMM);
        double maxZ = map.Points.Max(p => p.ZBinMM);
        double minAngle = 0.0;
        double maxAngle = 360.0;

        double zRange = Math.Max(maxZ - minZ, 1.0);

        Point MapPoint(double z, double angle)
        {
            int x = marginLeft + (int)((z - minZ) / zRange * plotW);
            int y = marginTop + plotH - (int)((angle - minAngle) / (maxAngle - minAngle) * plotH);
            return new Point(x, y);
        }

        foreach (var p in map.Points)
        {
            var pt = MapPoint(p.ZBinMM, p.AngleBinDeg);

            MCvScalar color = ErrorToColor(p.RadiusErrorMM);

            CvInvoke.Rectangle(
                canvas,
                new Rectangle(pt.X, pt.Y, 6, 6),
                color,
                -1);
        }

        // Plot border
        CvInvoke.Rectangle(
            canvas,
            new Rectangle(marginLeft, marginTop, plotW, plotH),
            new MCvScalar(180, 180, 180),
            1);

        // Title
        CvInvoke.PutText(
            canvas,
            "RADIUS DEFECT HEATMAP",
            new Point(30, 35),
            FontFace.HersheySimplex,
            0.9,
            new MCvScalar(255, 255, 255),
            2);

        // Axis labels
        CvInvoke.PutText(
            canvas,
            "Z Position (mm)",
            new Point(marginLeft + plotW / 2 - 100, height - 20),
            FontFace.HersheySimplex,
            0.6,
            new MCvScalar(255, 255, 255),
            1);

        CvInvoke.PutText(
            canvas,
            "Angle",
            new Point(10, marginTop + plotH / 2),
            FontFace.HersheySimplex,
            0.6,
            new MCvScalar(255, 255, 255),
            1);

        // Basic axis ticks
        for (int i = 0; i <= 5; i++)
        {
            double z = minZ + zRange * i / 5.0;
            int x = marginLeft + (int)(plotW * i / 5.0);

            CvInvoke.Line(
                canvas,
                new Point(x, marginTop + plotH),
                new Point(x, marginTop + plotH + 6),
                new MCvScalar(180, 180, 180),
                1);

            CvInvoke.PutText(
                canvas,
                $"{z:F0}",
                new Point(x - 20, marginTop + plotH + 25),
                FontFace.HersheySimplex,
                0.45,
                new MCvScalar(220, 220, 220),
                1);
        }

        for (int a = 0; a <= 360; a += 45)
        {
            int y = marginTop + plotH - (int)(a / 360.0 * plotH);

            CvInvoke.Line(
                canvas,
                new Point(marginLeft - 6, y),
                new Point(marginLeft, y),
                new MCvScalar(180, 180, 180),
                1);

            CvInvoke.PutText(
                canvas,
                $"{a}",
                new Point(35, y + 5),
                FontFace.HersheySimplex,
                0.45,
                new MCvScalar(220, 220, 220),
                1);
        }

        DrawLegend(canvas, width - marginRight + 35, marginTop);

        return canvas;
    }

    /// <summary>
    /// Maps a radius error value in millimeters to a corresponding color for visualization.
    /// </summary>
    /// <param name="errorMM">The radius error value in millimeters.</param>
    /// <returns>A MCvScalar representing the color corresponding to the error value.</returns>
    private static MCvScalar ErrorToColor(double errorMM)
    {
        // BGR colors for Emgu/OpenCV

        if (errorMM <= -4.0)
            return new MCvScalar(255, 0, 0);       // severe low = blue

        if (errorMM <= -2.0)
            return new MCvScalar(255, 180, 0);     // low = cyan/blue

        if (errorMM >= 4.0)
            return new MCvScalar(45, 45, 45);

        if (errorMM >= 2.0)
            return new MCvScalar(45, 45, 45);

        return new MCvScalar(45, 45, 45);          // good = dark gray
    }

    /// <summary>
    /// Draws the legend for the heatmap visualization on the provided canvas at the specified coordinates.
    /// </summary>
    /// <param name="canvas">The canvas on which to draw the legend.</param>
    /// <param name="x">The x-coordinate of the top-left corner of the legend.</param>
    /// <param name="y">The y-coordinate of the top-left corner of the legend.</param>
    private static void DrawLegend(Mat canvas, int x, int y)
    {
        CvInvoke.PutText(
            canvas,
            "ERROR",
            new Point(x, y),
            FontFace.HersheySimplex,
            0.55,
            new MCvScalar(255, 255, 255),
            1);

        DrawLegendItem(canvas, x, y + 35, new MCvScalar(255, 0, 0), "<= -4 mm");
        DrawLegendItem(canvas, x, y + 70, new MCvScalar(255, 180, 0), "-4 to -2");
        DrawLegendItem(canvas, x, y + 105, new MCvScalar(45, 45, 45), "Good");

    }

    /// <summary>
    /// Draws a single legend item on the canvas at the specified coordinates with the given color and text.
    /// </summary>
    /// <param name="canvas">The canvas on which to draw the legend item.</param>
    /// <param name="x">The x-coordinate of the top-left corner of the legend item.</param>
    /// <param name="y">The y-coordinate of the top-left corner of the legend item.</param>
    /// <param name="color">The color of the legend item.</param>
    /// <param name="text">The text label of the legend item.</param>
    private static void DrawLegendItem(Mat canvas, int x, int y, MCvScalar color, string text)
    {
        CvInvoke.Rectangle(
            canvas,
            new Rectangle(x, y - 15, 22, 22),
            color,
            -1);

        CvInvoke.PutText(
            canvas,
            text,
            new Point(x + 32, y + 2),
            FontFace.HersheySimplex,
            0.45,
            new MCvScalar(230, 230, 230),
            1);
    }
}
