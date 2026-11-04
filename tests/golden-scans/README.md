# Golden scans

Saved production scans whose answer must not change. `GoldenScanTests` replays
every scan folder here that has a `golden-expected.json` and fails the build if
the head/tail position, inscribed diameter, strategy or sheet counts moved.

## Adding one

1. Copy a saved scan folder (the `Scan_####` folder with `Frames\` and
   `scanner-calibration.json`) from `SavedScans` into this folder, e.g.
   `tests\golden-scans\2026-09-11_Block_0012\Scan_0001`. Pick 2 or 3 normal
   blocks; each is roughly the size of its `Frames` folder.
2. Copy the PRODUCTION `appsettings.json` to the development computer's
   `%LOCALAPPDATA%\OptimizationXY\Charger\Config\` (the answer is recorded with
   those settings and the settings are saved next to it).
3. Run `OptimizationXY.Charger.exe --golden-record "<repo>\tests\golden-scans"`.
   It writes `golden-settings.json` and `golden-expected.json` into each scan.
4. Commit the folders.

Re-record only on purpose, when a change of result is intended.

To keep the scans out of git instead, put them anywhere and set the
`OXY_GOLDEN_SCANS` environment variable to that folder; the tests use it on
that computer only.
