# Code Map

A file-by-file guide to `src/OptimizationXY.Charger/`. Every file also starts with (or has on its main
class) a comment that explains it in more detail; this page is the index.

## Words used in the code

| Word | Meaning |
|---|---|
| Head | The end of the block nearest the lathe operator (also "Dog" in the setup wizard). |
| Tail | The far end of the block. Z = 0 is at the tail. |
| X / Y | Across the block. +X is toward the V's, −X toward the lathe. +Y is down, −Y is up. |
| Spindle axis | The line the block turns about. The current axis is (0, 0) in the point cloud. |
| Movement / offset | Where a chosen axis sits relative to the current axis (tail X/Y, head X/Y), in mm. |
| Command | What is sent to the PLC: the **negative** of the movement, converted to **inches**. |
| Centreline | The straight line fitted through the middle of the block (not the spindle axis). |
| Radius field | Every measured point as (Z, angle, radius). Two exist: one about the centreline (shape diagnostics) and one about the spindle axis (choosing the position). |
| Bin / cell | One box of the Z × angle grid the radius field is grouped into. |
| Virtual peel | Turning the measured block about an axis and "peeling" it one veneer thickness at a time to count usable sheets and wane. |
| Wane | Missing wood on a peeled sheet (gap/bark). Severe wane is a lot of missing wood. |
| Scan-quality gate | The Good / Warning / Failed verdict that decides whether a scan may be sent to the PLC. |

## Where to look for…

| Question | File |
|---|---|
| What happens in one production cycle? | `UI/MainForm.cs` (class summary), `docs/operation/SCAN_CYCLE.md` |
| The order of the processing steps | `Services/Core/LogProcessingPipeline.cs` (file header) |
| How the spindle position is chosen (production) | `Services/Optimization/Geometry/InscribedGeometryOptimizer.cs`, `Services/Optimization/Recovery/GeometryPeelSelector.cs` |
| What is sent to the PLC and in what units | `Services/Production/PlcPayloadBuilder.cs`, `Models/Plc/PlcCommandPayload.cs` |
| Which checks can stop a PLC write | `Services/Production/ProductionReadinessEvaluator.cs` |
| How a laser pixel becomes a radius | `Services/Calibration/LookupTableInterpolator.cs` |
| Every setting and its default | `Models/Settings/AppSettings.cs` (and the other files in `Models/Settings`) |
| Where files are written | `Infrastructure/ApplicationPaths.cs`, `Services/Storage/SimpleScanSaveService.cs` |

## Files

### Start-up and paths

- `Program.cs`: entry point. Reads the command line (`--replay`, `--replay-batch`, `--import-calibration-images`, `--set`), loads settings and starts the main form.
- `Infrastructure/ApplicationPaths.cs`: the one place every folder and file path is defined (data root, Config, Calibration, Logs, SavedScans).

### UI

- `UI/MainForm.cs`: the main window and the "conductor" of a production cycle: cameras, PLC, capture, processing, PLC write, saving.
- `UI/Forms/ProductionSetupWizardForm.cs`: the 7-tab setup and calibration wizard (database, cameras, seam, length scale, references, activation).
- `UI/Forms/ProductionSetupContext.cs`: the delegates the wizard uses to ask the main form for camera work.
- `UI/Forms/CalibrationSessionHelper.cs`: saves and restores a half-finished calibration so it can be resumed.
- `UI/Forms/OperatorScanQualityForm.cs`: the lathe-operator screen (state, scan-quality result, log profile).
- `UI/Forms/HeadCameraForm.cs`, `UI/Forms/TailCameraForm.cs`: live camera previews. The slider changes preview brightness only, not the camera.
- `UI/Forms/VisualizationViewerForm.cs`: shows a saved picture in a scrollable window.
- `UI/Controls/LogProfileControl.cs`, `UI/Controls/LogProfilePoint.cs`: the side-view drawing of the block on the operator screen.

### Kinematic (engineering tool)

- `Kinematic/KinematicForm.cs`: engineering window for the kinematic (spindle-rotation) calculator.
- `Kinematic/KinematicsCalculator.cs`: C# copy of the PLC's kinematic rotation logic.
- `Kinematic/KinematicsConfig.cs`: its settings.
- `Kinematic/KinematicPositionSelector.cs`: chooses between the kinematic-rotation result and the plain XY result.
- `Kinematic/KinematicVisualizationState.cs`: everything the calculator worked out, saved for diagnostics.

### Models (data passed between the services)

