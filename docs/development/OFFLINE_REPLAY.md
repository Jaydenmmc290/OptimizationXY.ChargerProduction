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
