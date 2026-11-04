
// ScanStorageSettings.cs - how long SavedScans folders are kept.
namespace OptimizationXY.Charger.Models.Settings;

/// <summary>
/// How long saved scan folders under SavedScans are kept before the program
/// deletes them (retention runs in the save service).
/// </summary>
public sealed class ScanStorageSettings
{

    /// <summary>
    /// Number of days to keep saved scans before deleting them.
    /// </summary>
    public int DaysToKeepSavedScans { get; set; } = 90;

    /// <summary>
    /// Whether to automatically delete old scans after the specified number of days.
    /// </summary>
    public bool AutoDeleteOldScans { get; set; } = true;

    /// <summary>
    /// Write the scan's frame PNGs on a background thread (FrameArchiveWriter)
    /// so the next block is not held up by the archive. No frame is dropped: a
    /// full queue writes the frame straight away. False writes them inside the
    /// save, as before.
    /// </summary>
    public bool BackgroundFrameArchive { get; set; } = true;

    /// <summary>Frames that may wait for the archive thread (about 3 blocks at ~100 frames each).</summary>
    public int FrameArchiveQueueCapacity { get; set; } = 300;
}
