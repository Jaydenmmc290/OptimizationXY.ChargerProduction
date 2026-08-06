using System.Drawing.Drawing2D;

namespace OptimizationXY.Charger.UI.Controls;

/// <summary>
/// Lightweight operator display of the measured block envelope. It draws
/// directly from axial radius values; no live image or saved bitmap is needed.
/// </summary>
public sealed partial class LogProfileControl : Control
{

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
    /// Overrides the OnPaint method to draw the log profile control. It draws a grid, the log profile, and any missing scan sections.
    /// If there are no valid profile points, it displays a message indicating that no processed log profile is available.
    /// </summary>
    /// <param name="e">A <see cref="PaintEventArgs"/> that contains the event data.</param>
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

    /// <summary>
    /// Draws a grid on the specified graphics surface within the given area. The grid consists of vertical lines dividing the area into five equal sections and a horizontal dashed line at the center.
    /// </summary>
    /// <param name="graphics">The graphics surface on which to draw the grid.</param>
    /// <param name="area">The area within which to draw the grid.</param>
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

    /// <summary>
    /// Draws the log profile on the specified graphics surface within the given area. It calculates the minimum and maximum positions and radii from the profile points, converts them to pixel coordinates,
    /// and draws filled polygons representing valid segments of the log profile. Missing scan sections are highlighted in red.
    /// </summary>
    /// <param name="graphics">The graphics surface on which to draw the log profile.</param>
    /// <param name="area">The area within which to draw the log profile.</param>
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

            PointF[] top = [.. segment.Select(point => Convert(point, true))];
            PointF[] bottom = [.. segment
                .AsEnumerable()
                .Reverse()
                .Select(point => Convert(point, false))];
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
