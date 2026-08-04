using OptimizationXY.Charger.Models.Acquisition;
using OptimizationXY.Charger.Models.Core;
using OptimizationXY.Charger.Services.Calibration;
using OptimizationXY.Charger.Services.Configuration;
using OptimizationXY.Charger.Services.Core;
using OptimizationXY.Charger.Services.Storage;
using OptimizationXY.Charger.Services.Production;
using OptimizationXY.Charger.Models.Production;
using OptimizationXY.Charger.Models.Plc;
using System.Diagnostics;
using System.Security.Cryptography;
using OptimizationXY.Charger.Models.Calibration;

namespace OptimizationXY.Charger.Services.Acquisition;

/// <summary>
/// Coordinates the transition from a completed capture to the processing pipeline.
/// Handles setup, pipeline execution, result formatting, and cleanup.
/// </summary>
public sealed class LogProcessingCoordinator
{
    public event EventHandler<string>? DiagnosticsMessage;

    /// <summary>
    /// Processes a completed log capture through the full optimization pipeline.
    /// Updates PipelineState and returns formatted diagnostics.
    /// </summary>
    public async Task<string> ProcessCompletedCaptureAsync(
        CompletedLogCapture capture,
        CancellationToken cancellationToken = default)
    {

        ArgumentNullException.ThrowIfNull(capture);

        if (capture.Frames == null || capture.Frames.Count == 0)
        {
            throw new InvalidOperationException(
                "Completed capture has no frames.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // ------------------------------------------------------------
        // VALIDATE CAPTURED SCAN FRAMES
        // ------------------------------------------------------------

        // Captured frames have not been profile-processed yet. Accept a frame
        // only when it contains an in-memory image or a readable image path.
        List<ScanFrame> validFrames = [.. capture.Frames
            .Where(frame =>
                frame is not null &&
                ((frame.GrayImage is not null && !frame.GrayImage.IsEmpty) ||
                 (!string.IsNullOrWhiteSpace(frame.ImagePath) && File.Exists(frame.ImagePath))))];

        int rejectedFrameCount =
            capture.Frames.Count - validFrames.Count;

        LogDiagnostics(
            $"Concatenated-frame validation complete: " +
            $"{validFrames.Count} valid, " +
            $"{rejectedFrameCount} rejected, " +
            $"{capture.Frames.Count} total processing frames.");

        if (validFrames.Count == 0)
        {
            throw new InvalidOperationException(
                "The completed capture contained no readable concatenated frames.");
        }

        int preferredFrames =
            PipelineState.Instance.Settings.RotatingScan.PreferredValidFrames;

        if (validFrames.Count != preferredFrames)
        {
            LogDiagnostics(
                $"WARNING: Expected {preferredFrames} concatenated rotation frames " +
                $"but received {validFrames.Count}.");
        }

        LogDiagnostics("Processing started...");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Prepare PipelineState with the captured frames
            var state = PipelineState.Instance;

            // Clear previous log data
            state.Clear();

            if (state.Settings.Diagnostics.IncludeSettingsSummary)
            {
                state.DiagnosticsReport +=
                    AppSettingsDiagnosticsBuilder.BuildSummary(state.Settings) +
                    Environment.NewLine;
            }

            state.CaptureAudit = CaptureAuditRecord.FromCapture(capture);
            state.HeadCameraConfigurationSnapshot =
                capture.HeadCameraConfigurationSnapshot.ToArray();
            state.TailCameraConfigurationSnapshot =
                capture.TailCameraConfigurationSnapshot.ToArray();

            // Store the captured frames after clearing the previous run.
            state.ScanFrames = [.. validFrames];

            LogDiagnostics("Loading scanner calibration...");
            var lookupTable = LookupTableStore.Load();
            VerifyCaptureMatchesCalibration(
                lookupTable,
                state.HeadCameraConfigurationSnapshot,
                state.TailCameraConfigurationSnapshot,
                state.Settings.Acquisition,
                validFrames);

            // Run the same ordered stage list used by folder processing.
            await Task.Run(() =>
            {
                try
                {
                    LogProcessingPipeline.Instance.RunCurrentFramesPipeline(
                        lookupTable,
                        cancellationToken,
                        LogDiagnostics);
                }
                catch (OperationCanceledException)
                {
                    LogDiagnostics("Processing was cancelled by user.");
                    throw;
                }
                catch (Exception ex)
                {
                    LogDiagnostics($"Pipeline error: {ex.Message}");
                    throw;
                }
            }, cancellationToken);

            stopwatch.Stop();

            LogDiagnostics($"Processing complete in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");

            // Return the final diagnostics report
            return PipelineState.Instance.DiagnosticsReport;
        }
        catch (OperationCanceledException)
        {
            LogDiagnostics("Processing cancelled.");
            PipelineState.Instance.Clear();
            throw;
        }
        catch (Exception ex)
        {
            LogDiagnostics($"Fatal processing error: {ex.Message}");
            PipelineState.Instance.Clear();
            throw;
        }
    }

    private static void VerifyCaptureMatchesCalibration(
        ScannerLookupTable calibration,
        byte[]? headConfiguration,
        byte[]? tailConfiguration,
        CameraAcquisitionSettings acquisitionSettings,
        IReadOnlyList<ScanFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(acquisitionSettings);
        ArgumentNullException.ThrowIfNull(frames);

        string headServerName = acquisitionSettings.HeadCamera.ServerName;
        string tailServerName = acquisitionSettings.TailCamera.ServerName;
        if (headConfiguration is not { Length: > 0 } ||
            tailConfiguration is not { Length: > 0 })
        {
            throw new InvalidOperationException(
                "The capture does not contain both camera-configuration snapshots required to verify scanner calibration compatibility.");
        }

        if (!string.IsNullOrWhiteSpace(calibration.HeadCameraServerName) &&
            !string.Equals(
                calibration.HeadCameraServerName,
                headServerName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The active calibration was created for head camera server '{calibration.HeadCameraServerName}', but the current setting is '{headServerName}'. Recalibrate or restore the correct camera assignment.");
        }

        if (!string.IsNullOrWhiteSpace(calibration.TailCameraServerName) &&
            !string.Equals(
                calibration.TailCameraServerName,
                tailServerName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The active calibration was created for tail camera server '{calibration.TailCameraServerName}', but the current setting is '{tailServerName}'. Recalibrate or restore the correct camera assignment.");
        }

        string currentGeometryHash =
            ScannerCalibrationCompatibility.ComputeAcquisitionGeometrySha256(
                acquisitionSettings);

        if (!string.Equals(
                calibration.AcquisitionGeometrySha256,
                currentGeometryHash,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The current head/tail crop, orientation, overlap, seam, or frame-pair configuration does not match the active scanner calibration. Restore the calibrated settings or recalibrate.");
        }

        var dimensions = frames
            .Where(frame => frame.GrayImage is not null && !frame.GrayImage.IsEmpty)
            .Select(frame => (frame.GrayImage!.Width, frame.GrayImage.Height))
            .Distinct()
            .ToList();

        if (dimensions.Count != 1)
        {
            throw new InvalidOperationException(
                "The captured concatenated frames do not have one consistent image size.");
        }

        if (dimensions[0].Width != calibration.ImageWidth ||
            dimensions[0].Height != calibration.ImageHeight)
        {
            throw new InvalidOperationException(
                $"The active calibration expects {calibration.ImageWidth}x{calibration.ImageHeight} concatenated images, but the capture is {dimensions[0].Width}x{dimensions[0].Height}. Recalibrate or restore the calibrated camera/concatenation settings.");
        }

        string headHash = Convert.ToHexString(
            SHA256.HashData(headConfiguration));
        string tailHash = Convert.ToHexString(
            SHA256.HashData(tailConfiguration));

        if (!string.IsNullOrWhiteSpace(
                calibration.HeadCameraConfigurationSha256) &&
            !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(calibration.HeadCameraConfigurationSha256),
                Convert.FromHexString(headHash)))
        {
            throw new InvalidOperationException(
                "The currently loaded head-camera configuration does not match the camera configuration used to create the active scanner calibration.");
        }

        if (!string.IsNullOrWhiteSpace(
                calibration.TailCameraConfigurationSha256) &&
            !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(calibration.TailCameraConfigurationSha256),
                Convert.FromHexString(tailHash)))
        {
            throw new InvalidOperationException(
                "The currently loaded tail-camera configuration does not match the camera configuration used to create the active scanner calibration.");
        }
    }

    public ProductionReadinessReport PrepareProductionCommand(bool plcConnected)
    {
        var state = PipelineState.Instance;

        try
        {
            state.PlcPayload = PlcPayloadBuilder.Build(state);
        }
        catch (Exception ex)
        {
            state.PlcPayload = null;
            LogDiagnostics($"PLC payload preparation blocked: {ex.Message}");
        }

        state.ProductionReadiness =
            ProductionReadinessEvaluator.Evaluate(state, plcConnected);

        if (state.PlcPayload is null)
        {
            state.ProductionReadiness.Checks.Add(
                new ProductionReadinessCheck
                {
                    Name = "PLC payload",
                    Status = ProductionCheckStatus.Block,
                    Message = "A valid restricted PLC payload could not be created."
                });
        }
        else
        {
            state.ProductionReadiness.Checks.Add(
                new ProductionReadinessCheck
                {
                    Name = "PLC payload",
                    Status = ProductionCheckStatus.Pass,
                    Message =
                        $"Block {state.PlcPayload.BlockCount}; safe carriage retract " +
                        $"{state.PlcPayload.SafeCarriageRetractRadiusMM:F3} mm " +
                        $"including {state.PlcPayload.KnifeClearanceMM:F3} mm knife clearance."
                });
        }

        string report = state.ProductionReadiness.BuildSummary();
        state.DiagnosticsReport += Environment.NewLine + report;
        LogDiagnostics(report.TrimEnd());

        if (!string.IsNullOrWhiteSpace(state.LastSavedRunFolder))
        {
            ProductionAuditWriter.Write(state);
            SimpleScanSaveService.WriteDiagnosticsReport(
                state,
                state.LastSavedRunFolder);
        }

        return state.ProductionReadiness;
    }

    public static void RecordPlcTransmission(PlcTransmissionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var state = PipelineState.Instance;
        state.PlcTransmission = record;
        state.MachineFeedback = new MachineFeedbackSnapshot
        {
            CapturedUtc = DateTime.UtcNow,
            CommandReadbackVerified = record.ReadbackVerified
        };

        state.DiagnosticsReport +=
            Environment.NewLine +
            "=== PLC TRANSMISSION ===" + Environment.NewLine +
            $"Success: {record.Success}" + Environment.NewLine +
            $"Readback Verified: {record.ReadbackVerified}" + Environment.NewLine +
            $"Message: {record.Message}" + Environment.NewLine +
            $"Tags: {string.Join(", ", record.TagsWritten)}" + Environment.NewLine;

        if (!string.IsNullOrWhiteSpace(state.LastSavedRunFolder))
        {
            ProductionAuditWriter.Write(state);
            SimpleScanSaveService.WriteDiagnosticsReport(
                state,
                state.LastSavedRunFolder);
        }
    }

    /// <summary>
    /// Saves the current processing results.
    /// </summary>
    public string SaveResults()
    {
        try
        {
            LogDiagnostics("Saving results...");

            ScanSaveResult result = ScanResultSaveService.Save(PipelineState.Instance);

            LogDiagnostics($"Results saved to: {result.FolderPath}");
            LogDiagnostics(result.DatabaseStatus);

            return result.FolderPath;
        }
        catch (Exception ex)
        {
            LogDiagnostics($"Save error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Logs a diagnostic message.
    /// </summary>
    private void LogDiagnostics(string message)
    {
        DiagnosticsMessage?.Invoke(this, message);
    }
}
