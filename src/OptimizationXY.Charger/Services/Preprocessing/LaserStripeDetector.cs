using Emgu.CV;
using OptimizationXY.Charger.Models.Geometry;

namespace OptimizationXY.Charger.Services.Preprocessing;

/// <summary>
/// Finds the laser line in the black/white mask made by ImagePreprocessingService.
/// </summary>
/// <remarks>
/// For every image column (X) it looks down the column for runs of white
/// pixels 1-25 pixels tall (taller runs are glare, not the laser). If a column
/// has several runs, the one closest to the previous column's line is taken
/// (or the top-most one in the first column), so the line is followed
/// smoothly across the image. The centre row of the run is the laser row.
/// Then the column results are split into smooth pieces (no gap over 3
/// columns, no jump over 12 rows) and, by default, only the LONGEST piece of
/// at least 30 columns is kept; anything else is treated as a reflection.
/// Optional (off by default, compare with --replay-batch first):
/// KeepAllStripeSegments also keeps pieces that line up with the longest one;
/// SubPixelStripeCenter refines each centre from the grey image brightness.
/// </remarks>
public static class LaserStripeDetector
{
    /// <summary>
    /// Detects the laser stripe in a binary image and returns a list of LaserProfileColumn objects representing the detected stripe.
    /// </summary>
    /// <param name="binaryImage">The input binary image.</param>
    /// <returns>A list of LaserProfileColumn objects representing the detected stripe.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the binary image is empty or no laser stripe is detected.</exception>
    public static List<LaserProfileColumn> Detect(Mat binaryImage) =>
        Detect(binaryImage, grayImage: null, keepAllAlignedSegments: false, subPixelCenter: false);

    /// <summary>
    /// Detects the laser stripe. With both options off this is exactly the
    /// original detection.
    /// </summary>
    /// <param name="binaryImage">Thresholded image the stripe is found in.</param>
    /// <param name="grayImage">Grey image of the same frame; needed only for <paramref name="subPixelCenter"/>.</param>
    /// <param name="keepAllAlignedSegments">
    /// Keep every stripe piece that lines up with the longest one, instead of only
    /// the longest piece (RotatingScan.KeepAllStripeSegments).
    /// </param>
    /// <param name="subPixelCenter">
    /// Replace the run midpoint with the brightness-weighted centre of the stripe
    /// in the grey image (RotatingScan.SubPixelStripeCenter).
    /// </param>
    public static List<LaserProfileColumn> Detect(
        Mat binaryImage,
        Mat? grayImage,
        bool keepAllAlignedSegments,
        bool subPixelCenter)
    {
        if (binaryImage == null || binaryImage.IsEmpty)
            throw new InvalidOperationException("Binary image is empty.");

        List<LaserProfileColumn> rawProfile = [];

        // Read the image once into a typed array (same pixels as before, without
        // casting the array again for every pixel).
        Array data = binaryImage.GetData();
        byte[,]? data2 = data as byte[,];
        byte[,,]? data3 = data as byte[,,];

        byte GetPixel(int y, int x) =>
            data2 is not null ? data2[y, x] : data3![y, x, 0];

        const int minStripeHeightPixels = 1;
        // Runs taller than this are glare/reflections, not the thin laser line.
        const int maxStripeHeightPixels = 25;

        // Laser row of the previous column; used to follow the same line.
        double? previousCenterY = null;

        for (int x = 0; x < binaryImage.Width; x++)
        {
            List<(int TopY, int BottomY, int Height, double CenterY)> runs = [];

            int y = 0;

            while (y < binaryImage.Height)
            {
                while (y < binaryImage.Height && GetPixel(y, x) == 0)
                    y++;

                if (y >= binaryImage.Height)
                    break;

                int topY = y;

                while (y < binaryImage.Height && GetPixel(y, x) > 0)
                    y++;

                int bottomY = y - 1;
                int height = bottomY - topY + 1;

                if (height >= minStripeHeightPixels &&
                    height <= maxStripeHeightPixels)
                {
                    double centerY = (topY + bottomY) / 2.0;
                    runs.Add((topY, bottomY, height, centerY));
                }
            }

            if (runs.Count == 0)
                continue;

            var (TopY, BottomY, Height, CenterY) =
                previousCenterY.HasValue
                    ? runs
                        .OrderBy(r => Math.Abs(r.CenterY - previousCenterY.Value))
                        .ThenBy(r => r.TopY)
                        .First()
                    : runs
                        .OrderBy(r => r.TopY)
                        .First();

            previousCenterY = CenterY;

            rawProfile.Add(new LaserProfileColumn
            {
                X = x,
                TopY = TopY,
                BottomY = BottomY,
                StripeHeightPixels = Height,
                LaserCenterY = CenterY,
                // Thinner stripe = sharper laser = higher confidence (1 for a 1-pixel run).
                Confidence = 1.0 / Height
            });
        }

        if (rawProfile.Count == 0)
            throw new InvalidOperationException("No laser stripe detected.");

        List<LaserProfileColumn> cleanProfile =
            keepAllAlignedSegments
                ? KeepAlignedSegments(
                    rawProfile,
                    maxCenterJumpPixels: 12.0,
                    minSegmentColumns: 30)
                : KeepLongestSmoothSegment(
                    rawProfile,
                    maxCenterJumpPixels: 12.0,
                    minSegmentColumns: 30);

        if (cleanProfile.Count == 0)
            throw new InvalidOperationException("No clean laser stripe segment detected.");

        if (subPixelCenter && grayImage is not null && !grayImage.IsEmpty)
            RefineCentersFromGray(cleanProfile, grayImage);

        return cleanProfile;
    }

