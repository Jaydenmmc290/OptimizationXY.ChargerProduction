using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Models.Core;
using OptimizationXY.Charger.Models.Geometry;

namespace OptimizationXY.Charger.Services.PointCloud;

/// <summary>
/// Builds a 3D point cloud from a series of rotating scan frames.
/// </summary>
public static class RotatingScanPointCloudBuilder
{
    /// <summary>
    /// Builds a 3D point cloud from a series of rotating scan frames.
    /// </summary>
    /// <param name="frames">The list of scan frames.</param>
    /// <param name="lengthPixelsPerMM">The length in pixels per millimeter.</param>
    /// <param name="settings">The application settings.</param>
    /// <param name="fillMissingPoints">Whether to fill missing points.</param>
    /// <returns>A list of 3D points representing the point cloud.</returns>
    public static List<Point3D> BuildFromFrames(
        List<ScanFrame> frames,
        double lengthPixelsPerMM,
        AppSettings settings,
        bool fillMissingPoints)
    {
        if (frames == null || frames.Count == 0)
            throw new InvalidOperationException("No rotating scan frames loaded.");

        if (lengthPixelsPerMM <= 0)
            throw new InvalidOperationException("LengthPixelsPerMM must be greater than zero.");

        var usableFrames = frames
            .Where(f => f.IsValid)
            .Where(f => f.Profile != null)
            .Where(f => f.Profile.Count > 0)
            .OrderBy(f => f.AngleDeg)
            .ToList();

        if (usableFrames.Count == 0)
            throw new InvalidOperationException("No usable scan frames.");

        int globalMinX = usableFrames
            .SelectMany(f => f.Profile)
            .Min(p => p.X);

        int globalMaxX = usableFrames
            .SelectMany(f => f.Profile)
            .Max(p => p.X);

        int xStep = EstimateTypicalXStep(usableFrames);
        var xColumns = BuildXColumns(globalMinX, globalMaxX, xStep);

        int frameCount = usableFrames.Count;
        int xCount = xColumns.Count;

        double[,] radiusGrid = new double[frameCount, xCount];

        for (int i = 0; i < frameCount; i++)
        {
            for (int j = 0; j < xCount; j++)
            {
                radiusGrid[i, j] = double.NaN;
            }
        }

        var xIndexLookup = xColumns
            .Select((x, index) => new { x, index })
            .ToDictionary(v => v.x, v => v.index);

        // ------------------------------------------------------------
        // 1. Load known radius measurements into the grid.
        // ------------------------------------------------------------
        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            var frame = usableFrames[frameIndex];

            var groupedColumns = frame.Profile
                .Where(p => IsValidRadius(p.RadiusMM))
                .GroupBy(p => p.X)
                .Select(g => new
                {
                    X = g.Key,
                    RadiusMM = g.Average(p => p.RadiusMM)
                });

            foreach (var column in groupedColumns)
            {
                if (!xIndexLookup.TryGetValue(column.X, out int xIndex))
                    continue;

                radiusGrid[frameIndex, xIndex] = column.RadiusMM;
            }
        }

        if (fillMissingPoints && settings.RotatingScan.FillMissingPoints)
        {
            FillSmallLengthwiseGaps(
                radiusGrid,
                xColumns,
                frameCount,
                xCount,
                settings.RotatingScan.DisplayFillMaxLinearGapPixels);

            FillSmallAngularGaps(
                radiusGrid,
                frameCount,
                xCount,
                settings.RotatingScan.DisplayFillMaxAngularNeighborFrames);
        }

        // ------------------------------------------------------------
        // 2. Convert completed radius grid into 3D points.
        //
        // Every later stage treats Z = 0 as the TAIL end and maximum Z as
        // the HEAD end. Image X runs left to right, so when the head camera
        // is placed on the left of the concatenated image the axial
        // coordinate must be measured from the right-hand edge.
        // ------------------------------------------------------------
        bool headOnImageLeft =
            settings.Acquisition.Concatenation.HeadOnLeft;

        var points = new List<Point3D>();

        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            var frame = usableFrames[frameIndex];

