using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using OptimizationXY.Charger.Models.Geometry;


namespace OptimizationXY.Charger.Visualization;

/// <summary>
/// Builds the tail / middle / head cross-section picture of a scanned block.
///
/// The picture has four panels: one detailed cross-section each for the TAIL,
/// MIDDLE and HEAD of the block, and one panel that overlays all three with a
/// summary. Every cross-section is drawn as seen from the tail end of the block:
/// right is toward the V's (+X), left is toward the lathe (-X), down is +Y and
/// up is -Y. The centre of each panel is the fitted centreline of the block.
///
/// This is drawing only. It reads the binned radius field that the pipeline has
/// already produced and adds no measurement work of its own.
/// </summary>
public static class RotatingRadiusSectionOverlayBuilder
{
    private const double MillimetersPerInch = 25.4;

    private static readonly MCvScalar White = new(255, 255, 255);
    private static readonly MCvScalar LightGray = new(185, 185, 185);
    private static readonly MCvScalar MidGray = new(120, 120, 120);
    private static readonly MCvScalar GridGray = new(55, 55, 55);
    private static readonly MCvScalar PanelBorder = new(90, 90, 90);
    private static readonly MCvScalar WidestColor = new(0, 215, 255);     // amber (BGR)
    private static readonly MCvScalar NarrowestColor = new(255, 200, 120); // light blue (BGR)
    private static readonly MCvScalar SpindleColor = new(0, 255, 255);    // yellow (BGR)

    private static readonly MCvScalar TailColor = new(70, 70, 255);       // red (BGR)
    private static readonly MCvScalar MiddleColor = new(70, 230, 70);     // green (BGR)
    private static readonly MCvScalar HeadColor = new(255, 150, 60);      // blue (BGR)

    /// <summary>
    /// Builds the four-panel tail / middle / head cross-section picture.
    /// </summary>
    /// <param name="binnedField">Binned radius field measured from the fitted block centreline.</param>
    /// <param name="width">Picture width in pixels.</param>
    /// <param name="height">Picture height in pixels.</param>
    /// <param name="centerline">
    /// Fitted block centreline in the machine frame. When supplied, each panel also
    /// marks where the spindle axis (machine origin) sits relative to the block centre.
    /// </param>
    public static Mat BuildTailMiddleHeadFromBinnedField(
        List<BinnedRadiusFieldPoint> binnedField,
        int width = 1000,
        int height = 1000,
        CenterlineResult? centerline = null)
    {
        width = Math.Max(400, width);
        height = Math.Max(400, height);

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

        double minZ = zBins[0];
        double maxZ = zBins[^1];

        double tailZ = zBins[(int)(zBins.Count * 0.15)];
        double midZ = zBins[(int)(zBins.Count * 0.50)];
        double headZ = zBins[(int)(zBins.Count * 0.85)];

        SectionData tail = BuildSection(binnedField, tailZ, minZ, "TAIL", "far from operator", TailColor, centerline);
        SectionData middle = BuildSection(binnedField, midZ, minZ, "MIDDLE", "centre of block", MiddleColor, centerline);
        SectionData head = BuildSection(binnedField, headZ, minZ, "HEAD", "operator side", HeadColor, centerline);

        double maxRadius = new[] { tail, middle, head }
            .Select(section => section.MaxRadiusMM + section.SpindleOffsetMM)
            .DefaultIfEmpty(1.0)
            .Max();

        if (!double.IsFinite(maxRadius) || maxRadius <= 0)
            maxRadius = 1.0;

        int panelWidth = width / 2;
        int panelHeight = height / 2;

        // One scale for every panel so the three sections can be compared by eye.
        double scale = Math.Min(panelWidth, panelHeight) * 0.30 / maxRadius;

        DrawSectionPanel(canvas, new Rectangle(0, 0, panelWidth, panelHeight), tail, scale);
        DrawSectionPanel(canvas, new Rectangle(panelWidth, 0, width - panelWidth, panelHeight), middle, scale);
        DrawSectionPanel(canvas, new Rectangle(0, panelHeight, panelWidth, height - panelHeight), head, scale);
        DrawSummaryPanel(
            canvas,
            new Rectangle(panelWidth, panelHeight, width - panelWidth, height - panelHeight),
            binnedField,
            zBins,
            minZ,
            maxZ,
            tail,
            middle,
            head,
            scale);

        return canvas;
    }

    // ------------------------------------------------------------
    // Section data
    // ------------------------------------------------------------