    /// <summary>
    /// Pixels above and below the thresholded stripe included in the
    /// brightness-weighted centre, so the dim edges of the stripe count too.
    /// </summary>
    private const int SubPixelMarginPixels = 3;

    /// <summary>
    /// Replaces each column's centre with the brightness-weighted centre of the
    /// stripe in the grey image. The darkest pixel in the window is taken as the
    /// background and subtracted. A column whose window has no brightness above
    /// the background keeps its original centre.
    /// </summary>
    internal static void RefineCentersFromGray(
        List<LaserProfileColumn> profile,
        Mat grayImage)
    {
        Array data = grayImage.GetData();
        byte[,]? g2 = data as byte[,];
        byte[,,]? g3 = data as byte[,,];

        if (g2 is null && g3 is null)
            return;

        int height = grayImage.Rows;
        int width = grayImage.Cols;

        byte Gray(int y, int x) => g2 is not null ? g2[y, x] : g3![y, x, 0];

        foreach (LaserProfileColumn column in profile)
        {
            if (column.X < 0 || column.X >= width)
                continue;

            int top = Math.Max(0, column.TopY - SubPixelMarginPixels);
            int bottom = Math.Min(height - 1, column.BottomY + SubPixelMarginPixels);

            int background = 255;
            for (int y = top; y <= bottom; y++)
                background = Math.Min(background, Gray(y, column.X));

            double weightSum = 0.0;
            double weightedY = 0.0;

            for (int y = top; y <= bottom; y++)
            {
                double weight = Gray(y, column.X) - background;
                if (weight <= 0.0)
                    continue;

                weightSum += weight;
                weightedY += weight * y;
            }

            if (weightSum > 0.0)
                column.LaserCenterY = weightedY / weightSum;
        }
    }

