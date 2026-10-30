# Architecture

## Active runtime path

```text
MainForm
  -> GenieNanoCameraService (head and tail)
  -> RotatingLogCaptureManager
       -> DualCameraFrameConcatenator
  -> CompletedLogCapture (one concatenated frame per head/tail pair)
  -> LogProcessingCoordinator
  -> LogProcessingPipeline.RunCurrentFramesPipeline
  -> OperatorScanQualityAnalyzer (scan-quality gate)
  -> PlcPayloadBuilder + ProductionReadinessEvaluator
  -> XyRestrictedPlcService (PLC write)
  -> ScanResultSaveService (by default after the PLC write)
       -> SimpleScanSaveService (scan folder, deferred peel pictures)
       -> optional SqlScanSaveService
       -> ProductionAuditWriter
```

The live-capture and folder-processing entry points share one ordered processing method. This prevents the two workflows from silently skipping different analysis stages.

## Acquisition boundary

Every head/tail pair between PLC ScanStart and ScanComplete is captured (typically 85-100 pairs). `RotatingLogCaptureManager` pairs the frames by sequence and `DualCameraFrameConcatenator` creates one full-length image per pair before handing ownership to `CompletedLogCapture`. Frame angles are assigned afterwards from one measured revolution (`RevolutionAngleAssigner`, see [`ROTATION_ANGLES.md`](../scanning/ROTATION_ANGLES.md)).

Concatenation supports:

- head-left or tail-left placement,
- independent horizontal reversal,
- independent left/right crop,
- tail vertical registration offset,
- controlled overlap/seam placement,
- strict equal-height enforcement for production measurement.

The UI preview calls the same concatenator with the persisted acquisition settings. Display normalization occurs before preview concatenation only and never enters the measurement pipeline.

## Responsibilities

- `Models`: settings and domain data only.
- `Services/Camera`: Sapera integration or the non-hardware stub.
- `Services/Acquisition`: live capture, frame pairing, concatenation, counters, and processing coordination.
- `Services/Calibration`: validated scanner lookup-table persistence and interpolation.
- `Services/Core`: pipeline ordering and state ownership.
- `Services/Preprocessing`, `Profile`, `Scan`, `Input`: frame clean-up, laser-line profiles and loading frames from folders.
- `Services/PointCloud`, `Geometry`, `Analysis` and `Defects`: measured log geometry and diagnostics.
- `Services/Optimization/Search` and `Scoring`: legacy penalty-scored spindle search and its scoring.
- `Services/Optimization/Geometry`: largest-inscribed-cylinder and largest-inscribed-oval searches.
- `Services/Optimization/Recovery`: virtual-peel comparison and final geometry selection.
- `Services/Optimization/Movement` and `Wane`: turning a chosen axis into spindle moves; wane helpers.
- `Services/Operator`: the scan-quality result shown to the lathe operator (and the PLC gate).
- `Services/Production`: PLC payload, readiness checks, audit records, block/scan sequence numbers.
- `Services/Plc`: the XYRestricted PLC connection, handshake and write/readback.
- `Services/Storage`: result-folder creation, manifests, optional SQL persistence, and retention.
- `Services/Configuration`: loading/upgrading/validating `appsettings.json` and SQL setup.
- `Services/Replay`: offline replay of saved scans (`--replay`, `--replay-batch`).
- `Kinematic`: the kinematic position form and its calculations.
- `Visualization`: display-only overlays and virtual peel images.
- `UI`: WinForms interaction and operator status.

## Shared processing order

Every optimization mode first runs the same measured-analysis stages:

1. Preprocess frames and find the laser line (per frame, in parallel).
2. Apply calibration to get a radius per image column.
3. Assign each frame its rotation angle from the one measured revolution.
4. Build the measured point cloud (and a separate display-only cloud).
5. Fit the centerline.
6. Build the radius fields (centreline-relative for diagnostics, spindle-axis-relative for choosing the position).
7. Bin the radius fields.
8. Analyze ovality and centerline sweep.
9. Analyze radius defects.
10. Analyze connected defect patches.

The selected optimization branch then runs:

```text
PenaltyScored
  -> adaptive penalty search
  -> selected virtual peel

LargestInscribedCylinder
  -> conservative cylinder-envelope search
  -> cylinder-axis virtual peel

LargestInscribedOval
  -> conservative ellipse-envelope search
  -> physical circular-lathe peel at the oval-selected axis

CompareInscribedGeometry
  -> cylinder search + peel
  -> oval search + peel
  -> select by usable full 50-inch sheets, usable length,
     rejected sheets, severe wane, then total wane
     (a small wane-only oval advantage keeps the cylinder)

RecoveryOptimized
  -> seeds: current axis, penalty search, cylinder, oval
  -> bounded direct recovery search around the seeds
  -> select by the same virtual-peel order (then smaller move)
```

`CompareInscribedGeometry` is the production mode. See
[`OPTIMIZATION_MODES.md`](OPTIMIZATION_MODES.md). The decision uses virtual-peel numbers only;
the peel pictures are drawn after the PLC write, when the scan is saved.

Defect maps and patches are retained in every branch for diagnostics. Their weights, risk score, and penalty score are not inputs to spindle selection in an inscribed-geometry branch.

## Measurement boundary

`PipelineState.PointCloud` contains measured points only. `DisplayPointCloud` may include small interpolated gaps when enabled. Geometry, defect analysis, scoring, and spindle recommendations must use `PointCloud`, not `DisplayPointCloud`.

The inscribed searches use each binned cell's conservative minimum radius when available, falling back to its mean only when a minimum is unavailable. Missing angular bins are filled conservatively from neighboring valid bins before evaluating a candidate.

## Physical-lathe boundary

The largest-inscribed oval is a spindle-axis selection strategy, not a claim that a conventional rotary lathe peels an elliptical path. The virtual peel always models the installed circular rotary motion around the selected fixed head/tail spindle axis. This keeps the cylinder-versus-oval comparison physically executable.

## Calibration boundary

`LookupTableStore` loads one validated lookup table per processing run. The same in-memory table is reused for all frames. Missing calibration is a hard failure; no estimated table is generated.

## Hardware boundary

The Sapera reference is conditional. `UseSapera=false` compiles a stub with the same public camera API so the rest of the application can be built without the proprietary SDK. Production hardware builds must supply `SAPERA_SDK_DIR` or `SaperaSdkDir`.

## Writable data

All mutable files are rooted under `ApplicationPaths.RootDirectory`, normally `%LOCALAPPDATA%\OptimizationXY\Charger` unless `OPTIMIZATION_XY_DATA_DIR` is set. Production cameras are configured directly from schema-10 settings and create immutable capture-time JSON snapshots; no camera CCF is installed or required. `NLog.config` remains read-only deployment content beside the executable.

Every saved scan contains immutable concatenated input-frame copies, diagnostics, a frame manifest, the measured point cloud, and available overlays. `PipelineState.LastSavedRunFolder` always identifies the exact `Scan_####` folder used for optional SQL persistence.

## Deliberate remaining debt

`MainForm.cs` and `LogProcessingPipeline.cs` remain large. Splitting them without compiler-backed tests and hardware reference data would be riskier than retaining their current behavior. The next refactor should introduce tests around concatenation registration, calibration, point-cloud construction, geometry envelopes, peel-sheet classification, scoring, and settings before decomposing these classes.
