
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

}
