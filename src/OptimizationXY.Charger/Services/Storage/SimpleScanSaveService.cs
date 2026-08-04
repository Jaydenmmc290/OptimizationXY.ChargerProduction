using System.Globalization;
using System.Text;
using System.Text.Json;
using Emgu.CV;
using NLog;
using OptimizationXY.Charger.Infrastructure;
using OptimizationXY.Charger.Models.Acquisition;
using OptimizationXY.Charger.Models.Calibration;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Core;

namespace OptimizationXY.Charger.Services.Storage;

public sealed class SimpleScanSaveService
{
    private static readonly Logger Logger =
        LogManager.GetCurrentClassLogger();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _rootFolder;

    public SimpleScanSaveService()
    {
        _rootFolder = ApplicationPaths.SavedScansDirectory;
    }

    public string SaveCurrentScan(PipelineState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ApplicationPaths.EnsureCreated();

        DeleteOldSavedScans(state.Settings.Storage);

        DateTime now = DateTime.Now;
        string dayFolder = Path.Combine(
            _rootFolder,
            now.ToString("yyyy", CultureInfo.InvariantCulture),
            now.ToString("MM", CultureInfo.InvariantCulture),
            now.ToString("dd", CultureInfo.InvariantCulture));

        Directory.CreateDirectory(dayFolder);

        string blockFolder = ResolveBlockFolder(
            state.CurrentLogFolder,
            dayFolder);

        string scanFolder = CreateNextScanFolder(blockFolder);
        string framesFolder = Path.Combine(scanFolder, "Frames");
        string overlaysFolder = Path.Combine(scanFolder, "Overlays");
        Directory.CreateDirectory(framesFolder);
        Directory.CreateDirectory(overlaysFolder);

        try
        {
            SaveFrames(state, framesFolder);
            WriteDiagnosticsReport(state, scanFolder);
            SaveFrameManifest(state, scanFolder);
            SaveOperatorScanQuality(state, scanFolder);
            SaveCalibrationSnapshot(state, scanFolder);
            SaveCameraConfigurationSnapshots(state, scanFolder);
            SavePointCloud(state, scanFolder);
            SaveOverlays(state, overlaysFolder);

            state.CurrentLogFolder = scanFolder;
            state.LastSavedRunFolder = scanFolder;
            return scanFolder;
        }
        catch
        {
            foreach (var frame in state.ScanFrames)
            {
                frame.SavedImagePath = string.Empty;
            }

            try
            {
                if (Directory.Exists(scanFolder))
                {
                    Directory.Delete(scanFolder, recursive: true);
                }
            }
            catch (Exception cleanupException) when (
                cleanupException is IOException or UnauthorizedAccessException)
            {
                Logger.Warn(
                    cleanupException,
                    "Could not remove incomplete scan folder {Folder}.",
                    scanFolder);
            }

            throw;
        }
    }

    private static string ResolveBlockFolder(
        string currentFolder,
        string dayFolder)
    {
        if (!string.IsNullOrWhiteSpace(currentFolder) &&
            Directory.Exists(currentFolder))
        {
            DirectoryInfo current = new(currentFolder);

            if (current.Name.StartsWith("Block_", StringComparison.OrdinalIgnoreCase))
            {
                return current.FullName;
            }

            if (current.Name.StartsWith("Scan_", StringComparison.OrdinalIgnoreCase) &&
                current.Parent is not null &&
                current.Parent.Name.StartsWith("Block_", StringComparison.OrdinalIgnoreCase))
            {
                return current.Parent.FullName;
            }
        }

        return CreateNextBlockFolder(dayFolder);
    }

    private static string CreateNextBlockFolder(string dayFolder)
    {
        for (int blockNumber = 1; ; blockNumber++)
        {
            string blockFolder = Path.Combine(
                dayFolder,
                $"Block_{blockNumber:0000}");

            if (!Directory.Exists(blockFolder))
            {
                Directory.CreateDirectory(blockFolder);
                return blockFolder;
            }
        }
    }

    private static string CreateNextScanFolder(string blockFolder)
    {
        for (int scanNumber = 1; ; scanNumber++)
        {
            string scanFolder = Path.Combine(
                blockFolder,
                $"Scan_{scanNumber:0000}");

            if (!Directory.Exists(scanFolder))
            {
                Directory.CreateDirectory(scanFolder);
                return scanFolder;
            }
        }
    }

    internal static void WriteDiagnosticsReport(
        PipelineState state,
        string scanFolder)
    {
        string path = Path.Combine(scanFolder, "DiagnosticsReport.txt");
        File.WriteAllText(
            path,
            state.DiagnosticsReport ?? string.Empty,
            new UTF8Encoding(false));
    }

