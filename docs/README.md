# Optimization XY Charger

Windows Forms software for dual-camera rotating-log acquisition, calibrated 3D geometry analysis, defect mapping, virtual peel simulation, and head/tail spindle-axis recommendations.

## Repository layout

```text
src/OptimizationXY.Charger/  Application source
scripts/                     Build, publish, and calibration scripts
docs/                        Architecture and commissioning notes
.github/workflows/            Non-hardware CI build and dependency audit
```

## Current measurement flow

Each complete scan captures 36 synchronized positions:

```text
36 head frames + 36 tail frames
  -> pair by capture index
  -> crop/orient/register each pair
  -> concatenate each pair into one full-length image
  -> 36 concatenated frames enter the processing pipeline
```

The live combined preview uses the same crop, orientation, overlap, vertical-offset, camera-side, and equal-height rules as the saved processing images. See `docs/DUAL_CAMERA_CONCATENATION.md`.

## Lathe operator scan view

A separate full-screen, read-only operator form normally opens on the second monitor. It shows the processed head-to-tail radius profile, red missing-scan sections, scan-completeness metrics, and current PLC `ScanStart`/`ScanComplete` state. It uses the existing PLC events and adds no PLC tags or writes.

## Optimization modes

The operator can select one of five modes:

- `RecoveryOptimized`: default production mode. Builds baseline, penalty-search, largest-cylinder, and largest-oval seeds, performs a bounded direct XY recovery refinement around those seeds, renders the candidates with the physical circular virtual peel, and chooses the best usable-sheet result.
- `PenaltyScored`: existing adaptive defect/wane/risk optimizer.
- `LargestInscribedCylinder`: chooses the fixed spindle axis with the largest conservative circular envelope.
- `LargestInscribedOval`: chooses the fixed spindle axis with the largest conservative elliptical envelope.
- `CompareInscribedGeometry`: builds cylinder and oval candidates, simulates both on the physical circular rotary lathe, and selects the output with the greatest usable full 50-inch sheet recovery.

All modes still run preprocessing, calibration, profile extraction, point-cloud construction, centerline fitting, radius-field construction/binning, radius-defect analysis, and defect-patch analysis. In the two inscribed-geometry modes, defect and penalty values remain diagnostics only; they do not move the spindle recommendation. See `docs/OPTIMIZATION_MODES.md`.

## Requirements

- Windows x64
- .NET 10 SDK pinned by `global.json`
- Teledyne DALSA Sapera SDK/runtime for Genie Nano hardware operation
- ASComm IoT 1.5 and approved production runtime licensing
- `XYRestricted.dll` and `AutomatedSolutions.ASCommStd.dll` under `lib/XYRestricted`
- Visual Studio with the .NET desktop workload, or the .NET CLI

The project can compile without Sapera for CI and non-hardware engineering. Camera operations then fail with a clear runtime message instead of a missing-reference compiler error.

## Build

Hardware build, using `SAPERA_SDK_DIR`:

```powershell
$env:SAPERA_SDK_DIR = "C:\Program Files\Teledyne DALSA\Sapera\Components\NET\Bin"
./scripts/build.ps1
```

Non-hardware validation build:

```powershell
./scripts/build.ps1 -SkipSapera
```

The MSBuild equivalents are `SaperaSdkDir=<path>` and `UseSapera=false`.

## Publish

Engineering publish:

```powershell
./scripts/publish.ps1 -SelfContained
```

Machine release publish:

```powershell
./scripts/publish-production.ps1
```

The production script requires Sapera hardware support, verifies restricted PLC dependencies and safety defaults, and creates a hash-manifested release ZIP.

## First-run production setup

The main operator form includes **Setup / Calibration** and opens it automatically when SQL, camera, laser, length-scale, or active-calibration evidence is missing. The wizard can:

