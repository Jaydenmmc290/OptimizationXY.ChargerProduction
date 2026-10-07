using System.Drawing.Drawing2D;

namespace OptimizationXY.Charger.UI.Controls;

/// <summary>
/// Operator side view of the measured block, drawn directly from the axial
/// slices of the scan; no live image or saved bitmap is needed.
///
/// The view is from the V's side of the machine: TAIL (far from the operator)
/// on the left, HEAD (operator side) on the right, up is up. It shows:
///   - the block, with the widest and narrowest radius at every point along it,
///   - the spindle axis and the fitted centreline of the block,
///   - dotted lines at the largest and smallest diameter,
///   - a ruler in inches from the tail end, and
///   - any sections the scan missed, in red.
/// </summary>
public sealed partial class LogProfileControl : Control
{
    private static readonly Color WoodLight = Color.FromArgb(236, 219, 180);
    private static readonly Color WoodDark = Color.FromArgb(214, 184, 128);
    private static readonly Color OutlineColor = Color.FromArgb(55, 45, 30);
    private static readonly Color InnerLineColor = Color.FromArgb(150, 115, 60);
    private static readonly Color CenterlineColor = Color.FromArgb(25, 105, 210);
    private static readonly Color SpindleColor = Color.FromArgb(95, 95, 95);
    private static readonly Color LargestColor = Color.FromArgb(205, 70, 10);
    private static readonly Color SmallestColor = Color.FromArgb(20, 120, 135);

    /// <summary>
    /// The list of log profile points to display. This is a read-only list of <see cref="LogProfilePoint"/> objects.
    /// </summary>
    private IReadOnlyList<LogProfilePoint> _profilePoints =
        [];