    private sealed class SectionData
    {
        public string Name { get; init; } = string.Empty;
        public string Note { get; init; } = string.Empty;
        public MCvScalar Color { get; init; }
        public double PositionFromTailMM { get; init; }

        // Measured outline, ordered by angle. Angle in degrees, radius in mm.
        public List<(double AngleDeg, double RadiusMM)> Outline { get; init; } = [];

        public double MeanRadiusMM { get; init; }
        public double MaxRadiusMM { get; init; }

        public bool HasDiameters { get; init; }
        public double WidestDiameterMM { get; init; }
        public double WidestAngleDeg { get; init; }
        public double NarrowestDiameterMM { get; init; }
        public double NarrowestAngleDeg { get; init; }

        // Spindle axis (machine origin) relative to the block centre, in mm.
        public bool HasSpindleAxis { get; init; }
        public double SpindleXMM { get; init; }
        public double SpindleYMM { get; init; }
        public double SpindleOffsetMM { get; init; }
    }

    private static SectionData BuildSection(
        List<BinnedRadiusFieldPoint> field,
        double targetZ,
        double minZ,
        string name,
        string note,
        MCvScalar color,
        CenterlineResult? centerline)
    {
        double nearestZ = field
            .Select(p => p.ZBinMM)
            .Distinct()
            .OrderBy(z => Math.Abs(z - targetZ))
            .First();

        List<(double AngleDeg, double RadiusMM)> outline = [.. field
            .Where(p => Math.Abs(p.ZBinMM - nearestZ) < 0.001)
            .Where(p => p.MeanRadiusMM > 0 && double.IsFinite(p.MeanRadiusMM))
            .Select(p => (AngleDeg: NormalizeAngle(p.AngleBinDeg), RadiusMM: p.MeanRadiusMM))
            .OrderBy(p => p.AngleDeg)];

        double meanRadius = outline.Count > 0 ? outline.Average(p => p.RadiusMM) : 0.0;
        double maxRadius = outline.Count > 0 ? outline.Max(p => p.RadiusMM) : 0.0;

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

        bool hasSpindle = false;
        double spindleX = 0.0;
        double spindleY = 0.0;

        if (centerline is not null && Math.Abs(centerline.DirectionZ) > 1e-9)
        {
            double along = (nearestZ - centerline.OriginZ) / centerline.DirectionZ;
            double blockCenterX = centerline.OriginX + centerline.DirectionX * along;
            double blockCenterY = centerline.OriginY + centerline.DirectionY * along;

            if (double.IsFinite(blockCenterX) && double.IsFinite(blockCenterY))
            {
                // The panel is centred on the block, so the machine origin sits at
                // minus the block centre.
                spindleX = -blockCenterX;
                spindleY = -blockCenterY;
                hasSpindle = true;
            }
        }

        return new SectionData
        {
            Name = name,
            Note = note,
            Color = color,
            PositionFromTailMM = nearestZ - minZ,
            Outline = outline,
            MeanRadiusMM = meanRadius,
            MaxRadiusMM = maxRadius,
            HasDiameters = hasDiameters,
            WidestDiameterMM = hasDiameters ? widest : 0.0,
            WidestAngleDeg = widestAngle,
            NarrowestDiameterMM = hasDiameters ? narrowest : 0.0,
            NarrowestAngleDeg = narrowestAngle,
            HasSpindleAxis = hasSpindle,
            SpindleXMM = spindleX,
            SpindleYMM = spindleY,
            SpindleOffsetMM = hasSpindle
                ? Math.Sqrt(spindleX * spindleX + spindleY * spindleY)
                : 0.0
        };
    }

    // ------------------------------------------------------------
    // Panels
    // ------------------------------------------------------------

