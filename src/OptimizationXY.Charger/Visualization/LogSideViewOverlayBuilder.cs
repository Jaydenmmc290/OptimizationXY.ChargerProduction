using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using OptimizationXY.Charger.Models.Geometry;


namespace OptimizationXY.Charger.Visualization;

/// <summary>
/// Builds the single side-view picture of a scanned block that is shown in the
/// log overlay box on the main form.
///
/// The block is drawn lying down: TAIL (far from the operator) on the left,
/// HEAD (operator side) on the right. Up on the picture is -Y and down is +Y,
/// the same as the machine. The light band is the widest the block gets at that
/// point along its length and the dark band is the narrowest.
///
/// The text is deliberately large because the picture is shrunk to fit a small
/// box. This is drawing only: it reads the binned radius field the pipeline has
/// already produced and adds no measurement work of its own.
/// </summary>
public static class LogSideViewOverlayBuilder
{
    private const double MillimetersPerInch = 25.4;

    // A slice needs at least this many angle bins to be drawn as measured.
    private const int MinimumBinsPerSlice = 12;

    private static readonly MCvScalar White = new(255, 255, 255);
    private static readonly MCvScalar LightGray = new(200, 200, 200);
    private static readonly MCvScalar MidGray = new(130, 130, 130);
    private static readonly MCvScalar GridGray = new(60, 60, 60);
    private static readonly MCvScalar WoodLight = new(150, 200, 225);
    private static readonly MCvScalar WoodDark = new(95, 150, 190);
    private static readonly MCvScalar OutlineColor = new(225, 240, 250);
    private static readonly MCvScalar MissingColor = new(60, 60, 200);      // red (BGR)
    private static readonly MCvScalar LargestColor = new(0, 215, 255);      // amber (BGR)
    private static readonly MCvScalar SmallestColor = new(255, 200, 120);   // light blue (BGR)
    private static readonly MCvScalar SpindleColor = new(0, 255, 255);      // yellow (BGR)
    private static readonly MCvScalar CenterlineColor = new(255, 170, 60);  // blue (BGR)

    private static readonly MCvScalar TailColor = new(70, 70, 255);         // red (BGR)
    private static readonly MCvScalar MiddleColor = new(70, 230, 70);       // green (BGR)
    private static readonly MCvScalar HeadColor = new(255, 150, 60);        // blue (BGR)

    private sealed class Slice
    {
        public double ZMM { get; init; }
        public double MeanRadiusMM { get; init; }
        public double MinRadiusMM { get; init; }
        public double MaxRadiusMM { get; init; }

        // Block centre relative to the spindle axis at this slice, in mm.
        public double CenterXMM { get; init; }
        public double CenterYMM { get; init; }
    }

