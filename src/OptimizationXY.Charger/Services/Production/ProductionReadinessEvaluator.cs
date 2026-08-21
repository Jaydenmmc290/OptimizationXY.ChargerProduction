using System.Text.Json;
using OptimizationXY.Charger.Models.Acquisition;
using OptimizationXY.Charger.Models.Production;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Models.Operator;
using OptimizationXY.Charger.Services.Core;

namespace OptimizationXY.Charger.Services.Production;

/// <summary>
/// Evaluates the production readiness of the system based on the current pipeline state and configuration settings.
/// </summary>
public static class ProductionReadinessEvaluator
{
    /// <summary>
    /// Evaluates the production readiness of the system based on the provided pipeline state and PLC connection status.
    /// </summary>
    /// <param name="state">The current pipeline state.</param>
    /// <param name="plcConnected">Indicates whether the PLC is connected.</param>
    /// <returns>A report detailing the production readiness of the system.</returns>
    public static ProductionReadinessReport Evaluate(
        PipelineState state,
        bool plcConnected)
    {
        ArgumentNullException.ThrowIfNull(state);

        AppSettings settings = state.Settings;
        var report = new ProductionReadinessReport
        {
            PlcConnected = plcConnected
        };

        Add(
            report,
            "Operating mode",
            settings.Production.OperatingMode == ChargerOperatingMode.Production
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            settings.Production.OperatingMode == ChargerOperatingMode.Production
                ? "Production mode selected."
                : $"Current mode is {settings.Production.OperatingMode}; PLC machine commands are blocked.");

        Add(
            report,
            "Commissioning approval",
            settings.Production.CommissioningComplete
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            settings.Production.CommissioningComplete
                ? $"Approved by {settings.Production.CommissioningApprovedBy} at {settings.Production.CommissioningApprovedUtc:O}."
                : "One or more required commissioning checks are incomplete.");

        bool setupComplete =
            settings.Setup.MinimumSetupComplete &&
            (!settings.Database.EnableDatabaseSaving ||
             settings.Setup.DatabaseConfigured);

        Add(
            report,
            "Production setup workflow",
            setupComplete
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            setupComplete
                ? $"SQL, camera assignment, laser detection, length scale, and active calibration setup are recorded. Active calibration: {settings.Setup.ActiveCalibrationId}."
                : "Complete the Production Setup / Calibration wizard before enabling machine commands.");

        bool productionOptimizationPolicy =
            settings.OptimizationMode ==
                OptimizationXY.Charger.Models.Optimization.SpindleOptimizationMode.RecoveryOptimized &&
            settings.RecoveryOptimization.IncludeBaselineCandidate &&
            settings.RecoveryOptimization.IncludePenaltyCandidate &&
            settings.RecoveryOptimization.IncludeCylinderCandidate &&
            settings.RecoveryOptimization.IncludeOvalCandidate &&
            settings.RecoveryOptimization.EnableDirectRecoveryRefinement;

        Add(
            report,
            "Production optimization policy",
            productionOptimizationPolicy
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            productionOptimizationPolicy
                ? "RecoveryOptimized mode includes baseline, penalty, cylinder, oval, and direct virtual-peel refinement candidates."
                : "Production requires RecoveryOptimized mode with all four seed strategies and direct virtual-peel refinement enabled.");

        bool directCameraInitialization =
            settings.Acquisition.HeadCamera.InitializationMode == CameraInitializationMode.Programmatic &&
            settings.Acquisition.TailCamera.InitializationMode == CameraInitializationMode.Programmatic &&
            settings.Acquisition.HeadCamera.RequireConfiguredFeatures &&
            settings.Acquisition.TailCamera.RequireConfiguredFeatures;

        Add(
            report,
            "Direct camera initialization",
            directCameraInitialization
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            directCameraInitialization
                ? "Both cameras are opened directly by Sapera server/device index and reject missing production-critical features."
                : "Production requires Programmatic camera initialization with RequireConfiguredFeatures enabled for both cameras.");

        string headCameraLocation =
            $"{settings.Acquisition.HeadCamera.ServerName}[{settings.Acquisition.HeadCamera.DeviceIndex}]".Trim();

        string tailCameraLocation =
            $"{settings.Acquisition.TailCamera.ServerName}[{settings.Acquisition.TailCamera.DeviceIndex}]".Trim();

        bool configuredCameraIdentity =
            !string.IsNullOrWhiteSpace(settings.Acquisition.HeadCamera.ServerName) &&
            !string.IsNullOrWhiteSpace(settings.Acquisition.TailCamera.ServerName) &&
            !string.Equals(headCameraLocation, tailCameraLocation, StringComparison.OrdinalIgnoreCase);
        bool capturedCameraIdentity =
            SnapshotLocationMatches(state.HeadCameraConfigurationSnapshot, headCameraLocation) &&
            SnapshotLocationMatches(state.TailCameraConfigurationSnapshot, tailCameraLocation);
        bool cameraIdentityBound = configuredCameraIdentity && capturedCameraIdentity;

        Add(
            report,
            "Physical camera identity binding",
            cameraIdentityBound
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            !cameraIdentityBound
    ? "One or both cameras are not properly assigned or the capture-time camera identities do not match those assignments."
    : "Production requires two distinct physical cameras to be assigned as " +
      "Head and Tail, and the capture-time camera identities must match those assignments.");

        bool encoderMode =
            settings.Acquisition.HeadCamera.Mode ==
                OptimizationXY.Charger.Models.Acquisition.AcquisitionMode.Encoder &&
            settings.Acquisition.TailCamera.Mode ==
                OptimizationXY.Charger.Models.Acquisition.AcquisitionMode.Encoder;

        Add(
            report,
            "Synchronized encoder acquisition",
            !settings.Production.RequireEncoderAcquisition || encoderMode
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            encoderMode
                ? "Both cameras are configured for PLC/encoder-triggered acquisition."
                : "One or both cameras are in free-running FrameRate mode; production pairing is not angle-synchronized.");

        bool productionPlcPolicy =
            settings.Plc.Enabled &&
            settings.Plc.AutoStartCaptureOnScanStart &&
            settings.Plc.AutoSendResultsAfterSave &&
            settings.Plc.RequireReadbackVerification &&
            settings.Plc.RequireGoodQuality &&
            settings.Plc.RequireScanCompleteBeforeProcessing;

        Add(
            report,
            "Production PLC policy",
            productionPlcPolicy
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            productionPlcPolicy
                ? "PLC auto-start, ScanComplete gating, verified readback, good-quality enforcement, and automatic result delivery are enabled."
                : "One or more mandatory production PLC controls are disabled.");

        Add(
            report,
            "Movement warning policy",
            settings.Safety.BlockOnMovementWarning
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            settings.Safety.BlockOnMovementWarning
                ? "Any configured movement warning blocks the production PLC command."
                : "Production requires movement warnings to block PLC command delivery.");

        bool exactRotationContract =
            settings.Acquisition.TargetFramePairsPerLog == 36 &&
            Math.Abs(settings.RotatingScan.ExpectedAngleStepDeg - 10.0) <= 0.01;

        Add(
            report,
            "36-position rotation contract",
            exactRotationContract
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            exactRotationContract
                ? "Configured for 36 synchronized pairs at 10.00 degrees per position."
                : $"Production requires 36 pairs at 10.00 degrees; configured {settings.Acquisition.TargetFramePairsPerLog} pairs at {settings.RotatingScan.ExpectedAngleStepDeg:F2} degrees.");

        int expectedFrames = settings.Acquisition.TargetFramePairsPerLog;
        int validFrames = state.ScanFrames.Count(frame => frame.IsValid);
        bool frameCountGood = settings.Production.RequireExactFrameCount
            ? state.ScanFrames.Count == expectedFrames && validFrames == expectedFrames
            : validFrames >= settings.RotatingScan.MinimumValidFrames;

        Add(
            report,
            "Concatenated frame count",
            frameCountGood ? ProductionCheckStatus.Pass : ProductionCheckStatus.Block,
            $"Expected {expectedFrames}; received {state.ScanFrames.Count}; valid {validFrames}.");

        int droppedFrames = state.CaptureAudit?.DroppedFrameCount ?? 0;
        Add(
            report,
            "Dropped source frames",
            droppedFrames <= settings.Production.MaximumDroppedSourceFrames
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            $"Dropped {droppedFrames}; maximum allowed {settings.Production.MaximumDroppedSourceFrames}.");

        bool scanCompleteRequired =
            settings.Plc.RequireScanCompleteBeforeProcessing;
        bool scanCompleteVerified =
            state.CaptureAudit?.PlcScanCompleteVerified == true;

        Add(
            report,
            "PLC rotation-cycle completion",
            !scanCompleteRequired || scanCompleteVerified
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            !scanCompleteRequired
                ? "PLC ScanComplete gating is disabled."
                : scanCompleteVerified
                    ? "A good PLC ScanComplete was received after the active ScanStart cycle."
                    : "The active PLC-controlled rotation cycle was not verified complete before processing.");

        Add(
            report,
            "PLC write order",
            settings.Plc.WriteCarriageRetractFirst
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            settings.Plc.WriteCarriageRetractFirst
                ? "CarriageRetract and RoundupComplete are written before the complete ScannedBlockData packet."
                : "Unsafe production configuration: ScannedBlockData would be written before its supporting radius packets.");

        bool calibrationValid =
            state.Calibration is not null &&
            double.IsFinite(state.Calibration.LengthPixelsPerMM) &&
            state.Calibration.LengthPixelsPerMM > 0 &&
            state.ScannerCalibrationSnapshot?.IsValid == true &&
            !string.IsNullOrWhiteSpace(settings.Setup.ActiveCalibrationId) &&
            string.Equals(
                state.ScannerCalibrationSnapshot.CalibrationId,
                settings.Setup.ActiveCalibrationId,
                StringComparison.Ordinal);

        Add(
            report,
            "Scanner calibration",
            calibrationValid
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            calibrationValid
                ? $"Active calibration {state.ScannerCalibrationSnapshot!.CalibrationId} loaded with {state.ScannerCalibrationSnapshot.Points.Count} points and preserved as a per-run snapshot."
                : "Calibration is missing, invalid, does not match Setup.ActiveCalibrationId, or was not preserved with the pipeline state.");

        bool cameraConfigurationsCaptured =
            state.HeadCameraConfigurationSnapshot is { Length: > 0 } &&
            state.TailCameraConfigurationSnapshot is { Length: > 0 };

        Add(
            report,
            "Capture-time camera configurations",
            cameraConfigurationsCaptured
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            cameraConfigurationsCaptured
                ? "The exact head and tail camera-configuration snapshots applied by Sapera were frozen when capture began."
                : "One or both capture-time camera-configuration snapshots are missing.");

        Add(
            report,
            "Point cloud",
            state.PointCloud.Count >= settings.Production.MinimumPointCloudPoints
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            $"Points {state.PointCloud.Count}; minimum {settings.Production.MinimumPointCloudPoints}.");

        Add(
            report,
            "Centerline slices",
            state.LocalCenterlines.Count >= settings.Production.MinimumLocalCenterlineSlices
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            $"Slices {state.LocalCenterlines.Count}; minimum {settings.Production.MinimumLocalCenterlineSlices}.");

        Add(
            report,
            "Radius-field coverage",
            state.BinnedRadiusField.Count >= settings.Production.MinimumBinnedRadiusCells
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            $"Cells {state.BinnedRadiusField.Count}; minimum {settings.Production.MinimumBinnedRadiusCells}.");

        var recommendation = state.FinalSpindleRecommendation;
        OperatorScanQualityResult? operatorScan = state.OperatorScanQuality;
        bool operatorScanUsable = operatorScan is not null &&
            (operatorScan.Status == ScanQualityStatus.Good ||
             (!settings.Plc.RequireGoodQuality && operatorScan.Status == ScanQualityStatus.Warning));

        Add(
            report,
            "Operator scan-quality gate",
            operatorScanUsable ? ProductionCheckStatus.Pass : ProductionCheckStatus.Block,
            operatorScan is null
                ? "The operator scan-quality result was not generated."
                : operatorScanUsable
                    ? $"Scan status {operatorScan.Status}; coverage {operatorScan.ProfileCoveragePercent:F1}%; valid slices {operatorScan.ValidSlicePercent:F1}%."
                    : operatorScan.Status == ScanQualityStatus.Warning && settings.Plc.RequireGoodQuality
                        ? "Scan status Warning. Plc.RequireGoodQuality is enabled, so a clean rescan is required before PLC command delivery."
                        : $"Scan status {operatorScan.Status}. Rescan is required before PLC command delivery.");

        Add(
            report,
            "Final spindle recommendation",
            recommendation is not null && recommendation.MachineMoveAllowed
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            recommendation is null
                ? "No final spindle recommendation exists."
                : recommendation.MachineMoveAllowed
                    ? recommendation.RecommendedAction
                    : "Recommendation is blocked by machine limits.");

        bool finiteCommands = recommendation is not null &&
            AreFinite(
                recommendation.CommandHeadXMM,
                recommendation.CommandHeadYMM,
                recommendation.CommandTailXMM,
                recommendation.CommandTailYMM);

        Add(
            report,
            "Finite command values",
            !settings.Safety.RequireFiniteCommands || finiteCommands
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            finiteCommands
                ? "All four XY offsets are finite."
                : "One or more XY offsets are NaN or infinite.");

        if (recommendation is not null)
        {
            bool withinLimits =
                Math.Abs(recommendation.CommandTailXMM) <= settings.MachineLimits.MaxTailMoveXMM &&
                Math.Abs(recommendation.CommandTailYMM) <= settings.MachineLimits.MaxTailMoveYMM &&
                Math.Abs(recommendation.CommandHeadXMM) <= settings.MachineLimits.MaxHeadMoveXMM &&
                Math.Abs(recommendation.CommandHeadYMM) <= settings.MachineLimits.MaxHeadMoveYMM;

            Add(
                report,
                "Hard machine travel limits",
                withinLimits ? ProductionCheckStatus.Pass : ProductionCheckStatus.Block,
                withinLimits
                    ? "All offsets are within commissioned hard limits."
                    : "At least one offset exceeds its hard limit.");

            bool hasWarnings = recommendation.Warnings.Count > 0;
            Add(
                report,
                "Movement warnings",
                hasWarnings && settings.Safety.BlockOnMovementWarning
                    ? ProductionCheckStatus.Block
                    : hasWarnings
                        ? ProductionCheckStatus.Warning
                        : ProductionCheckStatus.Pass,
                hasWarnings
                    ? string.Join(" | ", recommendation.Warnings)
                    : "No movement warnings.");
        }

        double veneerThicknessMM = settings.UsableRadius.VeneerThicknessMM;
        double requiredClearanceMM = 10.0 * veneerThicknessMM;
        double configuredClearanceMM =
            settings.Safety.ComputeKnifeClearanceMM(veneerThicknessMM);

        Add(
            report,
            "Knife clearance",
            configuredClearanceMM + 1e-9 >= requiredClearanceMM
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            $"Configured {configuredClearanceMM:F3} mm; required at least 10 x {veneerThicknessMM:F3} mm = {requiredClearanceMM:F3} mm.");

        bool safePayloadClearance =
            state.PlcPayload is not null &&
            state.PlcPayload.KnifeClearanceMM + 1e-9 >= requiredClearanceMM &&
            state.PlcPayload.SafeCarriageRetractRadiusMM + 1e-9 >=
                state.PlcPayload.LargestMeasuredRadiusMM + requiredClearanceMM;

        Add(
            report,
            "PLC carriage-retract payload",
            safePayloadClearance
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            state.PlcPayload is null
                ? "The restricted PLC payload has not been built."
                : safePayloadClearance
                    ? $"Safe retract radius {state.PlcPayload.SafeCarriageRetractRadiusMM:F3} mm includes at least the required {requiredClearanceMM:F3} mm beyond the measured largest radius."
                    : "The PLC carriage-retract payload does not preserve the required ten-peel-thickness knife clearance.");

        bool reservedRotationSlotsZero =
            state.PlcPayload is null ||
            (Math.Abs(state.PlcPayload.ScannedBlockData[8]) <= 0.000001f &&
             Math.Abs(state.PlcPayload.CarriageRetract[1]) <= 0.000001f &&
             Math.Abs(state.PlcPayload.CarriageRetract[2]) <= 0.000001f &&
             Math.Abs(state.PlcPayload.CarriageRetract[3]) <= 0.000001f &&
             Math.Abs(state.PlcPayload.RoundupComplete[1]) <= 0.000001f &&
             Math.Abs(state.PlcPayload.RoundupComplete[2]) <= 0.000001f &&
             Math.Abs(state.PlcPayload.RoundupComplete[3]) <= 0.000001f);

        Add(
            report,
            "Reserved PLC fields",
            reservedRotationSlotsZero
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            reservedRotationSlotsZero
                ? "All unused payload fields are zero; no rotation value is present."
                : "One or more reserved PLC payload fields are nonzero.");

        bool savedRecordExists =
            !string.IsNullOrWhiteSpace(state.LastSavedRunFolder) &&
            Directory.Exists(state.LastSavedRunFolder);

        Add(
            report,
            "Saved production record",
            !settings.Production.RequireResultSaveBeforePlcWrite || savedRecordExists
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            savedRecordExists
                ? state.LastSavedRunFolder
                : "Result has not yet been saved to an existing folder.");

        bool operatorScanRecordExists =
            savedRecordExists &&
            File.Exists(Path.Combine(
                state.LastSavedRunFolder,
                "operator-scan-quality.json"));

        Add(
            report,
            "Saved operator scan-quality record",
            !settings.Production.RequireResultSaveBeforePlcWrite || operatorScanRecordExists
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            operatorScanRecordExists
                ? "operator-scan-quality.json is preserved with the production scan."
                : "operator-scan-quality.json is missing from the saved production record.");

        bool calibrationSnapshotExists =
            savedRecordExists &&
            File.Exists(Path.Combine(
                state.LastSavedRunFolder,
                "scanner-calibration.json"));

        Add(
            report,
            "Saved calibration snapshot",
            !settings.Production.RequireResultSaveBeforePlcWrite || calibrationSnapshotExists
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            calibrationSnapshotExists
                ? "The exact scanner calibration used for this result is stored with the scan."
                : "scanner-calibration.json is missing from the saved production record.");

        string headSnapshotPath = Path.Combine(
            state.LastSavedRunFolder,
            "head-camera-configuration.snapshot");
        string tailSnapshotPath = Path.Combine(
            state.LastSavedRunFolder,
            "tail-camera-configuration.snapshot");

        bool cameraSnapshotsExist =
            savedRecordExists &&
            File.Exists(headSnapshotPath) &&
            File.Exists(tailSnapshotPath);

        bool cameraSnapshotsMatchCapture =
            cameraSnapshotsExist &&
            FileMatchesSnapshot(
                headSnapshotPath,
                state.HeadCameraConfigurationSnapshot) &&
            FileMatchesSnapshot(
                tailSnapshotPath,
                state.TailCameraConfigurationSnapshot);

        Add(
            report,
            "Saved camera configuration snapshots",
            !settings.Production.RequireResultSaveBeforePlcWrite ||
            cameraSnapshotsMatchCapture
                ? ProductionCheckStatus.Pass
                : ProductionCheckStatus.Block,
            cameraSnapshotsMatchCapture
                ? "The saved head and tail camera-configuration snapshots exactly match the settings applied by Sapera for this capture."
                : cameraSnapshotsExist
                    ? "Saved camera-configuration snapshots do not match the capture-time snapshots."
                    : "One or both camera-configuration snapshots are missing from the saved production record.");

        Add(
            report,
            "PLC connection",
            plcConnected ? ProductionCheckStatus.Pass : ProductionCheckStatus.Block,
            plcConnected
                ? "XYRestricted reports an active PLC connection."
                : "PLC is disconnected; writes are blocked.");

        Add(
            report,
            "Rotation ownership",
            ProductionCheckStatus.Pass,
            "No rotation value or rotation tag is generated by this application; rotation remains PLC-controlled.");

        return report;
    }