    private static void DrawSectionPanel(
        Mat canvas,
        Rectangle panel,
        SectionData section,
        double scale)
    {
        CvInvoke.Rectangle(canvas, panel, PanelBorder, 1);

        int centerX = panel.Left + panel.Width / 2;
        int centerY = panel.Top + panel.Height / 2 - 6;

        DrawGrid(canvas, panel, centerX, centerY, scale, section.MaxRadiusMM + section.SpindleOffsetMM);

        // Title block.
        Text(canvas, section.Name, panel.Left + 12, panel.Top + 26, 0.8, section.Color, 2);
        Text(
            canvas,
            $"({section.Note})  {Inches(section.PositionFromTailMM):F1} in from tail",
            panel.Left + 12,
            panel.Top + 46,
            0.42,
            LightGray);

        if (section.Outline.Count < 6)
        {
            Text(canvas, "Not enough data for this section", panel.Left + 12, centerY, 0.5, LightGray);
            return;
        }

        DrawFill(canvas, section, centerX, centerY, scale);
        DrawOutline(canvas, section, centerX, centerY, scale, thickness: 2, drawPoints: true);

        if (section.HasDiameters)
        {
            // Largest diameter: dotted line across the block through its centre.
            DrawDiameter(
                canvas,
                centerX,
                centerY,
                scale,
                section.WidestAngleDeg,
                section.MaxRadiusMM * 1.08,
                WidestColor,
                dashPixels: 4,
                gapPixels: 5,
                thickness: 2);

            DrawDiameter(
                canvas,
                centerX,
                centerY,
                scale,
                section.NarrowestAngleDeg,
                section.MaxRadiusMM * 1.08,
                NarrowestColor,
                dashPixels: 10,
                gapPixels: 6,
                thickness: 1);
        }

        // Block centre (fitted centreline).
        CvInvoke.Circle(canvas, new Point(centerX, centerY), 4, White, -1, LineType.AntiAlias);

        // Spindle axis (machine origin).
        if (section.HasSpindleAxis)
        {
            int spindleX = centerX + (int)Math.Round(section.SpindleXMM * scale);
            int spindleY = centerY + (int)Math.Round(section.SpindleYMM * scale);

            CvInvoke.Line(canvas, new Point(spindleX - 8, spindleY), new Point(spindleX + 8, spindleY), SpindleColor, 2, LineType.AntiAlias);
            CvInvoke.Line(canvas, new Point(spindleX, spindleY - 8), new Point(spindleX, spindleY + 8), SpindleColor, 2, LineType.AntiAlias);
            CvInvoke.Circle(canvas, new Point(spindleX, spindleY), 10, SpindleColor, 1, LineType.AntiAlias);
        }

        // Numbers.
        int textX = panel.Left + 12;
        int textY = panel.Bottom - 58;

        Text(
            canvas,
            $"Average diameter: {Inches(section.MeanRadiusMM * 2.0):F2} in",
            textX,
            textY,
            0.45,
            White);

        if (section.HasDiameters)
        {
            Text(
                canvas,
                $"Largest: {Inches(section.WidestDiameterMM):F2} in (dotted)   " +
                $"Smallest: {Inches(section.NarrowestDiameterMM):F2} in (dashed)",
                textX,
                textY + 18,
                0.42,
                WidestColor);

            Text(
                canvas,
                $"Out of round: {Inches(section.WidestDiameterMM - section.NarrowestDiameterMM):F2} in",
                textX,
                textY + 36,
                0.42,
                LightGray);
        }

        if (section.HasSpindleAxis)
        {
            Text(
                canvas,
                $"Block centre from spindle axis:  X {Inches(-section.SpindleXMM):+0.00;-0.00} in   " +
                $"Y {Inches(-section.SpindleYMM):+0.00;-0.00} in",
                textX,
                textY + 52,
                0.4,
                SpindleColor);
        }
    }

