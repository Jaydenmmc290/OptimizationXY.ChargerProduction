using System.Collections.Concurrent;
using System.Diagnostics;
using Emgu.CV;
using NLog;

namespace OptimizationXY.Charger.Services.Storage;

// =============================================================================
// FrameArchiveWriter - writes the scan's frame PNGs on a background thread.
// -----------------------------------------------------------------------------
// Encoding ~90 lossless PNGs per block used to run inside the scan save, and the
// next block cannot start processing until that save has finished (it shares
// PipelineState). A slow disk therefore pushed the NEXT block's result toward
// the PLC's 18 s fallback. The Rebuild program hit the same thing (F-19: one
// archive write blocked for 23 s and the scan was lost) and moved its archive to
// a background queue.
//
// Here the save hands each frame (a private copy) to this queue and moves on.
// Unlike Rebuild, frames are never dropped: if the queue is full the frame is
// written straight away in the caller, so every production scan keeps all of
// its frames (audit), only the hand-off speed changes. Failures are logged.
// =============================================================================

/// <summary>Background PNG writer for scan frames. One per process.</summary>
public static class FrameArchiveWriter
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly Lock StartLock = new();

    private static BlockingCollection<(string Path, Mat Image)>? _queue;
    private static Thread? _thread;
    private static int _pending;
    private static int _failed;

    /// <summary>Frames handed over and not yet written.</summary>
    public static int Backlog => Volatile.Read(ref _pending);

    /// <summary>Frames that could not be written since start-up.</summary>
    public static int FailedCount => Volatile.Read(ref _failed);

    /// <summary>
    /// Writes <paramref name="image"/> to <paramref name="path"/> as PNG. With
    /// <paramref name="inBackground"/> a copy is queued and the call returns at
    /// once; when the queue already holds <paramref name="capacity"/> frames, or
    /// in the foreground, it is written now. The caller keeps its own image.
    /// </summary>
    /// <returns>True if queued, false if written now (or the write failed).</returns>
    public static bool Write(string path, Mat image, bool inBackground, int capacity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(image);

        if (inBackground)
        {
            BlockingCollection<(string Path, Mat Image)> queue = EnsureStarted();

            if (Volatile.Read(ref _pending) < Math.Max(1, capacity))
            {
                Mat copy = image.Clone();
                Interlocked.Increment(ref _pending);

                if (queue.TryAdd((path, copy)))
                    return true;

                Interlocked.Decrement(ref _pending);
                copy.Dispose();
            }

            Logger.Warn(
                "Frame archive queue is full ({Backlog} frames waiting); writing {Path} now instead.",
                Backlog,
                path);
        }

        WriteNow(path, image);
        return false;
    }

    /// <summary>
    /// Waits until every queued frame has been written, at most <paramref name="timeout"/>.
    /// Called on shutdown so the last block's frames are not lost.
    /// </summary>
    public static bool Flush(TimeSpan timeout)
    {
        var clock = Stopwatch.StartNew();

        while (Volatile.Read(ref _pending) > 0)
        {
            if (clock.Elapsed >= timeout)
            {
                Logger.Error("Frame archive: {Backlog} frame(s) still unwritten after {Seconds:F0} s.", Backlog, timeout.TotalSeconds);
                return false;
            }

            Thread.Sleep(50);
        }

        return true;
    }

    private static BlockingCollection<(string Path, Mat Image)> EnsureStarted()
    {
        lock (StartLock)
        {
            if (_queue is not null)
                return _queue;

            _queue = new BlockingCollection<(string Path, Mat Image)>(new ConcurrentQueue<(string Path, Mat Image)>());
            _thread = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "Frame archive writer",
                Priority = ThreadPriority.BelowNormal
            };
            _thread.Start();
            return _queue;
        }
    }

    private static void WriterLoop()
    {
        foreach ((string path, Mat image) in _queue!.GetConsumingEnumerable())
        {
            try
            {
                WriteNow(path, image);
            }
            finally
            {
                image.Dispose();
                Interlocked.Decrement(ref _pending);
            }
        }
    }

    private static void WriteNow(string path, Mat image)
    {
        try
        {
            string? folder = Path.GetDirectoryName(path);

            // The scan folder is removed when its save fails; do not recreate it.
            if (folder is not null && !Directory.Exists(folder))
            {
                Interlocked.Increment(ref _failed);
                Logger.Warn("Frame archive: folder for {Path} no longer exists; frame not written.", path);
                return;
            }

            CvInvoke.Imwrite(path, image);

            if (!File.Exists(path) || new FileInfo(path).Length == 0)
            {
                Interlocked.Increment(ref _failed);
                Logger.Error("Frame archive: {Path} was not written.", path);
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failed);
            Logger.Error(ex, "Frame archive: writing {Path} failed.", path);
        }
    }
}
