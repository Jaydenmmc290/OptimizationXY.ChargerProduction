using Emgu.CV;
using Emgu.CV.CvEnum;

namespace OptimizationXY.Charger.Services.Input
{
    public static class ImageInputService
    {

        /// <summary>
        /// Loads a grayscale image from the specified file path.
        /// </summary>
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
}
