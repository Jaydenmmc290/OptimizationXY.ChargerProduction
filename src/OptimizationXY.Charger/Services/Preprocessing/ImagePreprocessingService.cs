using Emgu.CV;
using Emgu.CV.CvEnum;


namespace OptimizationXY.Charger.Services.Preprocessing;

/// <summary>
/// First image step for every frame (production, calibration and replay all
/// use this same code): turns the greyscale joined image into a black/white
/// mask where white = laser light. LaserStripeDetector then finds the line in
/// that mask.
/// </summary>
public static class ImagePreprocessingService
{

    /// <summary>
    /// Builds a binary image from a grayscale image using Gaussian blur and Otsu's thresholding.
    /// </summary>
    /// <param name="gray">The input grayscale image.</param>
    /// <returns>The resulting binary image.</returns>
    public static Mat BuildBinary(Mat gray)
    {
        using Mat blurred = new();
        // Light blur (5x5, sigma 1.5) removes single-pixel noise and speckle so
        // the threshold does not split the laser line into dots.
        CvInvoke.GaussianBlur(gray, blurred, new Size(5, 5), 1.5);

        Mat binary = new();
        try
        {
            // Otsu's method picks the threshold automatically from the image's own
            // brightness histogram (it separates "dark background" from "bright laser"),
            // so it adapts to small changes in laser brightness or exposure.
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
