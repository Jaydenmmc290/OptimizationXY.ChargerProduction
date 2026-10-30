using OptimizationXY.Charger.Services.Core;

namespace OptimizationXY.Charger.Services.Storage;

/// <summary>
/// Outcome of saving one scan: the Scan_#### folder path, the SQL scan id
/// (null when database saving is off or failed) and a text status.
/// </summary>
public sealed record ScanSaveResult(string FolderPath,int? DatabaseScanId,string DatabaseStatus);

/// <summary>
/// Saves the file artifacts first, then optionally records the saved scan in SQL.
/// A database failure never discards an otherwise successful file save.
/// </summary>
public sealed class ScanResultSaveService
{

    /// <summary>
    /// Saves the current scan to disk and optionally to the database.
    /// </summary>
    /// <param name="state">The current pipeline state containing scan data and settings.</param>
    /// <returns>A <see cref="ScanSaveResult"/> representing the outcome of the save operation.</returns>
    public static ScanSaveResult Save(PipelineState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var fileSaveService = new SimpleScanSaveService();
        string folderPath = fileSaveService.SaveCurrentScan(state);

        int? databaseScanId = null;
        string databaseStatus;

        if (!state.Settings.Database.EnableDatabaseSaving)
        {
            databaseStatus = "Database saving is disabled.";
        }
        else
        {
            try
            {
                var sqlSaveService = new SqlScanSaveService(
                    state.Settings.Database);

                databaseScanId = sqlSaveService.SaveScan(state);
                databaseStatus = $"SQL Scan ID: {databaseScanId.Value}";
            }
            catch (Exception ex)
            {
                databaseStatus = "Database save failed: " + ex.Message;
            }
        }

        state.DiagnosticsReport +=
            "\r\nDATABASE SAVE\r\n" +
            $"Folder: {folderPath}\r\n" +
            $"{databaseStatus}\r\n";

        // SaveCurrentScan writes diagnostics before the SQL attempt. Rewrite only
        // that report so the final file also records the database outcome.
        SimpleScanSaveService.WriteDiagnosticsReport(state, folderPath);

        return new ScanSaveResult(
            folderPath,
            databaseScanId,
            databaseStatus);
    }
}