- create/upgrade the local `OptimizationXY` SQL Express database with Windows Integrated Security;
- configure direct Sapera server/resource connections, learn and lock physical camera serial numbers, and reconnect the final head/tail cameras;
- open separate head/tail previews for physical-side confirmation;
- verify the laser line with the production preprocessing path;
- capture a concatenated image and calculate axial pixels/mm from two clicked marks;
- capture known round references and automatically build the lookup table;
- stage, hash, validate, archive, activate, and roll back calibration packages.

See `docs/PRODUCTION_SETUP_WIZARD.md` and `docs/CALIBRATION.md`. Production does not accept a loose lookup-table JSON; create and activate a complete package through the in-program wizard.

## Runtime data

By default, writable data is stored outside the application installation directory:

```text
%LOCALAPPDATA%\OptimizationXY\Charger\
  Config\appsettings.json
  Config\Calibration\scanner-lookup-table.json
  Config\Calibration\scanner-calibration.json
  Config\Calibration\head-camera-configuration.snapshot
  Config\Calibration\tail-camera-configuration.snapshot
  Config\Calibration\sha256-manifest.txt
  Config\Calibration\Archive\
  Config\Setup\production-setup-status.json
  Logs\Application.log
  SavedScans\
```

Override the root when the production machine uses a dedicated data drive:

```powershell
$env:OPTIMIZATION_XY_DATA_DIR = "D:\OptimizationXYData"
```

## Saved scan contents

Each `SavedScans\...\Scan_####` folder contains:

- `DiagnosticsReport.txt`
- copy-preserved concatenated input images under `Frames\`
- `frames.csv` with sequence, angle, status, and source/saved paths
- `point_cloud.csv`
- `scanner-calibration.json` containing the exact lookup table used for the result
- `head-camera-configuration.snapshot` and `tail-camera-configuration.snapshot` containing the exact direct Sapera identity, requested settings, and applied/read-back features frozen when capture began
- available images under `Overlays\`
- `unwrapped_peel_view.png` for the selected final recommendation
- `largest_inscribed_cylinder_peel.png` when a cylinder candidate was evaluated
- `largest_inscribed_oval_peel.png` when an oval candidate was evaluated
- `direct_recovery_refinement_peel.png` for the direct veneer-recovery search result
- command/readiness/transmission JSON records
- `software-build.json` containing the application version and SHA-256 identity of the running binary
- `sha256-manifest.txt` covering the complete saved record

## Safety defaults

- Production starts fail-closed in `Commissioning` mode; physical approval flags are false.
- A named operating-system mutex blocks a second controller instance.
- Production requires `RecoveryOptimized` with direct virtual-peel refinement enabled.
- PLC-triggered production capture requires a fresh `ScanStart` edge and a good `ScanComplete` after the 36-frame quota.
- Manual start, manual early-stop, and optimization-mode changes are disabled in Production mode.
- Carriage/knife clearance is never less than 10 peel thicknesses; default 3.2 mm veneer gives 32.0 mm.
- No rotation value or rotation tag is generated; rotation remains PLC-controlled.
- PLC outputs are saved first, validated, written only through the restricted tags, and read back with quality/tolerance checks.
- Each production scan preserves its scanner calibration, both direct camera-configuration snapshots, operator scan-quality result, settings, software-build hash, and a complete SHA-256 manifest before PLC motion data is authorized.
- Missing or invalid scanner calibration blocks processing.
- SQL persistence is disabled and its connection string is blank by default until the in-program local SQL Express setup succeeds.
- Raw measurement point clouds never use visualization gap fill.
- Geometry searches honor machine travel as a hard feasibility limit, not as a score.
- Production concatenation rejects unequal frame heights by default instead of silently rescaling measurement data.
- Logs rotate at 10 MB and retain ten archives.
- Hardware builds target x64.

Read `docs/PRODUCTION_DEPLOYMENT.md`, `docs/COMMISSIONING_CHECKLIST.md`, `docs/PLC_INTEGRATION.md`, and `docs/SAFETY_AND_INTERLOCKS.md` before commissioning the application.
