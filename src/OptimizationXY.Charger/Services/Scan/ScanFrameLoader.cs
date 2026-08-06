using OptimizationXY.Charger.Models.Core;

namespace OptimizationXY.Charger.Services.Scan;

/// <summary>
/// Provides functionality to load scan frames from a specified folder containing image files.
/// </summary>
public static class ScanFrameLoader
{

    /// <summary>
    /// Loads scan frames from the specified folder. The method looks for image files (PNG, JPG, JPEG, BMP) in the folder, sorts them based
    /// on the numeric part of their filenames, and calculates the angle for each frame based on the total number of frames.
    /// </summary>
    /// <param name="folderPath">The path to the folder containing the image files.</param>
    /// <returns>A list of <see cref="ScanFrame"/> objects representing the loaded scan frames.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown if the specified folder does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown if no image files are found in the folder.</exception>
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
    /// Extracts the numeric part from the filename (without extension) to determine the frame number. If no numeric part is found, it
    /// returns int.MaxValue to ensure such files are sorted last.
    /// </summary>
    /// <param name="fileNameWithoutExtension">The filename without its extension.</param>
    /// <returns>The extracted frame number or int.MaxValue if no numeric part is found.</returns>
    private static int ExtractFrameNumber(string fileNameWithoutExtension)
    {
        string digits = new([.. fileNameWithoutExtension.Where(char.IsDigit)]);

        if (int.TryParse(digits, out int number))
            return number;

        return int.MaxValue;
    }
}