    /// <summary>
    /// Initializes a new instance of the <see cref="LogProfileControl"/> class.
    /// </summary>
    public LogProfileControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        Dock = DockStyle.Fill;
    }

    /// <summary>
    /// Sets the log profile points to display in the control. The control will be invalidated and redrawn after setting the profile.
    /// </summary>
    /// <param name="profilePoints">The list of log profile points to display.</param>
    public void SetProfile(IReadOnlyList<LogProfilePoint>? profilePoints)
    {
        _profilePoints = profilePoints ?? [];
        Invalidate();
    }

    /// <summary>
    /// Draws the log profile view. If there are no valid profile points, it displays a message
    /// indicating that no processed log profile is available.
    /// </summary>
    /// <param name="e">A <see cref="PaintEventArgs"/> that contains the event data.</param>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(BackColor);

        // Top band holds the TAIL / MIDDLE / HEAD labels, bottom band the ruler.
        RectangleF area = new(
            40F,
            40F,
            Math.Max(1F, ClientSize.Width - 80F),
            Math.Max(1F, ClientSize.Height - 78F));

        if (_profilePoints.Count < 2 ||
            !_profilePoints.Any(point => point.IsValid && point.RadiusInches > 0))
        {
            DrawCenteredMessage(graphics, "No processed log profile available");
            return;
        }

        DrawProfile(graphics, area);
    }

    /// <summary>
    /// Draws the complete side view inside the given area.
    /// </summary>
    private void DrawProfile(Graphics graphics, RectangleF area)
    {
        List<LogProfilePoint> valid = [.. _profilePoints
            .Where(point => point.IsValid && point.RadiusInches > 0)];

        double minPosition = _profilePoints.Min(point => point.PositionInches);
        double maxPosition = _profilePoints.Max(point => point.PositionInches);
        double length = Math.Max(0.001, maxPosition - minPosition);

        // Vertical scale: leave room for the widest radius plus any offset of
        // the block from the spindle axis.
        double maxExtent = Math.Max(
            0.001,
            valid.Max(point => OuterRadius(point) + Math.Abs(CenterOffset(point))));

        float pixelsPerInch = (float)(area.Height * 0.46 / maxExtent);
        float axisY = area.Top + area.Height / 2F;

        float ToX(double positionInches) =>
            area.Left + (float)((positionInches - minPosition) / length * area.Width);

        // Machine +Y is down, which is also down on the screen.
        float CenterY(LogProfilePoint point) =>
            axisY + (float)(CenterOffset(point) * pixelsPerInch);

        DrawSectionBands(graphics, area, valid, minPosition, length);
        DrawRuler(graphics, area, minPosition, maxPosition, ToX);

        // ------------------------------------------------------------
        // Block body: light band = widest radius, dark band = narrowest.
        // ------------------------------------------------------------
        using Brush lightBrush = new SolidBrush(WoodLight);
        using Brush darkBrush = new SolidBrush(WoodDark);
        using Pen outlinePen = new(OutlineColor, 2.2F) { LineJoin = LineJoin.Round };
        using Pen innerPen = new(InnerLineColor, 1F);

        foreach (List<LogProfilePoint> segment in GetValidSegments())
        {
            if (segment.Count < 2)
                continue;

            PointF[] outerTop = [.. segment.Select(point =>
                new PointF(ToX(point.PositionInches), CenterY(point) - (float)(OuterRadius(point) * pixelsPerInch)))];
            PointF[] outerBottom = [.. segment.Select(point =>
                new PointF(ToX(point.PositionInches), CenterY(point) + (float)(OuterRadius(point) * pixelsPerInch)))];
            PointF[] innerTop = [.. segment.Select(point =>
                new PointF(ToX(point.PositionInches), CenterY(point) - (float)(InnerRadius(point) * pixelsPerInch)))];
            PointF[] innerBottom = [.. segment.Select(point =>
                new PointF(ToX(point.PositionInches), CenterY(point) + (float)(InnerRadius(point) * pixelsPerInch)))];

            graphics.FillPolygon(lightBrush, ClosedShape(outerTop, outerBottom));
            graphics.FillPolygon(darkBrush, ClosedShape(innerTop, innerBottom));

            graphics.DrawLines(innerPen, innerTop);
            graphics.DrawLines(innerPen, innerBottom);

            graphics.DrawLines(outlinePen, outerTop);
            graphics.DrawLines(outlinePen, outerBottom);
            graphics.DrawLine(outlinePen, outerTop[0], outerBottom[0]);
            graphics.DrawLine(outlinePen, outerTop[^1], outerBottom[^1]);
        }

        DrawMissingSections(graphics, area, minPosition, length);

        // ------------------------------------------------------------
        // Spindle axis (machine centre line).
        // ------------------------------------------------------------
        using (Pen spindlePen = new(SpindleColor, 1.4F) { DashStyle = DashStyle.DashDot })
        {
            graphics.DrawLine(spindlePen, area.Left - 26F, axisY, area.Right + 26F, axisY);
        }

        // ------------------------------------------------------------
        // Fitted centreline of the block.
        // ------------------------------------------------------------
        bool hasCenterline = valid.Any(point => point.HasCenter);

        if (hasCenterline && valid.Count >= 2)
        {
            using Pen centerlinePen = new(CenterlineColor, 2F);

            PointF[] centerline = [.. valid.Select(point =>
                new PointF(ToX(point.PositionInches), CenterY(point)))];

            graphics.DrawLines(centerlinePen, centerline);
        }

        // ------------------------------------------------------------
        // Largest and smallest diameter (dotted lines).
        // ------------------------------------------------------------
        LogProfilePoint largest = valid.OrderByDescending(point => point.RadiusInches).First();
        LogProfilePoint smallest = valid.OrderBy(point => point.RadiusInches).First();

        using Font noteFont = new("Segoe UI", 8.5F, FontStyle.Bold);

        DrawDiameterMarker(
            graphics,
            area,
            noteFont,
            ToX(largest.PositionInches),
            CenterY(largest),
            (float)(OuterRadius(largest) * pixelsPerInch),
            LargestColor,
            $"Largest dia {largest.RadiusInches * 2.0:F2} in at {largest.PositionInches - minPosition:F0} in",
            labelAbove: true);

        if (Math.Abs(largest.PositionInches - smallest.PositionInches) > 0.01)
        {
            DrawDiameterMarker(
                graphics,
                area,
                noteFont,
                ToX(smallest.PositionInches),
                CenterY(smallest),
                (float)(OuterRadius(smallest) * pixelsPerInch),
                SmallestColor,
                $"Smallest dia {smallest.RadiusInches * 2.0:F2} in at {smallest.PositionInches - minPosition:F0} in",
                labelAbove: false);
        }

        DrawLegend(graphics, area, hasCenterline);
    }

    /// <summary>
    /// Shades and labels the TAIL, MIDDLE and HEAD thirds of the block, with the
    /// average diameter of each third.
    /// </summary>
    private static void DrawSectionBands(
        Graphics graphics,
        RectangleF area,
        List<LogProfilePoint> valid,
        double minPosition,
        double length)
    {
        string[] names = ["TAIL", "MIDDLE", "HEAD"];
        string[] notes = ["far from operator", "centre of block", "operator side"];

        using Brush bandBrush = new SolidBrush(Color.FromArgb(247, 247, 250));
        using Pen dividerPen = new(Color.FromArgb(200, 200, 205), 1F) { DashStyle = DashStyle.Dash };
        using Font nameFont = new("Segoe UI", 10.5F, FontStyle.Bold);
        using Font noteFont = new("Segoe UI", 8F, FontStyle.Regular);
        using Brush nameBrush = new SolidBrush(Color.FromArgb(40, 40, 40));
        using Brush noteBrush = new SolidBrush(Color.DimGray);
        using StringFormat centered = new() { Alignment = StringAlignment.Center };

        float third = area.Width / 3F;

        for (int index = 0; index < 3; index++)
        {
            float left = area.Left + third * index;

            if (index == 1)
                graphics.FillRectangle(bandBrush, left, area.Top, third, area.Height);

            if (index > 0)
                graphics.DrawLine(dividerPen, left, area.Top - 34F, left, area.Bottom);

            double from = minPosition + length * index / 3.0;
            double to = minPosition + length * (index + 1) / 3.0;

            List<LogProfilePoint> inThird = [.. valid
                .Where(point => point.PositionInches >= from && point.PositionInches <= to)];

            string diameter = inThird.Count > 0
                ? $"avg dia {inThird.Average(point => point.RadiusInches) * 2.0:F2} in"
                : "no data";

            RectangleF nameBox = new(left, area.Top - 38F, third, 18F);
            RectangleF noteBox = new(left, area.Top - 20F, third, 16F);

            graphics.DrawString(names[index], nameFont, nameBrush, nameBox, centered);
            graphics.DrawString($"{notes[index]}  |  {diameter}", noteFont, noteBrush, noteBox, centered);
        }
    }

    /// <summary>
    /// Draws a ruler along the bottom, in inches from the tail end.
    /// </summary>
    private static void DrawRuler(
        Graphics graphics,
        RectangleF area,
        double minPosition,
        double maxPosition,
        Func<double, float> toX)
    {
        using Pen rulerPen = new(Color.FromArgb(120, 120, 120), 1F);
        using Pen gridPen = new(Color.FromArgb(232, 232, 232), 1F);
        using Font tickFont = new("Segoe UI", 7.5F, FontStyle.Regular);
        using Brush tickBrush = new SolidBrush(Color.DimGray);
        using StringFormat centered = new() { Alignment = StringAlignment.Center };

        float rulerY = area.Bottom + 4F;
        graphics.DrawLine(rulerPen, area.Left, rulerY, area.Right, rulerY);

        double length = maxPosition - minPosition;
        double step = length > 120.0 ? 20.0 : 10.0;

        for (double inches = 0.0; inches <= length + 0.001; inches += step)
        {
            float x = toX(minPosition + inches);

            graphics.DrawLine(gridPen, x, area.Top, x, area.Bottom);
            graphics.DrawLine(rulerPen, x, rulerY, x, rulerY + 5F);
            graphics.DrawString(
                $"{inches:F0}",
                tickFont,
                tickBrush,
                new RectangleF(x - 20F, rulerY + 5F, 40F, 14F),
                centered);
        }

        graphics.DrawString(
            "inches from tail",
            tickFont,
            tickBrush,
            area.Left - 36F,
            rulerY + 18F);
    }

    /// <summary>
    /// Highlights slices the scan could not measure.
    /// </summary>
    private void DrawMissingSections(
        Graphics graphics,
        RectangleF area,
        double minPosition,
        double length)
    {
        using Brush gapBrush = new SolidBrush(Color.FromArgb(85, Color.Firebrick));
        using Pen gapPen = new(Color.Firebrick, 2F)
        {
            DashStyle = DashStyle.Dash
        };

        for (int index = 0; index < _profilePoints.Count; index++)
        {
            if (_profilePoints[index].IsValid)
                continue;

            double previousPosition = index > 0
                ? _profilePoints[index - 1].PositionInches
                : _profilePoints[index].PositionInches;
            double nextPosition = index + 1 < _profilePoints.Count
                ? _profilePoints[index + 1].PositionInches
                : _profilePoints[index].PositionInches;
            float left = area.Left +
                (float)(((previousPosition + _profilePoints[index].PositionInches) / 2.0 - minPosition) / length * area.Width);
            float right = area.Left +
                (float)(((nextPosition + _profilePoints[index].PositionInches) / 2.0 - minPosition) / length * area.Width);
            RectangleF gap = new(
                Math.Min(left, right),
                area.Top,
                Math.Max(3F, Math.Abs(right - left)),
                area.Height);
            graphics.FillRectangle(gapBrush, gap);
            graphics.DrawRectangle(gapPen, gap.X, gap.Y, gap.Width, gap.Height);
        }
    }

    /// <summary>
    /// Draws a dotted line across the block at one position, with a label.
    /// </summary>
    private static void DrawDiameterMarker(
        Graphics graphics,
        RectangleF area,
        Font font,
        float x,
        float centerY,
        float radiusPixels,
        Color color,
        string label,
        bool labelAbove)
    {
        using Pen dottedPen = new(color, 2.2F) { DashStyle = DashStyle.Dot };
        using Brush textBrush = new SolidBrush(color);
        using Brush backBrush = new SolidBrush(Color.FromArgb(225, Color.White));

        float top = Math.Max(area.Top, centerY - radiusPixels - 5F);
        float bottom = Math.Min(area.Bottom, centerY + radiusPixels + 5F);

        graphics.DrawLine(dottedPen, x, top, x, bottom);

        SizeF size = graphics.MeasureString(label, font);

        // Keep the label inside the drawing area, on whichever side has room.
        float labelX = x + 6F;
        if (labelX + size.Width > area.Right)
            labelX = x - 6F - size.Width;

        labelX = Math.Max(area.Left, labelX);

        float labelY = labelAbove
            ? top + 4F
            : bottom - size.Height - 4F;

        graphics.FillRectangle(backBrush, labelX - 2F, labelY - 1F, size.Width + 4F, size.Height + 2F);
        graphics.DrawString(label, font, textBrush, labelX, labelY);
    }

    /// <summary>
    /// Draws the key to the lines and colours along the bottom edge.
    /// </summary>
    private static void DrawLegend(
        Graphics graphics,
        RectangleF area,
        bool hasCenterline)
    {
        using Font font = new("Segoe UI", 7.5F, FontStyle.Regular);
        using Brush textBrush = new SolidBrush(Color.FromArgb(70, 70, 70));

        float y = area.Bottom + 22F;
        float x = area.Left + 70F;

        void Item(Action<float, float> drawSwatch, string text)
        {
            drawSwatch(x, y + 7F);
            x += 30F;
            graphics.DrawString(text, font, textBrush, x, y);
            x += graphics.MeasureString(text, font).Width + 14F;
        }

        Item(
            (swatchX, swatchY) =>
            {
                using Pen pen = new(SpindleColor, 1.4F) { DashStyle = DashStyle.DashDot };
                graphics.DrawLine(pen, swatchX, swatchY, swatchX + 26F, swatchY);
            },
            "Spindle axis");

        if (hasCenterline)
        {
            Item(
                (swatchX, swatchY) =>
                {
                    using Pen pen = new(CenterlineColor, 2F);
                    graphics.DrawLine(pen, swatchX, swatchY, swatchX + 26F, swatchY);
                },
                "Block centerline");
        }

        Item(
            (swatchX, swatchY) =>
            {
                using Brush light = new SolidBrush(WoodLight);
                using Brush dark = new SolidBrush(WoodDark);
                using Pen pen = new(OutlineColor, 1F);
                graphics.FillRectangle(light, swatchX, swatchY - 5F, 26F, 10F);
                graphics.FillRectangle(dark, swatchX, swatchY - 2.5F, 26F, 5F);
                graphics.DrawRectangle(pen, swatchX, swatchY - 5F, 26F, 10F);
            },
            "Light = widest, dark = narrowest");

        Item(
            (swatchX, swatchY) =>
            {
                using Brush gap = new SolidBrush(Color.FromArgb(85, Color.Firebrick));
                using Pen pen = new(Color.Firebrick, 1F);
                graphics.FillRectangle(gap, swatchX, swatchY - 5F, 26F, 10F);
                graphics.DrawRectangle(pen, swatchX, swatchY - 5F, 26F, 10F);
            },
            "Missing scan");
    }

    /// <summary>
    /// Joins a top edge and a bottom edge (both left to right) into one closed outline.
    /// </summary>
    private static PointF[] ClosedShape(PointF[] top, PointF[] bottom)
    {
        PointF[] shape = new PointF[top.Length + bottom.Length];

        Array.Copy(top, shape, top.Length);

        for (int index = 0; index < bottom.Length; index++)
            shape[top.Length + index] = bottom[bottom.Length - 1 - index];

        return shape;
    }

    /// <summary>Widest radius at a slice, falling back to the typical radius.</summary>
    private static double OuterRadius(LogProfilePoint point) =>
        point.MaxRadiusInches > 0 && double.IsFinite(point.MaxRadiusInches)
            ? Math.Max(point.MaxRadiusInches, point.RadiusInches)
            : point.RadiusInches;

    /// <summary>Narrowest radius at a slice, falling back to the typical radius.</summary>
    private static double InnerRadius(LogProfilePoint point) =>
        point.MinRadiusInches > 0 && double.IsFinite(point.MinRadiusInches)
            ? Math.Min(point.MinRadiusInches, point.RadiusInches)
            : point.RadiusInches;

    /// <summary>Vertical offset of the block centreline from the spindle axis.</summary>
    private static double CenterOffset(LogProfilePoint point) =>
        point.HasCenter && double.IsFinite(point.CenterYInches)
            ? point.CenterYInches
            : 0.0;

    /// <summary>
    /// Gets the valid segments of log profile points. A valid segment is defined as a contiguous sequence of points where each point is valid and
    /// has a positive radius. The method yields each valid segment as a list of <see cref="LogProfilePoint"/> objects.
    /// </summary>
    /// <returns>An enumerable of lists of <see cref="LogProfilePoint"/> representing the valid segments.</returns>
    private IEnumerable<List<LogProfilePoint>> GetValidSegments()
    {
        var segment = new List<LogProfilePoint>();

        foreach (LogProfilePoint point in _profilePoints)
        {
            if (point.IsValid && point.RadiusInches > 0)
            {
                segment.Add(point);
                continue;
            }

            if (segment.Count > 0)
            {
                yield return segment;
                segment = [];
            }
        }

        if (segment.Count > 0)
            yield return segment;
    }


    /// <summary>
    /// Draws a centered message on the specified graphics surface. The message is drawn using a
    /// specified font and color, and it is centered both horizontally and vertically within the control's client area.
    /// </summary>
    /// <param name="graphics">The graphics surface on which to draw the message.</param>
    /// <param name="message">The message to be drawn.</param>
    private void DrawCenteredMessage(Graphics graphics, string message)
    {
        using Font font = new("Segoe UI", 15F, FontStyle.Regular);
        using Brush brush = new SolidBrush(Color.DimGray);
        SizeF size = graphics.MeasureString(message, font);
        graphics.DrawString(
            message,
            font,
            brush,
            (ClientSize.Width - size.Width) / 2F,
            (ClientSize.Height - size.Height) / 2F);
    }
}