    private static void DrawSummaryPanel(
        Mat canvas,
        Rectangle panel,
        List<BinnedRadiusFieldPoint> field,
        List<double> zBins,
        double minZ,
        double maxZ,
        SectionData tail,
        SectionData middle,
        SectionData head,
        double scale)
    {
        CvInvoke.Rectangle(canvas, panel, PanelBorder, 1);

        Text(canvas, "ALL THREE SECTIONS", panel.Left + 12, panel.Top + 26, 0.7, White, 2);
        Text(canvas, "View from the tail end of the block", panel.Left + 12, panel.Top + 46, 0.42, LightGray);

        // Smaller overlay of the three outlines on the right-hand side of the panel.
        double overlayScale = scale * 0.62;
        int centerX = panel.Left + (int)(panel.Width * 0.66);
        int centerY = panel.Top + (int)(panel.Height * 0.42);

        double largestRadius = new[] { tail, middle, head }.Max(section => section.MaxRadiusMM);
        DrawGrid(canvas, panel, centerX, centerY, overlayScale, largestRadius, drawAxisLabels: false, clipToRadius: true);

        foreach (SectionData section in new[] { tail, middle, head })
        {
            if (section.Outline.Count >= 6)
                DrawOutline(canvas, section, centerX, centerY, overlayScale, thickness: 2, drawPoints: false);
        }

        CvInvoke.Circle(canvas, new Point(centerX, centerY), 3, White, -1, LineType.AntiAlias);

        // Legend.
        int legendX = panel.Left + 12;
        int legendY = panel.Top + 78;

        LegendLine(canvas, legendX, legendY, TailColor, "TAIL  - far from operator");
        LegendLine(canvas, legendX, legendY + 20, MiddleColor, "MIDDLE");
        LegendLine(canvas, legendX, legendY + 40, HeadColor, "HEAD  - operator side");

        DrawDotted(canvas, new Point(legendX, legendY + 62), new Point(legendX + 26, legendY + 62), WidestColor, 4, 5, 2);
        Text(canvas, "Largest diameter", legendX + 34, legendY + 66, 0.4, WidestColor);

        DrawDotted(canvas, new Point(legendX, legendY + 82), new Point(legendX + 26, legendY + 82), NarrowestColor, 10, 6, 1);
        Text(canvas, "Smallest diameter", legendX + 34, legendY + 86, 0.4, NarrowestColor);

        CvInvoke.Circle(canvas, new Point(legendX + 13, legendY + 102), 4, White, -1, LineType.AntiAlias);
        Text(canvas, "Block centre (fitted centreline)", legendX + 34, legendY + 106, 0.4, White);

        CvInvoke.Line(canvas, new Point(legendX + 6, legendY + 122), new Point(legendX + 20, legendY + 122), SpindleColor, 2, LineType.AntiAlias);
        CvInvoke.Line(canvas, new Point(legendX + 13, legendY + 115), new Point(legendX + 13, legendY + 129), SpindleColor, 2, LineType.AntiAlias);
        Text(canvas, "Spindle axis (machine 0, 0)", legendX + 34, legendY + 126, 0.4, SpindleColor);

        // Whole-block numbers.
        var sliceDiameters = field
            .Where(p => p.MeanRadiusMM > 0 && double.IsFinite(p.MeanRadiusMM))
            .GroupBy(p => p.ZBinMM)
            .Select(group => new
            {
                Z = group.Key,
                DiameterMM = group.Average(p => p.MeanRadiusMM) * 2.0,
                Count = group.Count()
            })
            .Where(slice => slice.Count >= 12)
            .ToList();

        int textX = panel.Left + 12;
        int textY = panel.Bottom - 112;

        Text(canvas, "WHOLE BLOCK", textX, textY, 0.5, White, 2);
        Text(
            canvas,
            $"Scanned length: {Inches(maxZ - minZ):F1} in   ({zBins.Count} slices)",
            textX,
            textY + 20,
            0.42,
            LightGray);

        if (sliceDiameters.Count > 0)
        {
            var largest = sliceDiameters.OrderByDescending(slice => slice.DiameterMM).First();
            var smallest = sliceDiameters.OrderBy(slice => slice.DiameterMM).First();

            Text(
                canvas,
                $"Largest average diameter: {Inches(largest.DiameterMM):F2} in at {Inches(largest.Z - minZ):F1} in from tail",
                textX,
                textY + 38,
                0.42,
                WidestColor);

            Text(
                canvas,
                $"Smallest average diameter: {Inches(smallest.DiameterMM):F2} in at {Inches(smallest.Z - minZ):F1} in from tail",
                textX,
                textY + 56,
                0.42,
                NarrowestColor);

            Text(
                canvas,
                $"Average diameter  tail {Inches(tail.MeanRadiusMM * 2.0):F2}   " +
                $"middle {Inches(middle.MeanRadiusMM * 2.0):F2}   head {Inches(head.MeanRadiusMM * 2.0):F2} in",
                textX,
                textY + 74,
                0.42,
                LightGray);
        }

        Text(
            canvas,
            "Right = toward V's (+X)   Left = toward lathe (-X)   Down = +Y   Up = -Y",
            textX,
            textY + 96,
            0.38,
            MidGray);
    }

    // ------------------------------------------------------------
    // Drawing helpers
    // ------------------------------------------------------------

    private static Point ToScreen(
        int centerX,
        int centerY,
        double scale,
        double angleDeg,
        double radiusMM)
    {
        double theta = angleDeg * Math.PI / 180.0;

        // Machine +Y is down, which is also down on the screen.
        return new Point(
            centerX + (int)Math.Round(radiusMM * Math.Cos(theta) * scale),
            centerY + (int)Math.Round(radiusMM * Math.Sin(theta) * scale));
    }