    /// <summary>
    /// Checks whether the provided camera configuration snapshot matches the expected camera location.
    /// </summary>
    /// <param name="snapshot">The camera configuration snapshot to check.</param>
    /// <param name="expectedLocation">The expected camera location (ServerName[DeviceIndex]) to match.</param>
    /// <returns>True if the snapshot's location matches the expected location; otherwise, false.</returns>
    private static bool SnapshotLocationMatches(byte[]? snapshot, string expectedLocation)
    {
        if (snapshot is not { Length: > 0 } || string.IsNullOrWhiteSpace(expectedLocation))
            return false;

        try
        {
            using JsonDocument document = JsonDocument.Parse(snapshot);

            if (!document.RootElement.TryGetProperty("ConnectedServerName", out JsonElement serverElement) ||
                !document.RootElement.TryGetProperty("ConnectedDeviceIndex", out JsonElement deviceElement))
                return false;

            string actualServerName = serverElement.GetString()?.Trim() ?? string.Empty;
            int actualDeviceIndex = deviceElement.GetInt32();
            string actualLocation = $"{actualServerName}[{actualDeviceIndex}]";

            return string.Equals(
                actualLocation,
                expectedLocation.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if all provided double values are finite (not NaN or Infinity).
    /// </summary>
    /// <param name="values">The double values to check.</param>
    /// <returns>True if all values are finite; otherwise, false.</returns>
    private static bool AreFinite(params double[] values) =>
        values.All(double.IsFinite);


    /// <summary>
    /// Checks if the file at the specified path matches the provided snapshot byte array.
    /// </summary>
    /// <param name="path">The file path to check.</param>
    /// <param name="snapshot">The snapshot byte array to compare against the file's contents.</param>
    /// <returns>True if the file's contents match the snapshot; otherwise, false.</returns>
    private static bool FileMatchesSnapshot(
        string path,
        byte[]? snapshot)
    {
        if (snapshot is not { Length: > 0 } || !File.Exists(path))
            return false;

        try
        {
            return File.ReadAllBytes(path).AsSpan().SequenceEqual(snapshot);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Adds a new production readiness check to the report with the specified name, status, and message.
    /// </summary>
    /// <param name="report">The production readiness report to add the check to.</param>
    /// <param name="name">The name of the production readiness check.</param>
    /// <param name="status">The status of the production readiness check.</param>
    /// <param name="message">The message associated with the production readiness check.</param>
    private static void Add(
        ProductionReadinessReport report,
        string name,
        ProductionCheckStatus status,
        string message)
    {
        report.Checks.Add(new ProductionReadinessCheck
        {
            Name = name,
            Status = status,
            Message = message
        });
    }
}
