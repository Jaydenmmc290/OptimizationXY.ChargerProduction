using OptimizationXY.Charger.Services.Core;

namespace OptimizationXY.Charger.Services.Storage;

public sealed record ScanSaveResult(
    string FolderPath,
    int? DatabaseScanId,
    string DatabaseStatus);

/// <summary>
/// Saves the file artifacts first, then optionally records the saved scan in SQL.
/// A database failure never discards an otherwise successful file save.
/// </summary>
public sealed class ScanResultSaveService
{
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
