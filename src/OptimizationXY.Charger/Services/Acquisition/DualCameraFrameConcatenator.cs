using Emgu.CV;
using Emgu.CV.CvEnum;
using OptimizationXY.Charger.Models.Acquisition;
using OptimizationXY.Charger.Models.Core;

namespace OptimizationXY.Charger.Services.Acquisition;

/// <summary>
/// Converts one synchronized head/tail camera pair into one full-length scan
/// image. The processing pipeline receives exactly one concatenated frame per
/// rotation position (36 pairs -> 36 processing frames).
///
/// The concatenation is performed on the raw grayscale measurements. Display
/// normalization is intentionally not used because it would change the laser
/// values consumed by preprocessing and calibration.
/// </summary>
public static class DualCameraFrameConcatenator
{
    /// <summary>
    /// Concatenates the head/tail camera frames into a single scan frame for each
    /// synchronized pair.
    /// </summary>
    /// <param name="headFrames">The list of head camera frames.</param>
    /// <param name="tailFrames">The list of tail camera frames.</param>
    /// <param name="settings">The concatenation settings.</param>
    /// <param name="angularStepDegrees">The angular step in degrees between frames.</param>
    /// <returns>A list of concatenated scan frames.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static List<ScanFrame> ConcatenatePairs(
        IReadOnlyList<ScanFrame> headFrames,
        IReadOnlyList<ScanFrame> tailFrames,
        DualCameraConcatenationSettings settings,
        double angularStepDegrees)
    {
        ArgumentNullException.ThrowIfNull(headFrames);
        ArgumentNullException.ThrowIfNull(tailFrames);
        ArgumentNullException.ThrowIfNull(settings);

        settings.Validate();

        int pairCount = Math.Min(headFrames.Count, tailFrames.Count);

        if (pairCount == 0)
        {
            throw new InvalidOperationException(
                "No synchronized head/tail frame pairs are available.");
        }

        var headByIndex = headFrames
            .OrderBy(frame => frame.FrameIndex)
            .Take(pairCount)
            .ToList();

        var tailByIndex = tailFrames
            .OrderBy(frame => frame.FrameIndex)
            .Take(pairCount)
            .ToList();

        var result = new List<ScanFrame>(pairCount);

        try
        {
            for (int index = 0; index < pairCount; index++)
            {
                ScanFrame head = headByIndex[index];
                ScanFrame tail = tailByIndex[index];

                if (head.GrayImage is null || head.GrayImage.IsEmpty)
                {
                    throw new InvalidOperationException(
                        $"Head frame {head.FrameIndex} is empty.");
                }

                if (tail.GrayImage is null || tail.GrayImage.IsEmpty)
                {
                    throw new InvalidOperationException(
                        $"Tail frame {tail.FrameIndex} is empty.");
                }

                Mat concatenated = Concatenate(
                    head.GrayImage,
                    tail.GrayImage,
                    settings);

                result.Add(new ScanFrame
                {
                    FrameIndex = index,
                    AngleDeg = index * angularStepDegrees,
                    ImagePath = string.Empty,
                    GrayImage = concatenated,
                    IsValid = true,
                    StatusMessage =
                        $"Concatenated head/tail pair {index + 1}"
                });
            }

            return result;
        }
        catch
        {
            DisposeFrames(result);
            throw;
        }
    }

    /// <summary>
    /// Concatenates a single head/tail camera frame pair into a single scan frame.
    /// </summary>
    /// <param name="headSource">The head camera frame to concatenate.</param>
    /// <param name="tailSource">The tail camera frame to concatenate.</param>
    /// <param name="settings">The concatenation settings.</param>
    /// <returns>The concatenated scan frame as a Mat.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static Mat Concatenate(
        Mat headSource,
        Mat tailSource,
        DualCameraConcatenationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(headSource);
        ArgumentNullException.ThrowIfNull(tailSource);
        ArgumentNullException.ThrowIfNull(settings);

        settings.Validate();

        using Mat headGray = ToGray(headSource);
        using Mat tailGray = ToGray(tailSource);

        using Mat headCropped = Crop(
            headGray,
            settings.HeadCropLeftPixels,
            settings.HeadCropRightPixels);

        using Mat tailCropped = Crop(
            tailGray,
            settings.TailCropLeftPixels,
            settings.TailCropRightPixels);

        using Mat headOriented = Orient(
            headCropped,
            settings.ReverseHeadHorizontal,
            verticalOffsetPixels: 0);

        using Mat tailOriented = Orient(
            tailCropped,
            settings.ReverseTailHorizontal,
            settings.TailVerticalOffsetPixels);

        if (settings.RequireEqualHeights &&
            headOriented.Rows != tailOriented.Rows)
        {
            throw new InvalidOperationException(
                $"Head/tail image heights do not match " +
                $"({headOriented.Rows} vs {tailOriented.Rows}). " +
                "Correct camera registration instead of rescaling measurement images.");
        }

        int targetHeight = Math.Max(
            headOriented.Rows,
            tailOriented.Rows);

        using Mat headPrepared = PadToHeight(headOriented, targetHeight);
        using Mat tailPrepared = PadToHeight(tailOriented, targetHeight);

        Mat left = settings.HeadOnLeft ? headPrepared : tailPrepared;
        Mat right = settings.HeadOnLeft ? tailPrepared : headPrepared;

        return ConcatenateWithOverlap(
            left,
            right,
            settings.OverlapPixels);
    }

    /// <summary>
    /// Concatenates two images with a specified number of overlapping pixels. The overlapping region is combined using the maximum pixel value from both images.
    /// </summary>
    /// <param name="left">The left image to concatenate.</param>
    /// <param name="right">The right image to concatenate.</param>
    /// <param name="overlapPixels">The number of pixels to overlap between the two images.</param>
    /// <returns>The concatenated image as a Mat.</returns>
    private static Mat ConcatenateWithOverlap(
        Mat left,
        Mat right,
        int overlapPixels)
    {
        int overlap = Math.Clamp(
            overlapPixels,
            0,
            Math.Min(left.Cols, right.Cols) - 1);

        if (overlap == 0)
        {
            Mat combined = new();
            CvInvoke.HConcat([left, right], combined);
            return combined;
        }

        int outputWidth = left.Cols + right.Cols - overlap;
        Mat output = new(
            left.Rows,
            outputWidth,
            left.Depth,
            left.NumberOfChannels);

        output.SetTo(new Emgu.CV.Structure.MCvScalar(0));

        using (Mat leftDestination = new(
            output,
            new Rectangle(0, 0, left.Cols, left.Rows)))
        {
            left.CopyTo(leftDestination);
        }

        int rightStart = left.Cols - overlap;

        using Mat rightDestination = new(
            output,
            new Rectangle(
                rightStart,
                0,
                right.Cols,
                right.Rows));

        right.CopyTo(rightDestination);

        using Mat leftOverlap = new(
            left,
            new Rectangle(
                left.Cols - overlap,
                0,
                overlap,
                left.Rows));

        using Mat rightOverlap = new(
            right,
            new Rectangle(
                0,
                0,
                overlap,
                right.Rows));

        using Mat outputOverlap = new(
            output,
            new Rectangle(
                rightStart,
                0,
                overlap,
                output.Rows));

        CvInvoke.Max(leftOverlap, rightOverlap, outputOverlap);

        return output;
    }

    /// <summary>
    /// Converts the source image to grayscale if it is not already. If the source image has 3 channels, it is assumed to be in BGR format; if it has 4 channels, it is assumed to be in BGRA format. If the source image has 1 channel, it is returned as-is. An exception is thrown for unsupported channel counts.
    /// </summary>
    /// <param name="source">The source image to convert to grayscale.</param>
    /// <returns>A grayscale version of the source image.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the source image has an unsupported number of channels.</exception>
    private static Mat ToGray(Mat source)
    {
        if (source.NumberOfChannels == 1)
            return source.Clone();

        Mat gray = new();

        ColorConversion conversion = source.NumberOfChannels switch
        {
            3 => ColorConversion.Bgr2Gray,
            4 => ColorConversion.Bgra2Gray,
            _ => throw new InvalidOperationException(
                $"Unsupported camera channel count: {source.NumberOfChannels}.")
        };

        CvInvoke.CvtColor(source, gray, conversion);
        return gray;
    }

    /// <summary>
    /// Crops the source image by removing a specified number of pixels from the left and right sides. The resulting image will have a width of (source.Cols - cropLeft - cropRight). An exception is thrown if the cropping parameters would result in a non-positive width.
    /// </summary>
    /// <param name="source">The source image to crop.</param>
    /// <param name="cropLeft">The number of pixels to remove from the left side of the image.</param>
    /// <param name="cropRight">The number of pixels to remove from the right side of the image.</param>
    /// <returns>A cropped version of the source image.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the cropping parameters would result in a non-positive width.</exception>
    private static Mat Crop(
        Mat source,
        int cropLeft,
        int cropRight)
    {
        int width = source.Cols - cropLeft - cropRight;

        if (width <= 0)
        {
            throw new InvalidOperationException(
                "Camera crop settings remove the entire image width.");
        }

        using Mat region = new(
            source,
            new Rectangle(
                cropLeft,
                0,
                width,
                source.Rows));

        return region.Clone();
    }

    /// <summary>
    /// Orients the source image by optionally flipping it horizontally and applying a vertical offset.
    /// The vertical offset shifts the image up or down by the specified number of pixels, filling any empty space with black pixels. An exception is thrown if the vertical offset would move the entire image out of view.
    /// </summary>
    /// <param name="source">The source image to orient.</param>
    /// <param name="reverseHorizontal">Whether to flip the image horizontally.</param>
    /// <param name="verticalOffsetPixels">The number of pixels to shift the image vertically.</param>
    /// <returns>An oriented version of the source image.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the vertical offset would move the entire image out of view.</exception>
    private static Mat Orient(
        Mat source,
        bool reverseHorizontal,
        int verticalOffsetPixels)
    {
        Mat oriented = source.Clone();

        if (reverseHorizontal)
        {
            Mat flipped = new();
            CvInvoke.Flip(oriented, flipped, FlipType.Horizontal);
            oriented.Dispose();
            oriented = flipped;
        }

        if (verticalOffsetPixels == 0)
            return oriented;

        Mat shifted = new(
            oriented.Rows,
            oriented.Cols,
            oriented.Depth,
            oriented.NumberOfChannels);

        shifted.SetTo(new Emgu.CV.Structure.MCvScalar(0));

        int sourceY = Math.Max(0, -verticalOffsetPixels);
        int destinationY = Math.Max(0, verticalOffsetPixels);
        int copyHeight = oriented.Rows - Math.Abs(verticalOffsetPixels);

        if (copyHeight <= 0)
        {
            oriented.Dispose();
            shifted.Dispose();
            throw new InvalidOperationException(
                "TailVerticalOffsetPixels moves the entire image out of view.");
        }

        using Mat sourceRegion = new(
            oriented,
            new Rectangle(
                0,
                sourceY,
                oriented.Cols,
                copyHeight));

        using Mat destinationRegion = new(
            shifted,
            new Rectangle(
                0,
                destinationY,
                shifted.Cols,
                copyHeight));

        sourceRegion.CopyTo(destinationRegion);
        oriented.Dispose();

        return shifted;
    }
    
    /// <summary>
    /// Pads the source image to the specified target height by adding black pixels to the bottom if necessary.
    /// </summary>
    /// <param name="source">The source image to pad.</param>
    /// <param name="targetHeight">The target height for the padded image.</param>
    /// <returns>A new image padded to the target height.</returns>
    private static Mat PadToHeight(Mat source, int targetHeight)
    {
        if (source.Rows == targetHeight)
            return source.Clone();

        Mat padded = new(
            targetHeight,
            source.Cols,
            source.Depth,
            source.NumberOfChannels);

        padded.SetTo(new Emgu.CV.Structure.MCvScalar(0));

        using Mat destination = new(
            padded,
            new Rectangle(
                0,
                0,
                source.Cols,
                source.Rows));

        source.CopyTo(destination);
        return padded;
    }

    /// <summary>
    /// Disposes of the images in the provided collection of ScanFrame objects. This method ensures that all Mat
    /// objects (GrayImage, BinaryImage, OverlayImage) are properly disposed to free up unmanaged resources.
    /// </summary>
    /// <param name="frames">The collection of ScanFrame objects whose images are to be disposed.</param>
    private static void DisposeFrames(IEnumerable<ScanFrame> frames)
    {
        foreach (ScanFrame frame in frames)
        {
            frame.GrayImage?.Dispose();
            frame.BinaryImage?.Dispose();
            frame.OverlayImage?.Dispose();
        }
    }
}
