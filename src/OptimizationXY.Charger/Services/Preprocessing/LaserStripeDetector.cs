using Emgu.CV;
using OptimizationXY.Charger.Models.Geometry;

namespace OptimizationXY.Charger.Services.Preprocessing
{
    public static class LaserStripeDetector
    {
        public static List<LaserProfileColumn> Detect(Mat binaryImage)
        {
            if (binaryImage == null || binaryImage.IsEmpty)
                throw new InvalidOperationException("Binary image is empty.");

            List<LaserProfileColumn> rawProfile = new();

            Array data = binaryImage.GetData();

            byte GetPixel(int y, int x)
            {
                if (data.Rank == 2)
                    return ((byte[,])data)[y, x];

                return ((byte[,,])data)[y, x, 0];
            }

            const int minStripeHeightPixels = 1;
            const int maxStripeHeightPixels = 25;

            double? previousCenterY = null;

            for (int x = 0; x < binaryImage.Width; x++)
            {
                List<(int TopY, int BottomY, int Height, double CenterY)> runs = new();

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

                var chosen =
                    previousCenterY.HasValue
                        ? runs
                            .OrderBy(r => Math.Abs(r.CenterY - previousCenterY.Value))
                            .ThenBy(r => r.TopY)
                            .First()
                        : runs
                            .OrderBy(r => r.TopY)
                            .First();

                previousCenterY = chosen.CenterY;

                rawProfile.Add(new LaserProfileColumn
                {
                    X = x,
                    TopY = chosen.TopY,
                    BottomY = chosen.BottomY,
                    StripeHeightPixels = chosen.Height,
                    LaserCenterY = chosen.CenterY,
                    Confidence = 1.0 / chosen.Height
                });
            }

            if (rawProfile.Count == 0)
                throw new InvalidOperationException("No laser stripe detected.");

            List<LaserProfileColumn> cleanProfile =
                KeepLongestSmoothSegment(
                    rawProfile,
                    maxCenterJumpPixels: 12.0,
                    minSegmentColumns: 30);

            if (cleanProfile.Count == 0)
                throw new InvalidOperationException("No clean laser stripe segment detected.");

            return cleanProfile;
        }

        private static List<LaserProfileColumn> KeepLongestSmoothSegment(
            List<LaserProfileColumn> profile,
            double maxCenterJumpPixels,
            int minSegmentColumns)
        {
            List<List<LaserProfileColumn>> segments = new();
            List<LaserProfileColumn> current = new();

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

                    current = new List<LaserProfileColumn> { p };
                }
            }

            if (current.Count >= minSegmentColumns)
                segments.Add(current);

            if (segments.Count == 0)
                return new List<LaserProfileColumn>();

            return segments
                .OrderByDescending(s => s.Count)
                .First();
        }
    }
}