    private static void SaveFrames(
        PipelineState state,
        string framesFolder)
    {
        for (int index = 0; index < state.ScanFrames.Count; index++)
        {
            var frame = state.ScanFrames[index];
            int sequenceIndex = index + 1;

            string extension = string.IsNullOrWhiteSpace(frame.ImagePath)
                ? ".png"
                : Path.GetExtension(frame.ImagePath).ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            string destinationPath = Path.Combine(
                framesFolder,
                $"Frame_{sequenceIndex:0000}{extension}");

            if (!string.IsNullOrWhiteSpace(frame.ImagePath) &&
                File.Exists(frame.ImagePath))
            {
                File.Copy(
                    frame.ImagePath,
                    destinationPath,
                    overwrite: false);
            }
            else if (frame.GrayImage is not null && !frame.GrayImage.IsEmpty)
            {
                // Live frames have no source path. Store a lossless PNG copy so
                // every production result remains reproducible and auditable.
                destinationPath = Path.ChangeExtension(
                    destinationPath,
                    ".png");

                CvInvoke.Imwrite(destinationPath, frame.GrayImage);

                if (!File.Exists(destinationPath) ||
                    new FileInfo(destinationPath).Length == 0)
                {
                    throw new IOException(
                        $"Failed to save captured frame {sequenceIndex}.");
                }
            }
            else
            {
                frame.SavedImagePath = string.Empty;
                continue;
            }

            frame.SavedImagePath = destinationPath;
        }
    }

