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