- `Models/Acquisition`: camera settings (`CameraAcquisitionSettings`, `AcquisitionMode`, `CameraInitializationMode`, `CameraPosition`), camera discovery (`DiscoveredCamera`, `CameraSerialNumber`), frame events, the application state enum, and `CompletedLogCapture` (everything one capture hands to processing).
- `Models/Core/ScanFrame.cs`: one joined head + tail picture at one rotation angle, plus what was measured from it.
- `Models/Calibration`: the scanner lookup table (`ScannerLookupTable`, `LookupPoint`), calibration captures and references, quality summary, the axial scale (`CalibrationResult`) and camera status.
- `Models/Geometry`: points, laser profile columns, calibrated profile columns, centreline, local slices, radius-field points (raw and binned) and the surface grid.
- `Models/Analysis`: ovality, sweep/bow, virtual-peel results, sheet counts and the deferred peel-picture queue.
- `Models/Defects`: the radius defect map, its points and types, and connected defect patches.
- `Models/Optimization`: the optimization mode enum, inscribed-geometry results, the cylinder-vs-oval comparison, recovery candidates/search results, the legacy penalty-search results and scoring profile, the log size summary (`LogGeometryModel`), spindle movements and the **final recommendation** (`FinalSpindleRecommendation`).
- `Models/Operator`: the scan-quality verdict and the operator-screen status.
- `Models/Plc`: the PLC payload (values in inches) and the transmission record.
- `Models/Production`: capture audit, readiness report, machine feedback (mostly unavailable today) and software build identity.
- `Models/Diagnostics/PipelineTimingEntry.cs`: timing of one processing step.
- `Models/Settings`: every setting (`AppSettings` is the root; also operator view, peel view and storage settings).

### Services: capture

- `Services/Camera/GenieNanoCameraService.cs`: opens a Genie Nano camera through Sapera, writes only the trigger settings, reads back its features and makes the configuration snapshot. (`.Unavailable.cs` is the stand-in used when the Sapera SDK is not present.)
- `Services/Camera/SaperaCameraDiscoveryService.cs`: lists the cameras Sapera can see and, when asked, reads their serial numbers.
- `Services/Acquisition/RotatingLogCaptureManager.cs`: collects head and tail frames during a scan and pairs them.
- `Services/Acquisition/DualCameraFrameConcatenator.cs`: joins one head and one tail frame into one full-length picture (crop, flip, offset, overlap).
- `Services/Acquisition/LogProcessingCoordinator.cs`: hands a finished capture to processing, then to the PLC write and saving.
- `Services/Acquisition/RevolutionAngleAssigner.cs`: gives every frame its rotation angle from the one measured revolution.
- `Services/Acquisition/DiagnosticsCollector.cs`: thread-safe counters for the diagnostics display.

### Services: measurement

- `Services/Preprocessing/ImagePreprocessingService.cs`: turns each grey picture into a black/white laser mask.
- `Services/Preprocessing/LaserStripeDetector.cs`: finds the laser line in the mask.
- `Services/Profile/LaserRadiusProfileBuilder.cs`: laser rows → calibrated radius per image column.
- `Services/Calibration/LookupTableInterpolator.cs`: the calibration maths (column + laser row → radius).
- `Services/Calibration/LookupTableStore.cs`: loads the active calibration once per scan.
- `Services/Calibration/ScannerCalibrationBuilder.cs`: builds a calibration from reference captures and checks laser detection.
- `Services/Calibration/ScannerCalibrationCompatibility.cs`: the fingerprint of everything that must not change after calibration.
- `Services/Calibration/CalibrationPackageStore.cs`: activates, archives and restores calibration packages.
- `Services/Calibration/CalibrationFrameCaptureSession.cs`: captures frames for calibration without running production processing.
- `Services/Calibration/CalibrationImageImportRunner.cs`: builds a calibration from saved reference pictures (`--import-calibration-images`).
- `Services/PointCloud/RotatingScanPointCloudBuilder.cs`: profiles + angles → 3D point cloud.
- `Services/Geometry/CenterlineMathService.cs`: fits the block's centreline.
- `Services/Geometry/CenterlineSweepAnalyzer.cs`: sweep and bow of the centreline.
- `Services/Geometry/RadiusFieldGenerator.cs`: the centreline-relative radius field.
- `Services/Geometry/RadiusFieldBinner.cs`: groups radius-field points into Z × angle cells.
- `Services/Analysis/OvalityAnalyzer.cs`: the most out-of-round cross-section.
- `Services/Defects/RadiusDefectMapBuilder.cs`, `RadiusDefectPatchBuilder.cs`, `DefectPatchAnalyzer.cs`: low/high spots, connected patches and their summary (diagnostics in the production mode).
- `Services/Input/ImageInputService.cs`, `Services/Scan/ScanFrameLoader.cs`: load saved pictures as frames (replay and import).