            double angleRad = frame.AngleDeg * Math.PI / 180.0;
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);

            for (int xIndex = 0; xIndex < xCount; xIndex++)
            {
                double radius = radiusGrid[frameIndex, xIndex];

                if (!IsValidRadius(radius))
                    continue;

                double z = headOnImageLeft
                    ? (globalMaxX - xColumns[xIndex]) / lengthPixelsPerMM
                    : (xColumns[xIndex] - globalMinX) / lengthPixelsPerMM;

                double x = radius * cos;
                double y = radius * sin;

                points.Add(new Point3D
                {
                    X = x,
                    Y = y,
                    Z = z
                });
            }
        }

        return [.. points
            .OrderBy(p => p.Z)
            .ThenBy(p => Math.Atan2(p.Y, p.X))];
    }

    /// <summary>
    /// Converts the raw point cloud into a cylindrical field in the scanner /
    /// current-spindle coordinate frame. The current spindle axis is (0,0).
    ///
    /// This is intentionally separate from RadiusFieldGenerator.Build, which
    /// creates the fitted-centerline-relative field used for shape diagnostics.
    /// Machine-position optimization must retain the PointCloud XY origin.
    /// </summary>
    public static List<RadiusFieldPoint> BuildMachineFrameRadiusField(
        List<Point3D> pointCloud)
    {
        if (pointCloud == null || pointCloud.Count == 0)
            return [];

        var field = new List<RadiusFieldPoint>(pointCloud.Count);

        foreach (Point3D point in pointCloud)
        {
            if (!double.IsFinite(point.X) ||
                !double.IsFinite(point.Y) ||
                !double.IsFinite(point.Z))
            {
                continue;
            }

            double radiusMM = Math.Sqrt(
                point.X * point.X +
                point.Y * point.Y);

            if (!double.IsFinite(radiusMM) || radiusMM <= 0.0)
                continue;

            double angleDeg =
                Math.Atan2(point.Y, point.X) *
                180.0 /
                Math.PI;

            if (angleDeg < 0.0)
                angleDeg += 360.0;

            field.Add(new RadiusFieldPoint
            {
                Z = point.Z,
                X = point.X,
                Y = point.Y,
                RadiusMM = radiusMM,
                AngleDeg = angleDeg
            });
        }

        return field;
    }

    // ------------------------------------------------------------
    // Fills missing radius values along the log length.
    //
    // Example:
    // known radius ---- missing ---- known radius
    //
    // It linearly blends between the two nearby good values.
    // This is safe for small gaps.
    // ------------------------------------------------------------
    /// <summary>
    /// Fills missing radius values along the log length.
    /// </summary>
    /// <param name="radiusGrid">The grid of radius values.</param>
    /// <param name="xColumns">The list of x column indices.</param>
    /// <param name="frameCount">The number of frames.</param>
    /// <param name="xCount">The number of x columns.</param>
    /// <param name="maxLinearGapPixels">The maximum linear gap in pixels to fill.</param>
    private static void FillSmallLengthwiseGaps(
        double[,] radiusGrid,
        List<int> xColumns,
        int frameCount,
        int xCount,
        int maxLinearGapPixels)
    {
        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            int lastValidIndex = -1;

            for (int xIndex = 0; xIndex < xCount; xIndex++)
            {
                if (!IsValidRadius(radiusGrid[frameIndex, xIndex]))
                    continue;

                if (lastValidIndex >= 0 && xIndex - lastValidIndex > 1)
                {
                    int leftIndex = lastValidIndex;
                    int rightIndex = xIndex;

                    int gapPixels = xColumns[rightIndex] - xColumns[leftIndex];

                    if (gapPixels <= maxLinearGapPixels)
                    {
                        double leftRadius = radiusGrid[frameIndex, leftIndex];
                        double rightRadius = radiusGrid[frameIndex, rightIndex];

                        for (int fillIndex = leftIndex + 1; fillIndex < rightIndex; fillIndex++)
                        {
                            double t =
                                (double)(xColumns[fillIndex] - xColumns[leftIndex]) /
                                (xColumns[rightIndex] - xColumns[leftIndex]);

                            radiusGrid[frameIndex, fillIndex] =
                                leftRadius + (rightRadius - leftRadius) * t;
                        }
                    }
                }

                lastValidIndex = xIndex;
            }
        }
    }

    // ------------------------------------------------------------
    // Fills missing values by looking at nearby rotation frames.
    //
    // This helps when one angle missed a point but the neighboring
    // angles saw roughly the same area.
    // ------------------------------------------------------------
    /// <summary>
    /// Fills missing values by looking at nearby rotation frames.
    /// </summary>
    /// <param name="radiusGrid">The grid of radius values.</param>
    /// <param name="frameCount">The number of frames.</param>
    /// <param name="xCount">The number of x columns.</param>
    /// <param name="maxAngularNeighborFrames">The maximum number of neighboring frames to consider.</param>
    private static void FillSmallAngularGaps(
        double[,] radiusGrid,
        int frameCount,
        int xCount,
        int maxAngularNeighborFrames)
    {
        if (frameCount < 3)
            return;

        double[,] filledCopy = (double[,])radiusGrid.Clone();

        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            for (int xIndex = 0; xIndex < xCount; xIndex++)
            {
                if (IsValidRadius(radiusGrid[frameIndex, xIndex]))
                    continue;

                double weightedSum = 0.0;
                double weightTotal = 0.0;

                for (int offset = 1; offset <= maxAngularNeighborFrames; offset++)
                {
                    int prevFrame = WrapIndex(frameIndex - offset, frameCount);
                    int nextFrame = WrapIndex(frameIndex + offset, frameCount);

                    double weight = 1.0 / offset;

                    double prevRadius = radiusGrid[prevFrame, xIndex];
                    if (IsValidRadius(prevRadius))
                    {
                        weightedSum += prevRadius * weight;
                        weightTotal += weight;
                    }

                    double nextRadius = radiusGrid[nextFrame, xIndex];
                    if (IsValidRadius(nextRadius))
                    {
                        weightedSum += nextRadius * weight;
                        weightTotal += weight;
                    }
                }

                if (weightTotal > 0)
                {
                    filledCopy[frameIndex, xIndex] = weightedSum / weightTotal;
                }
            }
        }

        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            for (int xIndex = 0; xIndex < xCount; xIndex++)
            {
                radiusGrid[frameIndex, xIndex] = filledCopy[frameIndex, xIndex];
            }
        }
    }

    /// <summary>
    /// Checks if the given radius is valid (greater than zero and not NaN or Infinity).
    /// </summary>
    /// <param name="radius">The radius value to check.</param>
    /// <returns>True if the radius is valid; otherwise, false.</returns>
    private static bool IsValidRadius(double radius)
    {
        return radius > 0 &&
               !double.IsNaN(radius) &&
               !double.IsInfinity(radius);
    }

    /// <summary>
    /// Wraps the index to ensure it stays within the bounds of the count.
    /// </summary>
    /// <param name="index">The index to wrap.</param>
    /// <param name="count">The total number of elements.</param>
    /// <returns>The wrapped index.</returns>
    private static int WrapIndex(int index, int count)
    {
        while (index < 0)
            index += count;

        while (index >= count)
            index -= count;

        return index;
    }

    /// <summary>
    /// Builds a list of x column indices from the minimum to maximum x values with the specified step.
    /// </summary>
    /// <param name="minX">The minimum x value.</param>
    /// <param name="maxX">The maximum x value.</param>
    /// <param name="step">The step size between x values.</param>
    /// <returns>A list of x column indices.</returns>
    private static List<int> BuildXColumns(int minX, int maxX, int step)
    {
        if (step <= 0)
            step = 1;

        var result = new List<int>();

        for (int x = minX; x <= maxX; x += step)
        {
            result.Add(x);
        }

        if (result.Count == 0 || result[^1] != maxX)
            result.Add(maxX);

        return result;
    }

    /// <summary>
    /// Estimates the typical step size between x values in the scan frames.
    /// </summary>
    /// <param name="frames">The list of scan frames.</param>
    /// <returns>The estimated typical step size between x values.</returns>
    private static int EstimateTypicalXStep(List<ScanFrame> frames)
    {
        var diffs = frames
            .SelectMany(f => f.Profile.Select(p => p.X))
            .Distinct()
            .OrderBy(x => x)
            .Zip(
                frames.SelectMany(f => f.Profile.Select(p => p.X))
                    .Distinct()
                    .OrderBy(x => x)
                    .Skip(1),
                (a, b) => b - a)
            .Where(d => d > 0)
            .OrderBy(d => d)
            .ToList();

        if (diffs.Count == 0)
            return 1;

        int middle = diffs.Count / 2;
        int step = diffs[middle];

        return Math.Max(1, step);
    }
}