    /// <summary>
    /// Builds the side-view picture.
    /// </summary>
    /// <param name="binnedField">Binned radius field measured from the fitted block centreline.</param>
    /// <param name="centerline">
    /// Fitted block centreline in the machine frame. When supplied the block is drawn
    /// where it actually sits relative to the spindle axis.
    /// </param>
    /// <param name="width">Picture width in pixels.</param>
    /// <param name="height">Picture height in pixels.</param>
    public static Mat Build(
        List<BinnedRadiusFieldPoint> binnedField,
        CenterlineResult? centerline = null,
        int width = 640,
        int height = 620)
    {
        width = Math.Max(480, width);
        height = Math.Max(480, height);

        Mat canvas = new(height, width, DepthType.Cv8U, 3);
        canvas.SetTo(new MCvScalar(0, 0, 0));

        if (binnedField == null || binnedField.Count == 0)
        {
            Text(canvas, "No scan data", 40, height / 2, 0.9, LightGray, 2);
            return canvas;
        }

        bool hasCenterline = centerline is not null && Math.Abs(centerline.DirectionZ) > 1e-9;

        List<Slice> slices = [.. binnedField
            .Where(p => p.MeanRadiusMM > 0 && double.IsFinite(p.MeanRadiusMM))
            .GroupBy(p => p.ZBinMM)
            .Where(group => group.Count() >= MinimumBinsPerSlice)
            .Select(group =>
            {
                double centerX = 0.0;
                double centerY = 0.0;

                if (hasCenterline)
                {
                    double along = (group.Key - centerline!.OriginZ) / centerline.DirectionZ;
                    centerX = centerline.OriginX + centerline.DirectionX * along;
                    centerY = centerline.OriginY + centerline.DirectionY * along;

                    if (!double.IsFinite(centerX) || !double.IsFinite(centerY))
                    {
                        centerX = 0.0;
                        centerY = 0.0;
                    }
                }

                return new Slice
                {
                    ZMM = group.Key,
                    MeanRadiusMM = group.Average(p => p.MeanRadiusMM),
                    MinRadiusMM = group.Min(p => p.MeanRadiusMM),
                    MaxRadiusMM = group.Max(p => p.MeanRadiusMM),
                    CenterXMM = centerX,
                    CenterYMM = centerY
                };
            })
            .OrderBy(slice => slice.ZMM)];

        if (slices.Count < 3)
        {
            Text(canvas, "Not enough scan data", 40, height / 2, 0.9, LightGray, 2);
            return canvas;
        }

        double minZ = slices[0].ZMM;
        double maxZ = slices[^1].ZMM;
        double lengthMM = Math.Max(1.0, maxZ - minZ);

        // Typical spacing between slices; a jump of more than twice this is a gap.
        double sliceSpacingMM = lengthMM / (slices.Count - 1);
        double gapLimitMM = sliceSpacingMM * 2.5;

        // Layout.
        int left = 44;
        int right = width - 28;
        int top = 150;
        int bottom = height - 220;
        int areaWidth = right - left;
        int areaHeight = bottom - top;
        int axisY = top + areaHeight / 2;

        double extentMM = slices.Max(slice => slice.MaxRadiusMM + Math.Abs(slice.CenterYMM));
        if (!double.IsFinite(extentMM) || extentMM <= 0)
            extentMM = 1.0;

        double pixelsPerMM = areaHeight * 0.46 / extentMM;

        int ToX(double zMM) => left + (int)Math.Round((zMM - minZ) / lengthMM * areaWidth);
        double CenterY(Slice slice) => axisY + slice.CenterYMM * pixelsPerMM;

        // ------------------------------------------------------------
        // Header: TAIL | MIDDLE | HEAD with the average diameter of each third
        // ------------------------------------------------------------

        double third = areaWidth / 3.0;

        (string Name, string Note, MCvScalar Color)[] sections =
        [
            ("TAIL", "far side", TailColor),
            ("MIDDLE", string.Empty, MiddleColor),
            ("HEAD", "operator", HeadColor)
        ];

        for (int index = 0; index < sections.Length; index++)
        {
            var section = sections[index];
            int middleX = left + (int)Math.Round(third * (index + 0.5));

            double fromZ = minZ + lengthMM * index / 3.0;
            double toZ = minZ + lengthMM * (index + 1) / 3.0;

            List<Slice> inSection = [.. slices.Where(slice => slice.ZMM >= fromZ && slice.ZMM <= toZ)];

            CenteredText(canvas, section.Name, middleX, 44, 1.0, section.Color, 2);

            if (section.Note.Length > 0)
                CenteredText(canvas, section.Note, middleX, 70, 0.6, LightGray, 1);

            if (inSection.Count > 0)
            {
                double diameterInches = Inches(inSection.Average(slice => slice.MeanRadiusMM) * 2.0);
                CenteredText(canvas, $"{diameterInches:F1} in", middleX, 104, 0.85, White, 2);
            }

            if (index > 0)
            {
                int dividerX = left + (int)Math.Round(third * index);
                DrawDashed(canvas, new Point(dividerX, 14), new Point(dividerX, bottom + 6), GridGray, 6, 6, 1);
            }
        }

        CenteredText(canvas, "average diameter", left + areaWidth / 2, 128, 0.5, MidGray, 1);

        // ------------------------------------------------------------
        // Body: one vertical stroke per pixel column
        // ------------------------------------------------------------

        int sliceIndex = 0;

        for (int pixelX = left; pixelX <= right; pixelX++)
        {
            double zMM = minZ + (pixelX - left) / (double)areaWidth * lengthMM;

            // Slices are sorted, so the nearest one only ever moves forward.
            while (sliceIndex + 1 < slices.Count &&
                   Math.Abs(slices[sliceIndex + 1].ZMM - zMM) <= Math.Abs(slices[sliceIndex].ZMM - zMM))
            {
                sliceIndex++;
            }

            Slice slice = slices[sliceIndex];

            if (Math.Abs(slice.ZMM - zMM) > gapLimitMM)
            {
                // No measurement here: show it instead of drawing wood that was not seen.
                CvInvoke.Line(canvas, new Point(pixelX, top), new Point(pixelX, bottom), MissingColor, 1);
                continue;
            }

            double centerY = CenterY(slice);

            CvInvoke.Line(
                canvas,
                new Point(pixelX, (int)Math.Round(centerY - slice.MaxRadiusMM * pixelsPerMM)),
                new Point(pixelX, (int)Math.Round(centerY + slice.MaxRadiusMM * pixelsPerMM)),
                WoodLight,
                1);

            CvInvoke.Line(
                canvas,
                new Point(pixelX, (int)Math.Round(centerY - slice.MinRadiusMM * pixelsPerMM)),
                new Point(pixelX, (int)Math.Round(centerY + slice.MinRadiusMM * pixelsPerMM)),
                WoodDark,
                1);
        }

        // Outline along the top and bottom of the block.
        for (int index = 0; index + 1 < slices.Count; index++)
        {
            Slice current = slices[index];
            Slice next = slices[index + 1];

            if (next.ZMM - current.ZMM > gapLimitMM)
                continue;

            double currentCenter = CenterY(current);
            double nextCenter = CenterY(next);

            CvInvoke.Line(
                canvas,
                new Point(ToX(current.ZMM), (int)Math.Round(currentCenter - current.MaxRadiusMM * pixelsPerMM)),
                new Point(ToX(next.ZMM), (int)Math.Round(nextCenter - next.MaxRadiusMM * pixelsPerMM)),
                OutlineColor,
                2,
                LineType.AntiAlias);

            CvInvoke.Line(
                canvas,
                new Point(ToX(current.ZMM), (int)Math.Round(currentCenter + current.MaxRadiusMM * pixelsPerMM)),
                new Point(ToX(next.ZMM), (int)Math.Round(nextCenter + next.MaxRadiusMM * pixelsPerMM)),
                OutlineColor,
                2,
                LineType.AntiAlias);
        }

        // Close both ends.
        foreach (Slice end in new[] { slices[0], slices[^1] })
        {
            double endCenter = CenterY(end);

            CvInvoke.Line(
                canvas,
                new Point(ToX(end.ZMM), (int)Math.Round(endCenter - end.MaxRadiusMM * pixelsPerMM)),
                new Point(ToX(end.ZMM), (int)Math.Round(endCenter + end.MaxRadiusMM * pixelsPerMM)),
                OutlineColor,
                2,
                LineType.AntiAlias);
        }

        // ------------------------------------------------------------
        // Spindle axis and block centreline
        // ------------------------------------------------------------

        if (hasCenterline)
        {
            DrawDashed(canvas, new Point(left - 34, axisY), new Point(right + 20, axisY), SpindleColor, 14, 8, 2);
        }

        CvInvoke.Line(
            canvas,
            new Point(ToX(slices[0].ZMM), (int)Math.Round(CenterY(slices[0]))),
            new Point(ToX(slices[^1].ZMM), (int)Math.Round(CenterY(slices[^1]))),
            CenterlineColor,
            3,
            LineType.AntiAlias);

        // ------------------------------------------------------------
        // Largest and smallest diameter
        // ------------------------------------------------------------

        Slice largest = slices.OrderByDescending(slice => slice.MeanRadiusMM).First();
        Slice smallest = slices.OrderBy(slice => slice.MeanRadiusMM).First();

        DrawDiameterMarker(canvas, ToX(largest.ZMM), CenterY(largest), largest.MaxRadiusMM * pixelsPerMM, LargestColor, 3, 5);
        DrawDiameterMarker(canvas, ToX(smallest.ZMM), CenterY(smallest), smallest.MaxRadiusMM * pixelsPerMM, SmallestColor, 10, 6);

        // ------------------------------------------------------------
        // Ruler in inches from the tail
        // ------------------------------------------------------------

        int rulerY = bottom + 14;
        CvInvoke.Line(canvas, new Point(left, rulerY), new Point(right, rulerY), MidGray, 1);

        for (int inches = 0; inches <= Inches(lengthMM) + 0.001; inches += 10)
        {
            int tickX = ToX(minZ + inches * MillimetersPerInch);
            CvInvoke.Line(canvas, new Point(tickX, rulerY), new Point(tickX, rulerY + 7), MidGray, 1);

            if (inches % 20 == 0)
                CenteredText(canvas, inches.ToString(), tickX, rulerY + 28, 0.6, LightGray, 1);
        }

        Text(canvas, "in", right + 2, rulerY + 28, 0.5, MidGray, 1);

        // ------------------------------------------------------------
        // Numbers and key
        // ------------------------------------------------------------

        int keyX = left - 30;
        int labelX = left + 16;
        int lineY = rulerY + 62;

        DrawDashed(canvas, new Point(keyX, lineY - 7), new Point(keyX + 36, lineY - 7), LargestColor, 3, 5, 3);
        Text(
            canvas,
            $"Largest dia {Inches(largest.MeanRadiusMM * 2.0):F2} in at {Inches(largest.ZMM - minZ):F0} in",
            labelX,
            lineY,
            0.72,
            LargestColor,
            2);

        lineY += 34;

        DrawDashed(canvas, new Point(keyX, lineY - 7), new Point(keyX + 36, lineY - 7), SmallestColor, 10, 6, 3);
        Text(
            canvas,
            $"Smallest dia {Inches(smallest.MeanRadiusMM * 2.0):F2} in at {Inches(smallest.ZMM - minZ):F0} in",
            labelX,
            lineY,
            0.72,
            SmallestColor,
            2);

        lineY += 34;

        CvInvoke.Line(canvas, new Point(keyX, lineY - 7), new Point(keyX + 36, lineY - 7), CenterlineColor, 3, LineType.AntiAlias);
        Text(canvas, "Block centerline", labelX, lineY, 0.72, CenterlineColor, 2);

        if (hasCenterline)
        {
            DrawDashed(canvas, new Point(left + 250, lineY - 7), new Point(left + 286, lineY - 7), SpindleColor, 14, 8, 2);
            Text(canvas, "Spindle axis", left + 296, lineY, 0.72, SpindleColor, 2);

            lineY += 34;

            Slice tail = slices[0];
            Slice head = slices[^1];

            Text(
                canvas,
                $"Off axis: tail X{Inches(tail.CenterXMM):+0.00;-0.00} Y{Inches(tail.CenterYMM):+0.00;-0.00}  " +
                $"head X{Inches(head.CenterXMM):+0.00;-0.00} Y{Inches(head.CenterYMM):+0.00;-0.00} in",
                keyX,
                lineY,
                0.6,
                LightGray,
                1);
        }

        return canvas;
    }

