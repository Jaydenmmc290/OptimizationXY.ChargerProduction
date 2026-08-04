using System.Drawing.Drawing2D;

namespace OptimizationXY.Charger.UI.Controls;

/// <summary>
/// Lightweight operator display of the measured block envelope. It draws
/// directly from axial radius values; no live image or saved bitmap is needed.
/// </summary>
public sealed partial class LogProfileControl : Control
{
    private IReadOnlyList<LogProfilePoint> _profilePoints =
        Array.Empty<LogProfilePoint>();

    public LogProfileControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        Dock = DockStyle.Fill;
    }

    public void SetProfile(IReadOnlyList<LogProfilePoint>? profilePoints)
    {
        _profilePoints = profilePoints ?? Array.Empty<LogProfilePoint>();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);

        RectangleF area = new(
            34F,
            22F,
            Math.Max(1F, ClientSize.Width - 68F),
            Math.Max(1F, ClientSize.Height - 48F));

        DrawGrid(e.Graphics, area);

        if (_profilePoints.Count < 2 ||
            !_profilePoints.Any(point => point.IsValid && point.RadiusInches > 0))
        {
            DrawCenteredMessage(e.Graphics, "No processed log profile available");
            return;
        }

        DrawProfile(e.Graphics, area);
    }

    private static void DrawGrid(Graphics graphics, RectangleF area)
    {
        using Pen gridPen = new(Color.Gainsboro, 1F);
        using Pen centerPen = new(Color.Gray, 1.25F)
        {
            DashStyle = DashStyle.Dash
        };

        for (int i = 1; i < 5; i++)
        {
            float x = area.Left + area.Width * i / 5F;
            graphics.DrawLine(gridPen, x, area.Top, x, area.Bottom);
        }

        graphics.DrawLine(
            centerPen,
            area.Left,
            area.Top + area.Height / 2F,
            area.Right,
            area.Top + area.Height / 2F);
    }

    private void DrawProfile(Graphics graphics, RectangleF area)
    {
        double minPosition = _profilePoints.Min(point => point.PositionInches);
        double maxPosition = _profilePoints.Max(point => point.PositionInches);
        double length = Math.Max(0.001, maxPosition - minPosition);
        double maxRadius = Math.Max(
            0.001,
            _profilePoints
                .Where(point => point.IsValid && point.RadiusInches > 0)
                .Max(point => point.RadiusInches));
        float centerY = area.Top + area.Height / 2F;

        PointF Convert(LogProfilePoint point, bool top)
        {
            float x = area.Left +
                (float)((point.PositionInches - minPosition) / length * area.Width);
            float radiusPixels =
                (float)(point.RadiusInches / maxRadius * area.Height * 0.43);
            return new PointF(x, top ? centerY - radiusPixels : centerY + radiusPixels);
        }

        using Brush woodBrush = new SolidBrush(Color.FromArgb(225, 201, 153));
        using Pen outlinePen = new(Color.FromArgb(45, 45, 45), 3F);
        using Brush gapBrush = new SolidBrush(Color.FromArgb(85, Color.Firebrick));
        using Pen gapPen = new(Color.Firebrick, 2F)
        {
            DashStyle = DashStyle.Dash
        };

        foreach (List<LogProfilePoint> segment in GetValidSegments())
        {
            if (segment.Count < 2)
                continue;

            PointF[] top = segment.Select(point => Convert(point, true)).ToArray();
            PointF[] bottom = segment
                .AsEnumerable()
                .Reverse()
                .Select(point => Convert(point, false))
                .ToArray();
            PointF[] polygon = [.. top, .. bottom];

            graphics.FillPolygon(woodBrush, polygon);
            graphics.DrawLines(outlinePen, top);
            graphics.DrawLines(outlinePen, bottom.Reverse().ToArray());
            graphics.DrawLine(outlinePen, top[0], bottom[^1]);
            graphics.DrawLine(outlinePen, top[^1], bottom[0]);
        }

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

        using Font labelFont = new("Segoe UI", 9F, FontStyle.Bold);
        using Brush labelBrush = new SolidBrush(Color.DimGray);
        graphics.DrawString("Missing scan sections are shown in red", labelFont, labelBrush, area.Left, area.Bottom + 4F);
    }

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
                segment = new List<LogProfilePoint>();
            }
        }

        if (segment.Count > 0)
            yield return segment;
    }

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
