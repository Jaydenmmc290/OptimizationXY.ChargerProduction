using OptimizationXY.Charger.Models.Core;

namespace OptimizationXY.Charger.Services.Scan
{
    public static class ScanFrameLoader
    {
        public static List<ScanFrame> LoadFromFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException(folderPath);

            var files = Directory.GetFiles(folderPath)
    .Where(f =>
        f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
        f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
        f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
    .OrderBy(f => ExtractFrameNumber(Path.GetFileNameWithoutExtension(f)))
    .ThenBy(f => f)
    .ToList();

            if (files.Count == 0)
                throw new InvalidOperationException("No image files found in selected folder.");

            double angleStepDeg = 360.0 / files.Count;

            var frames = new List<ScanFrame>();

            for (int i = 0; i < files.Count; i++)
            {
                frames.Add(new ScanFrame
                {
                    FrameIndex = i,
                    ImagePath = files[i],
                    AngleDeg = i * angleStepDeg
                });
            }

            return frames;
        }

        /// <summary>
        /// Extracts the first sequence of digits from the file name (without extension) and returns it as an integer.
        /// If no digits are found, returns int.MaxValue to ensure such files are sorted last.
        /// </summary>
        private static int ExtractFrameNumber(string fileNameWithoutExtension)
        {
            string digits = new string(fileNameWithoutExtension
                .Where(char.IsDigit)
                .ToArray());

            if (int.TryParse(digits, out int number))
                return number;

            return int.MaxValue;
        }
    }
}