    // ------------------------------------------------------------
    // Drawing helpers
    // ------------------------------------------------------------

    private static void DrawDiameterMarker(
        Mat canvas,
        int x,
        double centerY,
        double radiusPixels,
        MCvScalar color,
        int dashPixels,
        int gapPixels)
    {
        DrawDashed(
            canvas,
            new Point(x, (int)Math.Round(centerY - radiusPixels - 10)),
            new Point(x, (int)Math.Round(centerY + radiusPixels + 10)),
            color,
            dashPixels,
            gapPixels,
            3);
    }

    private static void DrawDashed(
        Mat canvas,
        Point from,
        Point to,
        MCvScalar color,
        int dashPixels,
        int gapPixels,
        int thickness)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);

        if (length < 1.0)
            return;

        double stepX = dx / length;
        double stepY = dy / length;
        int period = Math.Max(2, dashPixels + gapPixels);

        for (double start = 0.0; start < length; start += period)
        {
            double end = Math.Min(length, start + dashPixels);

            CvInvoke.Line(
                canvas,
                new Point(from.X + (int)Math.Round(stepX * start), from.Y + (int)Math.Round(stepY * start)),
                new Point(from.X + (int)Math.Round(stepX * end), from.Y + (int)Math.Round(stepY * end)),
                color,
                thickness,
                LineType.AntiAlias);
        }
    }

    private static void Text(
        Mat canvas,
        string text,
        int x,
        int y,
        double fontScale,
        MCvScalar color,
        int thickness = 1)
    {
        CvInvoke.PutText(
            canvas,
            text,
            new Point(x, y),
            FontFace.HersheySimplex,
            fontScale,
            color,
            thickness,
            LineType.AntiAlias);
    }

    /// <summary>
    /// Draws text centred on <paramref name="centerX"/>. The width is estimated
    /// from the character count, which is close enough for these short labels.
    /// </summary>
    private static void CenteredText(
        Mat canvas,
        string text,
        int centerX,
        int y,
        double fontScale,
        MCvScalar color,
        int thickness)
    {
        int estimatedWidth = (int)Math.Round(text.Length * 19.0 * fontScale);
        Text(canvas, text, centerX - estimatedWidth / 2, y, fontScale, color, thickness);
    }

    private static double Inches(double millimeters) =>
        millimeters / MillimetersPerInch;
}