    private static void DrawGrid(
        Mat canvas,
        Rectangle panel,
        int centerX,
        int centerY,
        double scale,
        double maxRadiusMM,
        bool drawAxisLabels = true,
        bool clipToRadius = false)
    {
        // Rings every 2 inches of radius.
        double ringStepMM = 2.0 * MillimetersPerInch;
        double outerRingMM = 0.0;

        for (double radius = ringStepMM; radius <= maxRadiusMM * 1.12; radius += ringStepMM)
        {
            CvInvoke.Circle(canvas, new Point(centerX, centerY), (int)Math.Round(radius * scale), GridGray, 1, LineType.AntiAlias);
            outerRingMM = radius;

            Text(
                canvas,
                $"{Inches(radius):F0}",
                centerX + 3,
                centerY - (int)Math.Round(radius * scale) - 2,
                0.32,
                MidGray);
        }

        // The cross-hair reaches just past the outermost ring so it never runs
        // into the title or the numbers printed in the panel.
        int reach = Math.Min(
            (int)Math.Round(Math.Max(outerRingMM, maxRadiusMM) * scale) + (clipToRadius ? 8 : 14),
            Math.Min(panel.Width, panel.Height) / 2 - 18);

        CvInvoke.Line(canvas, new Point(centerX - reach, centerY), new Point(centerX + reach, centerY), GridGray, 1);
        CvInvoke.Line(canvas, new Point(centerX, centerY - reach), new Point(centerX, centerY + reach), GridGray, 1);

        if (!drawAxisLabels)
            return;

        // Axis labels sit just outside the cross-hair.
        Text(canvas, "+X V's", centerX + reach + 4, centerY + 4, 0.36, MidGray);
        Text(canvas, "-X lathe", centerX - reach - 54, centerY + 4, 0.36, MidGray);
        Text(canvas, "-Y up", centerX + 6, centerY - reach - 4, 0.36, MidGray);
        Text(canvas, "+Y down", centerX + 6, centerY + reach + 12, 0.36, MidGray);
    }

    /// <summary>
    /// Shades the inside of the section with fine spokes from the block centre so
    /// the cross-section reads as a solid shape.
    /// </summary>
    private static void DrawFill(
        Mat canvas,
        SectionData section,
        int centerX,
        int centerY,
        double scale)
    {
        MCvScalar fill = new(
            section.Color.V0 * 0.22,
            section.Color.V1 * 0.22,
            section.Color.V2 * 0.22);

        Point center = new(centerX, centerY);

        for (double angle = 0.0; angle < 360.0; angle += 1.0)
        {
            double radius = InterpolateRadius(section.Outline, angle);
            if (!(radius > 0.0))
                continue;

            CvInvoke.Line(canvas, center, ToScreen(centerX, centerY, scale, angle, radius), fill, 3);
        }
    }

    private static void DrawOutline(
        Mat canvas,
        SectionData section,
        int centerX,
        int centerY,
        double scale,
        int thickness,
        bool drawPoints)
    {
        List<(double AngleDeg, double RadiusMM)> outline = section.Outline;

        for (int index = 0; index < outline.Count; index++)
        {
            var current = outline[index];
            var next = outline[(index + 1) % outline.Count];

            double gap = next.AngleDeg - current.AngleDeg;
            if (gap <= 0.0)
                gap += 360.0;

            Point from = ToScreen(centerX, centerY, scale, current.AngleDeg, current.RadiusMM);

            if (drawPoints)
                CvInvoke.Circle(canvas, from, 3, section.Color, -1, LineType.AntiAlias);

            // Join neighbouring measurements. A wide gap is real missing data
            // and is left open so it is visible.
            if (gap <= 30.0)
            {
                Point to = ToScreen(centerX, centerY, scale, next.AngleDeg, next.RadiusMM);
                CvInvoke.Line(canvas, from, to, section.Color, thickness, LineType.AntiAlias);
            }
        }
    }

    private static void DrawDiameter(
        Mat canvas,
        int centerX,
        int centerY,
        double scale,
        double angleDeg,
        double reachMM,
        MCvScalar color,
        int dashPixels,
        int gapPixels,
        int thickness)
    {
        Point from = ToScreen(centerX, centerY, scale, angleDeg, reachMM);
        Point to = ToScreen(centerX, centerY, scale, angleDeg + 180.0, reachMM);

        DrawDotted(canvas, from, to, color, dashPixels, gapPixels, thickness);
    }

    private static void DrawDotted(
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

    private static void LegendLine(
        Mat canvas,
        int x,
        int y,
        MCvScalar color,
        string label)
    {
        CvInvoke.Line(canvas, new Point(x, y), new Point(x + 26, y), color, 3, LineType.AntiAlias);
        Text(canvas, label, x + 34, y + 4, 0.42, color);
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
