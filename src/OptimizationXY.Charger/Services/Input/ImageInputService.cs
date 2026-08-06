using Emgu.CV;
using Emgu.CV.CvEnum;

namespace OptimizationXY.Charger.Services.Input;

public static class ImageInputService
{

    /// <summary>
    /// Loads a grayscale image from the specified path.
    /// </summary>
    /// <param name="path">The path to the image file.</param>
    /// <returns>The loaded grayscale image as a Mat object.</returns>
    /// <exception cref="ArgumentException">Thrown if the image path is null, empty, or consists only of white-space characters.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the image file does not exist at the specified path.</exception>
    /// <exception cref="InvalidDataException">Thrown if the image could not be decoded.</exception>
    public static Mat LoadGrayImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException(
                "Image path is empty.");

        if (!File.Exists(path))
            throw new FileNotFoundException(
                path);

        Mat image =
            CvInvoke.Imread(
                path,
                ImreadModes.Grayscale);

        if (image.IsEmpty)
        {
            image.Dispose();
            throw new InvalidDataException(
                $"Image could not be decoded: {path}");
        }

        return image;
    }
}
