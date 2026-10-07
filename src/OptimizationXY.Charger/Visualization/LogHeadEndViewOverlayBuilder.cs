using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using OptimizationXY.Charger.Models.Geometry;


namespace OptimizationXY.Charger.Visualization;

/// <summary>
/// Builds the single picture of a scanned block that is shown in the log overlay
/// box on the main form: the block seen end-on, looking down its length from the
/// HEAD end (the operator side).
///
/// Because the viewer stands at the head end, the V's (+X) are on the LEFT of the
/// picture and the lathe (-X) is on the RIGHT. Down is +Y and up is -Y, the same
/// as the machine. The centre of the picture is the spindle axis, so each section
/// is drawn where it actually sits on the machine.
///
/// The head section is nearest the viewer and is drawn filled. The middle and
/// tail sections are drawn as outlines behind it.
///
/// The text is deliberately large because the picture is shrunk to fit a small
/// box. This is drawing only: it reads the binned radius field the pipeline has
/// already produced and adds no measurement work of its own.
/// </summary>
public static class LogHeadEndViewOverlayBuilder
{
    private const double MillimetersPerInch = 25.4;

    private static readonly MCvScalar White = new(255, 255, 255);
    private static readonly MCvScalar LightGray = new(200, 200, 200);
    private static readonly MCvScalar MidGray = new(130, 130, 130);
    private static readonly MCvScalar GridGray = new(60, 60, 60);
    private static readonly MCvScalar WoodColor = new(95, 150, 190);
    private static readonly MCvScalar LargestColor = new(0, 215, 255);      // amber (BGR)
    private static readonly MCvScalar SmallestColor = new(255, 200, 120);   // light blue (BGR)
    private static readonly MCvScalar SpindleColor = new(0, 255, 255);      // yellow (BGR)

    private static readonly MCvScalar TailColor = new(70, 70, 255);         // red (BGR)
    private static readonly MCvScalar MiddleColor = new(70, 230, 70);       // green (BGR)
    private static readonly MCvScalar HeadColor = new(255, 150, 60);        // blue (BGR)

    private sealed class Section
    {
        public string Name { get; init; } = string.Empty;
        public MCvScalar Color { get; init; }

        // Measured outline, ordered by angle. Angle in degrees, radius in mm,
        // both measured about the block centre at this section.
        public List<(double AngleDeg, double RadiusMM)> Outline { get; init; } = [];

        // Block centre relative to the spindle axis at this section, in mm.
        public double CenterXMM { get; init; }
        public double CenterYMM { get; init; }

        public double MeanRadiusMM { get; init; }
        public double MaxRadiusMM { get; init; }

        public bool HasDiameters { get; init; }
        public double WidestDiameterMM { get; init; }
        public double WidestAngleDeg { get; init; }
        public double NarrowestDiameterMM { get; init; }
        public double NarrowestAngleDeg { get; init; }
    }

    /// <summary>
    /// Builds the head-end view picture.
    /// </summary>
    /// <param name="binnedField">Binned radius field measured from the fitted block centreline.</param>
    /// <param name="centerline">
    /// Fitted block centreline in the machine frame. When supplied each section is
    /// drawn where it sits relative to the spindle axis.
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

        List<double> zBins = [.. binnedField
            .Select(p => p.ZBinMM)
            .Distinct()
            .OrderBy(z => z)];

        if (zBins.Count < 3)
        {
            Text(canvas, "Not enough scan data", 40, height / 2, 0.9, LightGray, 2);
            return canvas;
        }

        // Same three positions as the saved four-panel cross-section picture.
        Section tail = BuildSection(binnedField, zBins[(int)(zBins.Count * 0.15)], "TAIL", TailColor, centerline);
        Section middle = BuildSection(binnedField, zBins[(int)(zBins.Count * 0.50)], "MIDDLE", MiddleColor, centerline);
        Section head = BuildSection(binnedField, zBins[(int)(zBins.Count * 0.85)], "HEAD", HeadColor, centerline);

        Section[] sections = [tail, middle, head];
        List<Section> drawable = [.. sections.Where(section => section.Outline.Count >= 6)];

        if (drawable.Count == 0)
        {
            Text(canvas, "Not enough scan data", 40, height / 2, 0.9, LightGray, 2);
            return canvas;
        }

        int centerX = width / 2;
        int centerY = 268;

