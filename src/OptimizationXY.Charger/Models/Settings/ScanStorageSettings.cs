
/// <summary>
/// Settings for the scan storage system, which manages how long saved scans are kept before being deleted.
/// </summary>
namespace OptimizationXY.Charger.Models.Settings;

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

}