    /// <summary>
    /// Keeps the longest smooth piece of the stripe plus every other piece of at
    /// least <paramref name="minSegmentColumns"/> columns that lines up with the
    /// pieces already kept: its near end must be within
    /// <paramref name="maxCenterJumpPixels"/> plus one pixel per 10 columns of gap
    /// of the nearest kept piece's end. Pieces are taken in order of length, so a
    /// short reflection cannot pull the line away from the long pieces.
    /// </summary>
    internal static List<LaserProfileColumn> KeepAlignedSegments(
        List<LaserProfileColumn> profile,
        double maxCenterJumpPixels,
        int minSegmentColumns)
    {
        List<List<LaserProfileColumn>> segments = SplitSmoothSegments(
            profile,
            maxCenterJumpPixels,
            minSegmentColumns);

        if (segments.Count == 0)
            return [];

        var ordered = segments.OrderByDescending(segment => segment.Count).ToList();
        var kept = new List<List<LaserProfileColumn>> { ordered[0] };

        bool addedOne;
        var remaining = ordered.Skip(1).ToList();

        do
        {
            addedOne = false;

            foreach (List<LaserProfileColumn> candidate in remaining.ToList())
            {
                int candidateStart = candidate[0].X;
                int candidateEnd = candidate[^1].X;

                // Never overlap a kept piece.
                if (kept.Any(piece => candidateStart <= piece[^1].X && candidateEnd >= piece[0].X))
                {
                    remaining.Remove(candidate);
                    continue;
                }

                // Nearest kept piece by gap along X, and the ends that face each other.
                double bestGap = double.PositiveInfinity;
                double jump = double.PositiveInfinity;

                foreach (List<LaserProfileColumn> piece in kept)
                {
                    if (candidateStart > piece[^1].X)
                    {
                        double gap = candidateStart - piece[^1].X;
                        if (gap < bestGap)
                        {
                            bestGap = gap;
                            jump = Math.Abs(candidate[0].LaserCenterY - piece[^1].LaserCenterY);
                        }
                    }
                    else if (candidateEnd < piece[0].X)
                    {
                        double gap = piece[0].X - candidateEnd;
                        if (gap < bestGap)
                        {
                            bestGap = gap;
                            jump = Math.Abs(candidate[^1].LaserCenterY - piece[0].LaserCenterY);
                        }
                    }
                }

                if (double.IsFinite(bestGap) &&
                    jump <= maxCenterJumpPixels + bestGap / 10.0)
                {
                    kept.Add(candidate);
                    remaining.Remove(candidate);
                    addedOne = true;
                }
            }
        }
        while (addedOne && remaining.Count > 0);

        return [.. kept.SelectMany(piece => piece).OrderBy(column => column.X)];
    }

    /// <summary>
    /// Splits the column results into smooth pieces: a new piece starts at a gap
    /// of more than 3 columns or a jump of more than <paramref name="maxCenterJumpPixels"/>
    /// rows. Pieces shorter than <paramref name="minSegmentColumns"/> are dropped.
    /// </summary>
    private static List<List<LaserProfileColumn>> SplitSmoothSegments(
        List<LaserProfileColumn> profile,
        double maxCenterJumpPixels,
        int minSegmentColumns)
    {
        List<List<LaserProfileColumn>> segments = [];
        List<LaserProfileColumn> current = [];

        foreach (var p in profile.OrderBy(p => p.X))
        {
            if (current.Count == 0)
            {
                current.Add(p);
                continue;
            }

            var previous = current[^1];

            bool xIsContinuous = p.X - previous.X <= 3;
            bool yIsSmooth =
                Math.Abs(p.LaserCenterY - previous.LaserCenterY) <= maxCenterJumpPixels;

            if (xIsContinuous && yIsSmooth)
            {
                current.Add(p);
            }
            else
            {
                if (current.Count >= minSegmentColumns)
                    segments.Add(current);

                current = [p];
            }
        }

        if (current.Count >= minSegmentColumns)
            segments.Add(current);

        return segments;
    }

    /// <summary>
    /// Keeps the longest smooth segment of the laser profile (default behaviour).
    /// Same splitting rule as <see cref="SplitSmoothSegments"/>.
    /// </summary>
    /// <param name="profile">The list of LaserProfileColumn objects representing the laser profile.</param>
    /// <param name="maxCenterJumpPixels">The maximum allowed jump in the laser center Y position between consecutive columns.</param>
    /// <param name="minSegmentColumns">The minimum number of columns required for a segment to be considered valid.</param>
    /// <returns>The longest smooth segment of the laser profile.</returns>
    private static List<LaserProfileColumn> KeepLongestSmoothSegment(
        List<LaserProfileColumn> profile,
        double maxCenterJumpPixels,
        int minSegmentColumns)
    {
        List<List<LaserProfileColumn>> segments = [];
        List<LaserProfileColumn> current = [];

        foreach (var p in profile.OrderBy(p => p.X))
        {
            if (current.Count == 0)
            {
                current.Add(p);
                continue;
            }

            var previous = current[^1];

            bool xIsContinuous = p.X - previous.X <= 3;
            bool yIsSmooth =
                Math.Abs(p.LaserCenterY - previous.LaserCenterY) <= maxCenterJumpPixels;

            if (xIsContinuous && yIsSmooth)
            {
                current.Add(p);
            }
            else
            {
                if (current.Count >= minSegmentColumns)
                    segments.Add(current);

                current = [ p ];
            }
        }

        if (current.Count >= minSegmentColumns)
            segments.Add(current);

        if (segments.Count == 0)
            return [];

        return segments
            .OrderByDescending(s => s.Count)
            .First();
    }
}