        double extentMM = drawable.Max(section =>
            section.MaxRadiusMM +
            Math.Sqrt(section.CenterXMM * section.CenterXMM + section.CenterYMM * section.CenterYMM));

        if (!double.IsFinite(extentMM) || extentMM <= 0)
            extentMM = 1.0;

        double scale = 178.0 / extentMM;

        // Looking from the head end: +X (V's) is on the left, +Y is down.
        Point ToScreen(Section section, double angleDeg, double radiusMM)
        {
            double theta = angleDeg * Math.PI / 180.0;

            return new Point(
                centerX - (int)Math.Round((section.CenterXMM + radiusMM * Math.Cos(theta)) * scale),
                centerY + (int)Math.Round((section.CenterYMM + radiusMM * Math.Sin(theta)) * scale));
        }

        CenteredText(canvas, "VIEW FROM HEAD END", centerX, 34, 0.9, White, 2);
        CenteredText(canvas, "looking down the log from the operator side", centerX, 58, 0.5, LightGray, 1);

        // ------------------------------------------------------------
        // Head section, filled: it is the end nearest the viewer
        // ------------------------------------------------------------

        if (head.Outline.Count >= 6)
        {
            Point headCenter = ToScreen(head, 0.0, 0.0);

            for (double angle = 0.0; angle < 360.0; angle += 1.0)
            {
                double radius = InterpolateRadius(head.Outline, angle);
                if (!(radius > 0.0))
                    continue;

                CvInvoke.Line(canvas, headCenter, ToScreen(head, angle, radius), WoodColor, 3);
            }
        }

        // ------------------------------------------------------------
        // Grid about the spindle axis: a ring every 2 inches of radius
        // ------------------------------------------------------------

        double ringStepMM = 2.0 * MillimetersPerInch;
        int ringNumber = 1;

        for (double radius = ringStepMM; radius <= extentMM * 1.02; radius += ringStepMM)
        {
            int ringPixels = (int)Math.Round(radius * scale);
            CvInvoke.Circle(canvas, new Point(centerX, centerY), ringPixels, GridGray, 1, LineType.AntiAlias);

            if (ringNumber % 2 == 0)
                Text(canvas, $"{Inches(radius):F0}", centerX + 4, centerY - ringPixels - 3, 0.42, MidGray, 1);

            ringNumber++;
        }

        int reach = (int)Math.Round(extentMM * scale) + 10;

        CvInvoke.Line(canvas, new Point(centerX - reach, centerY), new Point(centerX + reach, centerY), GridGray, 1);
        CvInvoke.Line(canvas, new Point(centerX, centerY - reach), new Point(centerX, centerY + reach), GridGray, 1);

        Text(canvas, "V's", 8, centerY - 10, 0.6, LightGray, 1);
        Text(canvas, "+X", 8, centerY + 16, 0.6, LightGray, 1);
        Text(canvas, "lathe", width - 64, centerY - 10, 0.6, LightGray, 1);
        Text(canvas, "-X", width - 40, centerY + 16, 0.6, LightGray, 1);
        Text(canvas, "up -Y", centerX + 44, centerY - reach + 2, 0.5, MidGray, 1);
        Text(canvas, "down +Y", centerX + 44, centerY + reach + 2, 0.5, MidGray, 1);

        // ------------------------------------------------------------
        // Outlines: tail and middle behind, head on top
        // ------------------------------------------------------------

        foreach (Section section in drawable)
        {
            int thickness = ReferenceEquals(section, head) ? 3 : 2;
            List<(double AngleDeg, double RadiusMM)> outline = section.Outline;

            for (int index = 0; index < outline.Count; index++)
            {
                var current = outline[index];
                var next = outline[(index + 1) % outline.Count];

                double gap = next.AngleDeg - current.AngleDeg;
                if (gap <= 0.0)
                    gap += 360.0;

                // A wide gap is real missing data and is left open so it shows.
                if (gap > 30.0)
                    continue;

                CvInvoke.Line(
                    canvas,
                    ToScreen(section, current.AngleDeg, current.RadiusMM),
                    ToScreen(section, next.AngleDeg, next.RadiusMM),
                    section.Color,
                    thickness,
                    LineType.AntiAlias);
            }
        }

        // ------------------------------------------------------------
        // Largest and smallest diameter found in the three sections
        // ------------------------------------------------------------