    private static void SaveFrameManifest(
        PipelineState state,
        string scanFolder)
    {
        string path = Path.Combine(scanFolder, "frames.csv");
        using StreamWriter writer = new(
            path,
            append: false,
            new UTF8Encoding(false));

        writer.WriteLine(
            "SequenceIndex,CaptureFrameIndex,AngleDegrees,IsValid,ProfilePointCount,StatusMessage,SourceImagePath,SavedImagePath");

        for (int index = 0; index < state.ScanFrames.Count; index++)
        {
            var frame = state.ScanFrames[index];
            string escapedStatus = EscapeCsv(frame.StatusMessage);
            string escapedSourcePath = EscapeCsv(frame.ImagePath);
            string escapedSavedPath = EscapeCsv(frame.SavedImagePath);

            writer.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{index + 1},{frame.FrameIndex},{frame.AngleDeg:F6},{frame.IsValid},{frame.Profile?.Count ?? 0},\"{escapedStatus}\",\"{escapedSourcePath}\",\"{escapedSavedPath}\""));
        }
    }

    private static string EscapeCsv(string? value) =>
        (value ?? string.Empty).Replace("\"", "\"\"");

    private static void SaveOperatorScanQuality(
        PipelineState state,
        string scanFolder)
    {
        if (state.OperatorScanQuality is null)
        {
            if (state.Settings.Production.OperatingMode == ChargerOperatingMode.Production)
            {
                throw new InvalidDataException(
                    "The operator scan-quality result is missing. Production scans must preserve this gate for audit and rescan decisions.");
            }

            return;
        }

        string path = Path.Combine(
            scanFolder,
            "operator-scan-quality.json");
        string json = JsonSerializer.Serialize(
            state.OperatorScanQuality,
            JsonOptions);
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    private static void SaveCalibrationSnapshot(
        PipelineState state,
        string scanFolder)
    {
        ScannerLookupTable? calibration =
            state.ScannerCalibrationSnapshot;

        if (calibration is null || !calibration.IsValid)
        {
            throw new InvalidDataException(
                "The exact scanner calibration used by this pipeline run is missing or invalid.");
        }

        string path = Path.Combine(
            scanFolder,
            "scanner-calibration.json");
        string json = JsonSerializer.Serialize(
            calibration,
            JsonOptions);
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    private static void SaveCameraConfigurationSnapshots(
        PipelineState state,
        string scanFolder)
    {
        string ccfDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "CameraConfiguration");

        bool required =
            state.Settings.Production.OperatingMode == ChargerOperatingMode.Production;

        SaveCameraConfigurationSnapshot(
            state.HeadCameraConfigurationSnapshot,
            ccfDirectory,
            state.Settings.Acquisition.HeadCamera.InitializationMode,
            state.Settings.Acquisition.HeadCamera.ConfigurationFileName,
            Path.Combine(scanFolder, "head-camera-configuration.snapshot"),
            required);

        SaveCameraConfigurationSnapshot(
            state.TailCameraConfigurationSnapshot,
            ccfDirectory,
            state.Settings.Acquisition.TailCamera.InitializationMode,
            state.Settings.Acquisition.TailCamera.ConfigurationFileName,
            Path.Combine(scanFolder, "tail-camera-configuration.snapshot"),
            required);
    }

    private static void SaveCameraConfigurationSnapshot(
        byte[]? capturedSnapshot,
        string ccfDirectory,
        CameraInitializationMode initializationMode,
        string configurationFileName,
        string destination,
        bool required)
    {
        if (capturedSnapshot is { Length: > 0 })
        {
            using FileStream stream = new(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            stream.Write(capturedSnapshot);
            stream.Flush(flushToDisk: true);
            return;
        }

        if (required)
        {
            throw new InvalidDataException(
                "The exact camera configuration captured at scan start is missing. Production records do not fall back to a mutable external configuration file.");
        }

        if (initializationMode == CameraInitializationMode.Programmatic)
        {
            Logger.Warn(
                "No capture-time direct camera feature snapshot was available. Commissioning scan will continue without a camera snapshot.");
            return;
        }

        if (string.IsNullOrWhiteSpace(configurationFileName))
        {
            return;
        }

        string source = Path.Combine(
            ccfDirectory,
            Path.GetFileName(configurationFileName));

        if (!File.Exists(source))
        {
            Logger.Warn(
                "Commissioning-only camera configuration fallback was not saved because the source file was not found: {Path}",
                source);
            return;
        }

        Logger.Warn(
            "No capture-time camera-configuration snapshot was available. Commissioning legacy mode is copying the configured file instead: {Path}",
            source);
        File.Copy(source, destination, overwrite: false);
    }

    private static void SavePointCloud(
        PipelineState state,
        string scanFolder)
    {
        string path = Path.Combine(scanFolder, "point_cloud.csv");
        using StreamWriter writer = new(
            path,
            append: false,
            new UTF8Encoding(false));

        writer.WriteLine("X_MM,Y_MM,Z_MM");

        foreach (var point in state.PointCloud)
        {
            writer.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{point.X:F4},{point.Y:F4},{point.Z:F4}"));
        }
    }

    private static void SaveOverlays(
        PipelineState state,
        string overlaysFolder)
    {
        SaveMatIfValid(
            state.PointCloudOverlay,
            Path.Combine(overlaysFolder, "point_cloud_overlay.png"));
        SaveMatIfValid(
            state.CenterlineOffsetOverlay,
            Path.Combine(overlaysFolder, "centerline_offset_overlay.png"));
        SaveMatIfValid(
            state.SpindleAxisOverlay,
            Path.Combine(overlaysFolder, "spindle_axis_overlay.png"));
        SaveMatIfValid(
            state.RadiusDefectHeatmap,
            Path.Combine(overlaysFolder, "radius_defect_heatmap.png"));
        SaveMatIfValid(
            state.RotatingScanTopDownOverlay,
            Path.Combine(overlaysFolder, "rotating_scan_top_down_overlay.png"));
        SaveMatIfValid(
            state.UnwrappedPeelView,
            Path.Combine(overlaysFolder, "unwrapped_peel_view.png"));
        SaveMatIfValid(
            state.CylinderPeelView,
            Path.Combine(overlaysFolder, "largest_inscribed_cylinder_peel.png"));
        SaveMatIfValid(
            state.OvalPeelView,
            Path.Combine(overlaysFolder, "largest_inscribed_oval_peel.png"));
        SaveMatIfValid(
            state.BaselinePeelView,
            Path.Combine(overlaysFolder, "baseline_peel.png"));
        SaveMatIfValid(
            state.PenaltyPeelView,
            Path.Combine(overlaysFolder, "penalty_search_peel.png"));
        SaveMatIfValid(
            state.DirectRecoveryPeelView,
            Path.Combine(overlaysFolder, "direct_recovery_refinement_peel.png"));
    }

    private static void SaveMatIfValid(Mat? mat, string path)
    {
        if (mat is null || mat.IsEmpty)
        {
            return;
        }

        CvInvoke.Imwrite(path, mat);
    }

    private void DeleteOldSavedScans(ScanStorageSettings settings)
    {
        if (!settings.AutoDeleteOldScans || !Directory.Exists(_rootFolder))
        {
            return;
        }

        DateTime cutoffDate = DateTime.UtcNow.Date.AddDays(
            -settings.DaysToKeepSavedScans);

        try
        {
            foreach (string yearFolder in Directory.EnumerateDirectories(_rootFolder))
            {
                if (!int.TryParse(
                        Path.GetFileName(yearFolder),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out int year))
                {
                    continue;
                }

                foreach (string monthFolder in Directory.EnumerateDirectories(yearFolder))
                {
                    if (!int.TryParse(
                            Path.GetFileName(monthFolder),
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out int month))
                    {
                        continue;
                    }

                    foreach (string dayFolder in Directory.EnumerateDirectories(monthFolder))
                    {
                        if (!int.TryParse(
                                Path.GetFileName(dayFolder),
                                NumberStyles.None,
                                CultureInfo.InvariantCulture,
                                out int day) ||
                            !TryCreateUtcDate(year, month, day, out DateTime folderDate) ||
                            folderDate >= cutoffDate)
                        {
                            continue;
                        }

                        try
                        {
                            Directory.Delete(dayFolder, recursive: true);
                            Logger.Info(
                                "Deleted expired saved-scan folder {Folder}.",
                                dayFolder);
                        }
                        catch (Exception ex) when (
                            ex is IOException or UnauthorizedAccessException)
                        {
                            Logger.Warn(
                                ex,
                                "Could not delete expired saved-scan folder {Folder}.",
                                dayFolder);
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            // Retention cleanup must never prevent the current scan from saving.
            Logger.Warn(ex, "Saved-scan retention cleanup did not complete.");
        }
    }

    private static bool TryCreateUtcDate(
        int year,
        int month,
        int day,
        out DateTime value)
    {
        try
        {
            value = new DateTime(
                year,
                month,
                day,
                0,
                0,
                0,
                DateTimeKind.Utc);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            value = default;
            return false;
        }
    }
}
