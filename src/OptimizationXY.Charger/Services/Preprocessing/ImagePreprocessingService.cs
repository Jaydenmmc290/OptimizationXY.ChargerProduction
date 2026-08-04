using Emgu.CV;
using Emgu.CV.CvEnum;
using System.Drawing;

namespace OptimizationXY.Charger.Services.Preprocessing
{
    public static class ImagePreprocessingService
    {

        /// <summary>
        /// Builds a binary image from a grayscale image using Gaussian blur and Otsu's thresholding.
        /// </summary>
        public static Mat BuildBinary(Mat gray)
        {
            using Mat blurred = new();
            CvInvoke.GaussianBlur(gray, blurred, new Size(5, 5), 1.5);

            Mat binary = new();
            try
            {
                CvInvoke.Threshold(
                    blurred,
                    binary,
                    0,
                    255,
                    ThresholdType.Binary | ThresholdType.Otsu);

                return binary;
            }
            catch
            {
                binary.Dispose();
                throw;
            }
        }
    }
}