        List<Section> withDiameters = [.. drawable.Where(section => section.HasDiameters)];
        Section? largest = withDiameters.OrderByDescending(section => section.WidestDiameterMM).FirstOrDefault();
        Section? smallest = withDiameters.OrderBy(section => section.NarrowestDiameterMM).FirstOrDefault();

        if (largest is not null)
        {
            DrawDashed(
                canvas,
                ToScreen(largest, largest.WidestAngleDeg, largest.MaxRadiusMM * 1.07),
                ToScreen(largest, largest.WidestAngleDeg + 180.0, largest.MaxRadiusMM * 1.07),
                LargestColor,
                3,
                5,
                3);
        }

        if (smallest is not null)
        {
            DrawDashed(
                canvas,
                ToScreen(smallest, smallest.NarrowestAngleDeg, smallest.MaxRadiusMM * 1.07),
                ToScreen(smallest, smallest.NarrowestAngleDeg + 180.0, smallest.MaxRadiusMM * 1.07),
                SmallestColor,
                10,
                6,
                3);
        }

        // Block centre at each section (the fitted centreline seen end-on).
        foreach (Section section in drawable)
        {
            Point blockCenter = ToScreen(section, 0.0, 0.0);
            CvInvoke.Circle(canvas, blockCenter, 5, section.Color, -1, LineType.AntiAlias);
            CvInvoke.Circle(canvas, blockCenter, 5, White, 1, LineType.AntiAlias);
        }

        // Spindle axis (machine 0, 0).
        bool hasCenterline = centerline is not null && Math.Abs(centerline.DirectionZ) > 1e-9;

        if (hasCenterline)
            DrawSpindleMark(canvas, centerX, centerY, 12);

        // ------------------------------------------------------------
        // Numbers
        // ------------------------------------------------------------

        int lineY = 494;
        double third = width / 3.0;

        for (int index = 0; index < sections.Length; index++)
        {
            Section section = sections[index];
            int middleX = (int)Math.Round(third * (index + 0.5));

            CenteredText(canvas, section.Name, middleX, lineY, 0.8, section.Color, 2);

            if (section.Outline.Count >= 6)
                CenteredText(canvas, $"{Inches(section.MeanRadiusMM * 2.0):F1} in", middleX, lineY + 28, 0.75, White, 2);
        }

        lineY += 62;

        if (largest is not null)
        {
            DrawDashed(canvas, new Point(14, lineY - 7), new Point(50, lineY - 7), LargestColor, 3, 5, 3);
            Text(
                canvas,
                $"Largest {Inches(largest.WidestDiameterMM):F2} in ({largest.Name.ToLowerInvariant()})",
                58,
                lineY,
                0.6,
                LargestColor,
                2);
        }

        if (smallest is not null)
        {
            DrawDashed(canvas, new Point(332, lineY - 7), new Point(368, lineY - 7), SmallestColor, 10, 6, 3);
            Text(
                canvas,
                $"Smallest {Inches(smallest.NarrowestDiameterMM):F2} in ({smallest.Name.ToLowerInvariant()})",
                376,
                lineY,
                0.6,
                SmallestColor,
                2);
        }

        if (hasCenterline)
        {
            lineY += 32;

            DrawSpindleMark(canvas, 32, lineY - 7, 10);
            Text(
                canvas,
                $"Spindle axis.  Off axis: tail X{Inches(tail.CenterXMM):+0.00;-0.00} Y{Inches(tail.CenterYMM):+0.00;-0.00}  " +
                $"head X{Inches(head.CenterXMM):+0.00;-0.00} Y{Inches(head.CenterYMM):+0.00;-0.00}",
                58,
                lineY,
                0.5,
                LightGray,
                1);
        }