### Services: choosing the spindle position

- `Services/Optimization/Geometry/InscribedGeometryOptimizer.cs`: largest inscribed cylinder / oval search (coarse → medium → fine grid, centreline pull for repeatability).
- `Services/Optimization/Recovery/GeometryPeelSelector.cs`: **production choice** between cylinder and oval by virtual-peel results.
- `Services/Optimization/Recovery/VeneerRecoveryMetricsEvaluator.cs`: fast numbers-only virtual peel used while searching.
- `Services/Optimization/Recovery/DirectRecoveryOptimizer.cs`, `RecoveryStrategySelector.cs`: the RecoveryOptimized mode.
- `Services/Optimization/Search/ChargerOptimizer.cs`, `Scoring/*`, `Wane/SpindleAxisWaneOptimizer.cs`: the legacy penalty-scored search and its scores.
- `Services/Optimization/Movement/SpindleMovementBuilder.cs`: turns offset + sweep into tail/head movements.
- `Visualization/UnwrappedPeelViewBuilder.cs`: the virtual peel itself (`ComputeMetrics` for numbers, `BuildSimulation` for the picture).

### Services: core, quality, PLC and production

- `Services/Core/LogProcessingPipeline.cs`: runs every processing step in order and produces the final recommendation.
- `Services/Core/PipelineState.cs`: the one shared place the current scan's data and results live.
- `Services/Core/DeferredPeelViewRenderer.cs`: draws the queued peel pictures after the PLC write.
- `Services/Core/ScanTimeline.cs`, `UniformScoreComponent.cs`: timing report; one line of the legacy score.
- `Services/Operator/OperatorScanQualityAnalyzer.cs`: the Good / Warning / Failed scan-quality verdict.
- `Services/Production/PlcPayloadBuilder.cs`: builds the PLC numbers (inches), knife clearance and retract radius.
- `Services/Production/ProductionReadinessEvaluator.cs`: the readiness checklist that may block a PLC write.
- `Services/Production/ProductionAuditWriter.cs`: audit files and SHA-256 manifest in the scan folder.
- `Services/Production/ProductionSequenceStore.cs`: the persistent block count sent to the PLC.
- `Services/Plc/XyRestrictedPlcService.cs`: the PLC connection, ScanStart/ScanComplete handshake, writes and readback.

### Services: saving, settings and replay

- `Services/Storage/ScanResultSaveService.cs`: saves the folder first, then the optional SQL record.
- `Services/Storage/SimpleScanSaveService.cs`: writes the `SavedScans\yyyy\MM\dd\Block_####\Scan_####` folder and applies retention.
- `Services/Storage/SqlScanSaveService.cs`: writes the scan to SQL Server (values in inches).
- `Services/Configuration/AppSettingsStore.cs`, `AppSettingsValidator.cs`, `AppSettingsDiagnosticsBuilder.cs`: load/upgrade, validate and summarise `appsettings.json`.
- `Services/Configuration/SqlDatabaseSetupService.cs`, `DatabaseConnectionStringResolver.cs`: SQL setup and the connection string.
- `Services/Configuration/ProductionSetupStatusWriter.cs`: writes `production-setup-status.json` for people checking an installation.
- `Services/Replay/OfflineReplayRunner.cs`, `BatchReplayRunner.cs`, `SettingsOverride.cs`: `--replay`, `--replay-batch` and `--set`.

### Pictures

- `Visualization/RotatingRadiusSectionOverlayBuilder.cs`: tail / middle / head cross-section picture (seen from the tail).
- `Visualization/LogHeadEndViewOverlayBuilder.cs`: end-on picture seen from the head (main form overlay box).
- `Visualization/RadiusDefectHeatmapBuilder.cs`: low-spot heat map (Z × angle).
- `Visualization/UnwrappedPeelViewBuilder.cs`: virtual peel numbers and picture (see above).

## Tests (`tests/OptimizationXY.Charger.Tests`)

Each test class has a summary saying what it proves: accuracy options, batch replay, home-axis
calibration, peel metrics (numbers-only peel equals the drawn peel), radius profiles, repeatability,
retract radius and the timing report. The GitHub build check runs them on every push.
