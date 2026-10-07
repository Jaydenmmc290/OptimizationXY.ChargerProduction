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

        // The previous block may still be saving (the save now runs after the
        // PLC write). Processing clears and reuses the shared pipeline state, so
        // it must not begin until that save has completely finished.
        await WaitForPendingSaveAsync();

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



        if (headConfiguration is not { Length: > 0 } ||
            tailConfiguration is not { Length: > 0 })
        {
            throw new InvalidOperationException(
                "The capture does not contain both camera-configuration snapshots " +
                "required to verify scanner calibration compatibility.");
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
                "The current head/tail crop, orientation, overlap, seam, or " +
                "frame-pair configuration does not match the active scanner calibration. " +
                "Restore the calibrated settings or recalibrate.");
        }

        var dimensions = frames
            .Where(frame =>
                frame.GrayImage is not null &&
                !frame.GrayImage.IsEmpty)
            .Select(frame =>
                (frame.GrayImage!.Width, frame.GrayImage.Height))
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
                $"The active calibration expects " +
                $"{calibration.ImageWidth}x{calibration.ImageHeight} concatenated images, " +
                $"but the capture is " +
                $"{dimensions[0].Width}x{dimensions[0].Height}. " +
                $"Recalibrate or restore the calibrated camera/concatenation settings.");
        }

        string headHash =
            Convert.ToHexString(
                SHA256.HashData(headConfiguration));

        string tailHash =
            Convert.ToHexString(
                SHA256.HashData(tailConfiguration));

        if (!string.IsNullOrWhiteSpace(
                calibration.HeadCameraConfigurationSha256) &&
            !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(
                    calibration.HeadCameraConfigurationSha256),
                Convert.FromHexString(headHash)))
        {
            throw new InvalidOperationException(
                "The currently loaded head-camera configuration does not match " +
                "the camera configuration used to create the active scanner calibration.");
        }

        if (!string.IsNullOrWhiteSpace(
                calibration.TailCameraConfigurationSha256) &&
            !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(
                    calibration.TailCameraConfigurationSha256),
                Convert.FromHexString(tailHash)))
        {
            throw new InvalidOperationException(
                "The currently loaded tail-camera configuration does not match " +
                "the camera configuration used to create the active scanner calibration.");
        }
    }

    /// <summary>
    /// Prepares the production command by building the PLC payload and evaluating production readiness.
    /// 
    /// Process:
    /// 1. If FinalSpindleRecommendation exists, present operator with kinematic selection choice
    /// 2. Operator selects either "Use Kinematic Rotation" or "Continue With Current XY"
    /// 3. Selected recommendation (original or kinematic-adjusted) is used for PLC payload
    /// 4. Build PLC payload and evaluate production readiness
    /// </summary>
    /// <param name="plcConnected">Indicates whether the PLC is connected.</param>
    /// <returns>A report detailing the production readiness status.</returns>
    public ProductionReadinessReport PrepareProductionCommand(bool plcConnected)
    {
        var state = PipelineState.Instance;

        // ============================================================
        // KINEMATIC POSITION SELECTION STEP
        // Present operator with choice between kinematic rotation and current positions
        // ============================================================
        var settings = PipelineState.Instance.Settings;
        if (state.FinalSpindleRecommendation != null && settings.Kinematic.EnableKinematicPositionSelection)
        {
            try
            {
                LogDiagnostics("[KINEMATIC] Presenting operator with position selection choice...");

                state.FinalSpindleRecommendation = Kinematic.KinematicPositionSelector.GetOperatorPositionSelection(
                    state.FinalSpindleRecommendation);

                LogDiagnostics("[KINEMATIC] Operator selection complete.");
            }
            catch (Exception ex)
            {
                LogDiagnostics($"[KINEMATIC] Error during operator selection: {ex.Message}");
                // Continue with existing recommendation if selection fails
            }
        }
        else if (state.FinalSpindleRecommendation != null && !settings.Kinematic.EnableKinematicPositionSelection)
        {
            LogDiagnostics("[KINEMATIC] Kinematic position selection is disabled; proceeding with current XY positions.");
        }

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
                        $"{state.PlcPayload.SafeCarriageRetractRadiusMM:F3} in " +
                        $"including {state.PlcPayload.KnifeClearanceMM:F3} in knife clearance " +
                        "(payload values are inches)."
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

    /// <summary>
    /// Records a PLC transmission event.
    /// </summary>
    /// <param name="record">The PLC transmission record to log.</param>
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

    // The save that is running now, or the last one that ran. Only one save can
    // be in flight at a time: a new scan waits for it before it is processed.
    // Shared by every coordinator instance (a camera reconnect creates a new
    // coordinator) because they all work on the one shared pipeline state.
    private static volatile Task<string> _pendingSave = Task.FromResult(string.Empty);
    private static readonly object SaveStartLock = new();

    /// <summary>
    /// True while a save started by this coordinator has not finished yet.
    /// </summary>
    public static bool IsSaveInProgress => !_pendingSave.IsCompleted;

    /// <summary>
    /// Waits until the save of the previous block has finished. A failed save
    /// has already been reported by whoever started it, so its error is not
    /// raised again here.
    /// </summary>
    public async Task WaitForPendingSaveAsync()
    {
        Task pending = _pendingSave;

        if (pending.IsCompleted)
            return;

        LogDiagnostics("Waiting for the previous block's save to finish before processing this scan...");

        try
        {
            await pending;
        }
        catch (Exception)
        {
            // Already reported when the save failed.
        }

        LogDiagnostics("Previous block's save finished.");
    }

    /// <summary>
    /// Blocks for up to <paramref name="timeout"/> while a save is finishing.
    /// Used when the application closes so a scan record is not cut short.
    /// The save runs entirely on a background thread and never needs the UI
    /// thread, so waiting for it here cannot deadlock.
    /// </summary>
    /// <returns>True when no save is left running.</returns>
    public bool WaitForPendingSave(TimeSpan timeout)
    {
        Task pending = _pendingSave;

        if (pending.IsCompleted)
            return true;

        try
        {
            return pending.Wait(timeout);
        }
        catch (Exception)
        {
            // The save failed; it is no longer running.
            return true;
        }
    }

    /// <summary>
    /// Asynchronously saves the current processing results on a background
    /// thread so file I/O does not block the UI thread.
    ///
    /// Used when the result is saved BEFORE the PLC write: the caller must await
    /// this task before evaluating production readiness or sending a PLC payload.
    /// </summary>
    /// <returns>A task that completes when the save operation finishes, returning the save folder path.</returns>
    public Task<string> SaveResultsAsync()
    {
        lock (SaveStartLock)
        {
            PipelineState.Instance.ResultSaveStarted = true;

            Task<string> save = Task.Run(() => SaveResults());
            _pendingSave = save;
            return save;
        }
    }

    /// <summary>
    /// Saves the current processing results AFTER the PLC write, on a background
    /// thread, and then completes the production audit record so it includes the
    /// readiness report and the PLC transmission result.
    ///
    /// The next scan is not processed until this has finished (see
    /// <see cref="WaitForPendingSaveAsync"/>), so the data being saved cannot be
    /// cleared or replaced while it is written.
    /// </summary>
    /// <returns>A task that completes when the save has finished, returning the save folder path.</returns>
    public Task<string> SaveResultsAfterPlcWriteAsync()
    {
        lock (SaveStartLock)
        {
            // A scan is saved once. If its save has already been started (for
            // example by the application closing during the PLC write), hand back
            // that same save instead of starting a second one.
            if (PipelineState.Instance.ResultSaveStarted)
                return _pendingSave;

            PipelineState.Instance.ResultSaveStarted = true;

            Task<string> save = Task.Run(() => SaveAndCompleteAuditRecord());
            _pendingSave = save;
            return save;
        }
    }

    /// <summary>
    /// Background work of <see cref="SaveResultsAfterPlcWriteAsync"/>: saves the
    /// scan, then completes the production audit record.
    /// </summary>
    private string SaveAndCompleteAuditRecord()
    {
        string folderPath = SaveResults();

        PipelineState state = PipelineState.Instance;

        try
        {
            List<string> problems =
                ProductionReadinessEvaluator.RefreshSavedRecordChecks(state);

            foreach (string problem in problems)
                LogDiagnostics($"ERROR: Saved record is incomplete. {problem}");

            state.DiagnosticsReport +=
                Environment.NewLine +
                "=== SAVE AFTER PLC WRITE ===" + Environment.NewLine +
                $"Folder: {folderPath}" + Environment.NewLine +
                (problems.Count == 0
                    ? "Saved record: complete" + Environment.NewLine
                    : "Saved record: INCOMPLETE" + Environment.NewLine +
                      string.Join(Environment.NewLine, problems.Select(problem => "  " + problem)) +
                      Environment.NewLine);

            // Same audit files the save-first order writes, now including the
            // readiness report and the PLC transmission result in one pass.
            ProductionAuditWriter.Write(state);
            SimpleScanSaveService.WriteDiagnosticsReport(
                state,
                folderPath);
        }
        catch (Exception ex)
        {
            LogDiagnostics($"ERROR: The production audit record could not be completed: {ex.Message}");
            throw;
        }

        return folderPath;
    }

    /// <summary>
    /// Logs a diagnostic message.
    /// </summary>
    private void LogDiagnostics(string message)
    {
        DiagnosticsMessage?.Invoke(this, message);
    }
}
