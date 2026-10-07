# Scan Cycle

What the program does with one block, from the PLC ScanStart to the saved record, and what happens when a
step fails. How the measurement itself works is in
[Rotation angles and scan geometry](../scanning/ROTATION_ANGLES.md).

## Order of a scan

1. PLC ScanStart rises: the capture starts and the operator screen is cleared for the new block.
2. PLC ScanComplete rises: frame intake stops and the capture is collected.
3. The scan is processed and the result is shown on the main form and the operator screen.
4. The PLC payload is built and the readiness checks run.
5. The result is written to the PLC and the readback is verified.
6. The scan files, the SQL record and the production audit record are saved.

Steps 5 and 6 are in this order by default; see "Send to the PLC first, save second" below.

## Readiness checks and the PLC write (`Production.EnforceOnlyEssentialReadinessChecks`)

Default `true`. Every readiness check still runs and is written to the report and the
application log, but only these can stop a PLC write:

- Operating mode is Production
- PLC connection
- Operator scan-quality gate (a scan with large gaps or a Failed status is not sent)
- PLC payload could be built
- Final spindle recommendation exists and is allowed
- Finite command values
- Hard machine travel limits
- Knife clearance and the carriage-retract payload
- PLC write order
- Reserved PLC fields are zero

Any other check that fails is shown as `[WARNING] ... NOT ENFORCED (would have blocked)` and
logged as `ERROR (not enforced)` before the write. A failed result save is also logged and
no longer stops the write. Set the setting to `false` to make every check blocking again.

## Send to the PLC first, save second (`Plc.SendResultsBeforeSave`, default `true`)

The PLC write no longer waits for the save, which takes the save time off the PLC's wait.

Rules that keep this safe:

- The save always starts, whether the PLC write succeeded, was blocked, is disabled, or failed.
- The next scan can be **captured** while the save is still running, but it is not **processed** until the
  save has finished (`LogProcessingCoordinator.WaitForPendingSaveAsync`). Processing is what clears and
  reuses the scan data, so the data being saved can never be replaced mid-save.
- The saved-record readiness checks report "Deferred" when the readiness checks run and are brought up to date after the save.
  If any part of the record is missing they are written to the audit record as a not-enforced warning and
  an `ERROR:` line is logged.
- A failed save is logged as an error; it cannot block the PLC write because the write has already happened.
- Closing the application starts the save if it has not started yet and waits up to 20 s for it.
- A scan is saved exactly once.

Set `Plc.SendResultsBeforeSave` to `false` to go back to save first, send second. Save-first is also used
automatically when `Production.EnforceOnlyEssentialReadinessChecks` is `false`, because the full checks
require the saved record before the PLC write and a failed save must then stop it.

## Operator scan-quality screen

When a new block starts scanning, the previous block's numbers and log profile picture are cleared, so
the screen is empty until the new profile is ready.

## When a scan fails

Every failed scan ends in one place (`MainForm.FailScanAndGetReady`), which does four things in order:

1. Stops what is left of that scan (an unfinished capture is cancelled and its frames released).
2. Writes a `SCAN FAILED` block to the diagnostics log with the step it failed at and what happened,
   plus the technical error when there is one.
3. Shows the failure and "Rescan the block" on the lathe operator screen.
4. Puts the application in `Scan Failed - Ready to Rescan`, which accepts the next PLC ScanStart.

Nothing is written to the PLC for a failed scan. What counts as a failed scan:

| Where | Example |
|---|---|
| Starting the capture | cameras not ready |
| Capturing frames | no ScanComplete before the capture timeout, or a camera reached the frame limit first |
| Waiting for the PLC ScanComplete signal | ScanComplete handshake timed out |
| Collecting the captured frames / Processing the scan | frame, calibration, laser-profile or pipeline error |
| Checking the result before the PLC write | scan-quality gate or another enforced check blocked the send (Production mode) |
| Writing the result to the PLC | the PLC write or its readback failed (Production mode) |

If a camera is disconnected the application cannot scan, so it stays in `Faulted` and says which camera;
reconnect in Production Setup. Start-up failures also stay `Faulted`.

When the scan-quality gate is what blocked the send, the operator screen keeps its own RESCAN result and
reason. A scan that was processed but not sent is still saved, so there is a record of it.
