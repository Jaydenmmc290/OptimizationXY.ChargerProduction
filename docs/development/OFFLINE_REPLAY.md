# Offline Replay

The processing of a scan can be re-run on saved frames without cameras or a PLC:

```
OptimizationXY.Charger.exe --replay
OptimizationXY.Charger.exe --replay "C:\path\to\Scan_0001"
```

In Visual Studio: project Properties, Debug, "Command line arguments": `--replay`.

Pick a saved scan folder (it contains `Frames` and `scanner-calibration.json`) or any folder
of frame images; if no calibration file is found beside the frames you are asked for
`scanner-lookup-table.json`. The report is written into the chosen folder as
`ReplayDiagnosticsReport_<time>.txt` and opened. Nothing is sent to the PLC or written to
SQL. The `appsettings.json` on the computer running the replay is used, so copy the
production file to `%LOCALAPPDATA%\OptimizationXY\Charger\Config` (or `<OPTIMIZATION_XY_DATA_DIR>\Config` when that variable is set) first when the result has
to match production.

## Building on the development computer

Build in Visual Studio, or with `msbuild` from a Developer Command Prompt. The `dotnet` command line
(`dotnet build`, `dotnet run`) cannot build this project: it stops with error MSB4803 because of the
licensed-component file (`licenses.licx`).

## Batch replay: every saved scan at once

```
OptimizationXY.Charger.exe --replay-batch "\\fileserver\groups\TFCPublic\OptimizationXY\Charger\SavedScans"
```

Every folder under the chosen folder that contains `Frames` is replayed with the calibration saved
beside it. Results go to `%LOCALAPPDATA%\OptimizationXY\Charger\ReplayBatch\ReplayBatch_<time>\`
(or `--out <folder>`); nothing is written into the scan folders, the PLC or SQL.

| File | What it holds |
|---|---|
| `summary.csv` | One line per scan: commands, inscribed diameter, usable/total sheets, scan quality, processing time, revolution length (frames), distance of the chosen axis from the block's own centreline |
| `summary.txt` | Totals, failed scans, changes against a baseline, and how well repeat scans of the same block agree |
| `reports\` | The full diagnostics report of every scan |

### Checking a change

1. Run the batch once with the current settings. Its `summary.csv` is the **baseline**.
2. Run it again with the change, pointing at the baseline:

```
OptimizationXY.Charger.exe --replay-batch "<SavedScans>" --baseline "<first run>\summary.csv" --set Geometry.OptimizationCellIgnoreLowestReadings=1
```

`summary.txt` then lists every scan whose command moved more than `--tolerance-mm` (default 0.5) or
whose usable sheets changed, with totals for sheets gained and lost.

`--set Section.Name=value` changes a setting for that run only (it is never saved); use it as many
times as needed. It also works with `--replay`, after the folder.

### Repeatability

Scan the same block two or more times. The block is dropped and re-chucked between scans, so it
sits differently on the spindles each time and the machine commands are *expected* to differ.
"REPEAT SCANS OF THE SAME BLOCK" therefore compares each group inside the block:

- **axis**: distance of the chosen peel axis from the block's own fitted centreline (largest of tail/head)
- **inscribed diameter spread**, **usable sheets**, **revolution length** (and how many were not verified)
- **cylinder/oval**: how often each was chosen

Scans in one folder (`Scan_0001`, `Scan_0002`, ...) are one group. Otherwise list them:

```
--replay-batch "<SavedScans>" --same-block "09\11\Block_0001,09\11\Block_0002,09\11\Block_0003" --same-block "09\11\Block_0008,09\11\Block_0009"
```

A name can be a block folder with its date (`09\11\Block_0001`, matches every scan under it), a scan folder name, or a path relative to the scans folder.

### Options to compare (off by default unless noted)

| `--set` | What it changes |
|---|---|
| `RotatingScan.KeepAllStripeSegments=true` | Keep every laser-line piece that lines up with the longest one, instead of only the longest piece |
| `RotatingScan.SubPixelStripeCenter=true` | Brightness-weighted stripe centre (about 0.1 px) instead of the thresholded midpoint (0.5 px steps) |
| `Geometry.OptimizationCellIgnoreLowestReadings=1` | Ignore the single lowest reading of a well-filled cell in the spindle search, so one noisy low reading cannot decide the position |
| `InscribedGeometry.UseCertifiedCylinderSearch=true` | After the grid search, search the whole machine travel and report a certified gap: no position can score more than that many mm better |
| `RotatingScan.ApproximateClosureToleranceMM=2.5` | When the turn does not close within 1.5 mm, take the frame that comes closest (if within 2.5 mm) as the end of the revolution instead of the start of the final rest (reported as approximate, not verified) |
| `RotatingScan.SubFrameRevolutionClosure=true` | Place the 360 degree point between two frames (e.g. 84.6 frames) instead of rounding the revolution to whole frames |
| `InscribedGeometry.OvalMinimumWaneAdvantagePercent=0` | **On by default (1).** When cylinder and oval give the same sheets, the oval is chosen only if it cuts wane by at least 1%. `=0` restores the old rule (oval wins on any wane difference) |
| `OptimizationMode=RecoveryOptimized` | Choose the position by simulated veneer recovery (direct refinement around all the strategy seeds) instead of cylinder vs oval |

Turn an option on in production only after the batch shows it gains sheets or tightens the
repeat-scan spread without losing anywhere.
