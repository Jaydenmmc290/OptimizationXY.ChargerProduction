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
| `summary.csv` | One line per scan: commands, inscribed diameter, usable/total sheets, scan quality, processing time |
| `summary.txt` | Totals, failed scans, changes against a baseline, and the spread of the commands between repeat scans of the same block |
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

Scan the same block two or more times (`Scan_0001`, `Scan_0002`, ... in one block folder). The
"REPEAT SCANS OF THE SAME BLOCK" section shows how far apart the commands are. That spread is the
repeatability of the whole system.

### Options to compare (all off by default)

| `--set` | What it changes |
|---|---|
| `RotatingScan.KeepAllStripeSegments=true` | Keep every laser-line piece that lines up with the longest one, instead of only the longest piece |
| `RotatingScan.SubPixelStripeCenter=true` | Brightness-weighted stripe centre (about 0.1 px) instead of the thresholded midpoint (0.5 px steps) |
| `Geometry.OptimizationCellIgnoreLowestReadings=1` | Ignore the single lowest reading of a well-filled cell in the spindle search, so one noisy low reading cannot decide the position |
| `InscribedGeometry.UseCertifiedCylinderSearch=true` | After the grid search, search the whole machine travel and report a certified gap: no position can score more than that many mm better |
| `OptimizationMode=RecoveryOptimized` | Choose the position by simulated veneer recovery (direct refinement around all the strategy seeds) instead of cylinder vs oval |

Turn an option on in production only after the batch shows it gains sheets or tightens the
repeat-scan spread without losing anywhere.