        return canvas;
    }

    // ------------------------------------------------------------
    // Section data
    // ------------------------------------------------------------

    private static Section BuildSection(
        List<BinnedRadiusFieldPoint> field,
        double sectionZ,
        string name,
        MCvScalar color,
        CenterlineResult? centerline)
    {
        List<(double AngleDeg, double RadiusMM)> outline = [.. field
            .Where(p => Math.Abs(p.ZBinMM - sectionZ) < 0.001)
            .Where(p => p.MeanRadiusMM > 0 && double.IsFinite(p.MeanRadiusMM))
            .Select(p => (AngleDeg: NormalizeAngle(p.AngleBinDeg), RadiusMM: p.MeanRadiusMM))
            .OrderBy(p => p.AngleDeg)];

        // Diameters: a radius paired with the radius measured 180 degrees opposite.
        bool hasDiameters = false;
        double widest = 0.0;
        double widestAngle = 0.0;
        double narrowest = double.MaxValue;
        double narrowestAngle = 0.0;

        foreach (var point in outline)
        {
            if (point.AngleDeg >= 180.0)
                continue;

            double oppositeAngle = point.AngleDeg + 180.0;
            double oppositeRadius = double.NaN;
            double bestDifference = 6.0; // degrees

            foreach (var candidate in outline)
            {
                double difference = Math.Abs(candidate.AngleDeg - oppositeAngle);
                if (difference < bestDifference)
                {
                    bestDifference = difference;
                    oppositeRadius = candidate.RadiusMM;
                }
            }

            if (double.IsNaN(oppositeRadius))
                continue;

            double diameter = point.RadiusMM + oppositeRadius;
            hasDiameters = true;

            if (diameter > widest)
            {
                widest = diameter;
                widestAngle = point.AngleDeg;
            }

            if (diameter < narrowest)
            {
                narrowest = diameter;
                narrowestAngle = point.AngleDeg;
            }
        }

        double centerX = 0.0;
        double centerY = 0.0;

        if (centerline is not null && Math.Abs(centerline.DirectionZ) > 1e-9)
        {
            double along = (sectionZ - centerline.OriginZ) / centerline.DirectionZ;
            double x = centerline.OriginX + centerline.DirectionX * along;
            double y = centerline.OriginY + centerline.DirectionY * along;

            if (double.IsFinite(x) && double.IsFinite(y))
            {
                centerX = x;
                centerY = y;
            }
        }

        return new Section
        {
            Name = name,
            Color = color,
            Outline = outline,
            CenterXMM = centerX,
            CenterYMM = centerY,
            MeanRadiusMM = outline.Count > 0 ? outline.Average(p => p.RadiusMM) : 0.0,
            MaxRadiusMM = outline.Count > 0 ? outline.Max(p => p.RadiusMM) : 0.0,
            HasDiameters = hasDiameters,
            WidestDiameterMM = hasDiameters ? widest : 0.0,
            WidestAngleDeg = widestAngle,
            NarrowestDiameterMM = hasDiameters ? narrowest : 0.0,
            NarrowestAngleDeg = narrowestAngle
        };
    }

    /// <summary>
    /// Radius of the outline at any angle, interpolated between the two nearest
    /// measurements. Returns 0 inside a gap wider than 30 degrees.
    /// </summary>
    private static double InterpolateRadius(
        List<(double AngleDeg, double RadiusMM)> outline,
        double angleDeg)
    {
        if (outline.Count == 0)
            return 0.0;

        if (outline.Count == 1)
            return outline[0].RadiusMM;

        for (int index = 0; index < outline.Count; index++)
        {
            var current = outline[index];
            var next = outline[(index + 1) % outline.Count];

            double start = current.AngleDeg;
            double end = next.AngleDeg;

            if (end <= start)
                end += 360.0;

            double angle = angleDeg;
            if (angle < start)
                angle += 360.0;

            if (angle < start || angle > end)
                continue;

            double span = end - start;
            if (span > 30.0)
                return 0.0;

            double t = span > 0.0 ? (angle - start) / span : 0.0;
            return current.RadiusMM + (next.RadiusMM - current.RadiusMM) * t;
        }

        return 0.0;
    }

    // ------------------------------------------------------------
    // Drawing helpers
    // ------------------------------------------------------------

    private static void DrawSpindleMark(Mat canvas, int x, int y, int size)
    {
        CvInvoke.Line(canvas, new Point(x - size, y), new Point(x + size, y), SpindleColor, 2, LineType.AntiAlias);
        CvInvoke.Line(canvas, new Point(x, y - size), new Point(x, y + size), SpindleColor, 2, LineType.AntiAlias);
        CvInvoke.Circle(canvas, new Point(x, y), size + 2, SpindleColor, 1, LineType.AntiAlias);
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

    private static double NormalizeAngle(double angle)
    {
        angle %= 360.0;

        if (angle < 0)
            angle += 360.0;

        return angle;
    }
}
