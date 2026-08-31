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

/// <summary>
/// Provides functionality to save the current scan, including frames, overlays, calibration data, and other relevant information,
/// to a structured folder on disk. It also handles retention of old scans based on user settings.
/// </summary>
public sealed class SimpleScanSaveService
{
    private static readonly Logger Logger =
        LogManager.GetCurrentClassLogger();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _rootFolder;


    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleScanSaveService"/> class,
    /// setting the root folder for saved scans to the application's designated saved scans directory.
    /// </summary>
    public SimpleScanSaveService()
    {
        _rootFolder = ApplicationPaths.SavedScansDirectory;
    }

    /// <summary>
    /// Saves the current scan data, including frames, overlays, calibration snapshots, and other relevant information,
    /// to a structured folder on disk.
    /// </summary>
    /// <param name="state">The current pipeline state containing scan data and settings.</param>
    /// <returns>The path to the folder where the scan data was saved.</returns>
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

    /// <summary>
    /// Resolves the appropriate block folder for the current scan based on the provided current folder and day folder.
    /// </summary>
    /// <param name="currentFolder">The current folder of the scan.</param>
    /// <param name="dayFolder">The folder corresponding to the current day.</param>
    /// <returns>The path to the resolved block folder.</returns>
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

    /// <summary>
    /// Creates the next available block folder within the specified day folder. The block folders are named in the format "Block_0001", "Block_0002", etc.
    /// </summary>
    /// <param name="dayFolder">The folder corresponding to the current day.</param>
    /// <returns>The path to the newly created block folder.</returns>
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

    /// <summary>
    /// Creates the next available scan folder within the specified block folder. The scan folders are named in the format "Scan_0001", "Scan_0002", etc.
    /// </summary>
    /// <param name="blockFolder">The folder corresponding to the current block.</param>
    /// <returns>The path to the newly created scan folder.</returns>
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

    /// <summary>
    /// Writes the diagnostics report from the pipeline state to a text file named "DiagnosticsReport.txt" in the specified scan folder.
    /// </summary>
    /// <param name="state">The pipeline state containing the diagnostics report.</param>
    /// <param name="scanFolder">The folder where the diagnostics report will be saved.</param>
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

    /// <summary>
    /// Saves the captured scan frames from the pipeline state to the specified frames folder. Each frame is saved with a sequentially numbered
    /// filename, preserving the original image format if available, or defaulting to PNG if not.
    /// </summary>
    /// <param name="state">The pipeline state containing the scan frames to be saved.</param>
    /// <param name="framesFolder">The folder where the scan frames will be saved.</param>
    /// <exception cref="IOException">Thrown if a captured frame cannot be saved.</exception>
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

    /// <summary>
    /// Saves a manifest of the scan frames to a CSV file named "frames.csv" in the specified scan folder. The manifest includes
    /// details such as sequence index, capture frame index, angle in degrees,
    /// validity, profile point count, status message, source image path, and saved image path for each frame.
    /// </summary>
    /// <param name="state">The pipeline state containing the scan frames to be saved.</param>
    /// <param name="scanFolder">The folder where the scan frames manifest will be saved.</param>
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

    /// <summary>
    /// Saves the operator scan quality to a JSON file named "operator-scan-quality.json" in the specified scan folder.
    /// </summary>
    /// <param name="state">The pipeline state containing the operator scan quality to be saved.</param>
    /// <param name="scanFolder">The folder where the operator scan quality will be saved.</param>
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

    /// <summary>
    /// Saves the scanner calibration snapshot to a JSON file named "scanner-calibration.json" in the specified scan folder.
    /// </summary>
    /// <param name="state">The pipeline state containing the scanner calibration snapshot to be saved.</param>
    /// <param name="scanFolder">The folder where the scanner calibration snapshot will be saved.</param>
    /// <exception cref="InvalidDataException">Thrown if the scanner calibration snapshot is missing or invalid.</exception>
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


    /// <summary>
    /// Saves the camera configuration snapshots for both head and tail cameras to the specified scan folder. If the snapshots are not available,
    /// </summary>
    /// <param name="state">The pipeline state containing the camera configuration snapshots to be saved.</param>
    /// <param name="scanFolder">The folder where the camera configuration snapshots will be saved.</param>
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

    /// <summary>
    /// Saves a single camera configuration snapshot to the specified destination. If the snapshot is not available and is required,
    /// an exception is thrown.
    /// </summary>
    /// <param name="capturedSnapshot">The captured camera configuration snapshot.</param>
    /// <param name="ccfDirectory">The directory containing the camera configuration files.</param>
    /// <param name="initializationMode">The camera initialization mode.</param>
    /// <param name="configurationFileName">The name of the camera configuration file.</param>
    /// <param name="destination">The destination path where the camera configuration snapshot will be saved.</param>
    /// <param name="required">Indicates whether the camera configuration snapshot is required.</param>
    /// <exception cref="InvalidDataException">Thrown if the camera configuration snapshot is required but missing.</exception>
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

    /// <summary>
    /// Saves the point cloud data from the pipeline state to a CSV file named "point_cloud.csv" in the specified
    /// scan folder. Each point's X, Y, and Z coordinates are saved in millimeters with four decimal places of precision.
    /// </summary>
    /// <param name="state">The pipeline state containing the point cloud data.</param>
    /// <param name="scanFolder">The folder where the point cloud CSV file will be saved.</param>
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

    /// <summary>
    /// Saves the various overlay images from the pipeline state to the specified overlays folder.
    /// Each overlay is saved as a PNG file with a descriptive name.
    /// </summary>
    /// <param name="state">The pipeline state containing the overlay images.</param>
    /// <param name="overlaysFolder">The folder where the overlay PNG files will be saved.</param>
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

    /// <summary>
    /// Saves the provided OpenCV Mat to the specified path if it is valid (not null and not empty). If the Mat is invalid, the method does nothing.
    /// </summary>
    /// <param name="mat">The OpenCV Mat to be saved.</param>
    /// <param name="path">The file path where the Mat will be saved.</param>
    private static void SaveMatIfValid(Mat? mat, string path)
    {
        if (mat is null || mat.IsEmpty)
        {
            return;
        }

        CvInvoke.Imwrite(path, mat);
    }

    /// <summary>
    /// Deletes old saved scans based on the retention settings.
    /// </summary>
    /// <param name="settings">The scan storage settings containing retention configuration.</param>
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

    /// <summary>
    /// Attempts to create a UTC DateTime from the specified year, month, and day. Returns true if successful; otherwise, returns false.
    /// </summary>
    /// <param name="year">The year component of the date.</param>
    /// <param name="month">The month component of the date.</param>
    /// <param name="day">The day component of the date.</param>
    /// <param name="value">When this method returns, contains the UTC DateTime if the conversion succeeded, or the default value if it failed.</param>
    /// <returns>True if the UTC DateTime was successfully created; otherwise, false.</returns>
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
