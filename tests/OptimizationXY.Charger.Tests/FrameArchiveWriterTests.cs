using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using OptimizationXY.Charger.Services.Storage;
using Xunit;

namespace OptimizationXY.Charger.Tests;

/// <summary>
/// Frame PNGs written on the background archive thread: every frame ends up on
/// disk (none dropped), and the caller's image is not touched.
/// </summary>
public class FrameArchiveWriterTests
{
    [Fact]
    public void QueuedFramesAreAllWrittenAndTheCallersImageStaysUsable()
    {
        string folder = Path.Combine(Path.GetTempPath(), "oxy-archive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            using var image = new Mat(40, 60, DepthType.Cv8U, 1);
            image.SetTo(new MCvScalar(128));

            for (int i = 1; i <= 5; i++)
                FrameArchiveWriter.Write(Path.Combine(folder, $"Frame_{i:0000}.png"), image, inBackground: true, capacity: 300);

            Assert.True(FrameArchiveWriter.Flush(TimeSpan.FromSeconds(20)));

            for (int i = 1; i <= 5; i++)
                Assert.True(new FileInfo(Path.Combine(folder, $"Frame_{i:0000}.png")).Length > 0);

            Assert.False(image.IsEmpty);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AForegroundWriteHappensImmediately()
    {
        string folder = Path.Combine(Path.GetTempPath(), "oxy-archive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            using var image = new Mat(40, 60, DepthType.Cv8U, 1);
            image.SetTo(new MCvScalar(200));
            string path = Path.Combine(folder, "Frame_0001.png");

            bool queued = FrameArchiveWriter.Write(path, image, inBackground: false, capacity: 1);

            Assert.False(queued);
            Assert.True(new FileInfo(path).Length > 0);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
