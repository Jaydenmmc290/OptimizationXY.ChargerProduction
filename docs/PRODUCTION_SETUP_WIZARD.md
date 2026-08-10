# Production Setup Wizard — Schema 10

The setup wizard now configures both Genie Nano cameras directly through Sapera LT. Normal production operation does **not** load a `.ccf` file. A legacy CCF mode remains in the camera service only for controlled troubleshooting and migration.

## Camera discovery and assignment

The Production Setup Wizard automatically discovers acquisition cameras currently
visible to Sapera. Camera server names are not manually entered and are not used
as permanent physical-camera identities.

For each discovered camera the wizard displays:

- current Sapera server name;
- current device/resource index;
- physical camera serial number.

Assign one discovered physical camera to **Head / Dog** and one to **Tail**.

The application saves the serial number read directly from each camera as the
persistent physical-camera assignment. The Sapera server name and device index
are runtime locations and may change when cameras are reconnected, replaced,
rediscovered, or enumerated in a different order.

On application startup or camera reconnect, the software:

1. discovers the currently available Sapera acquisition devices;
2. reads their physical serial numbers;
3. locates the serial assigned to Head / Dog;
4. locates the serial assigned to Tail;
5. updates the current Sapera server name and device index;
6. connects to those resolved devices;
7. verifies the camera identity again after connection.

A change in Sapera server name alone does not invalidate scanner calibration.

Replacing a physical camera with a different serial number DOES require the new
camera to be explicitly assigned to that machine position and the affected
scanner calibration to be repeated.

## Calibration snapshot

Each successful camera connection creates an immutable JSON configuration snapshot containing the selected Sapera server/resource, requested settings, and the feature values read back from the camera when available. Calibration captures and production scans freeze these bytes. The active calibration stores SHA-256 hashes for both snapshots.

A camera feature or identity mismatch blocks processing until the calibrated settings are restored or calibration is repeated.

## Calibration workflow

Complete the setup wizard in order. Camera registration must be finalized before dimensional calibration because the combined Head/Tail image is the coordinate system used by later calibration steps.

1. Put the machine in a calibration-safe state: machine motion inhibited, knife retracted, reference secured, guarded area clear, and normal PLC production scanning inhibited.
2. Configure and verify the local SQL database.
3. Open **Step 3 - Cameras**.
4. Select **Refresh Connected Cameras**.
5. Confirm that both physical cameras appear in the discovered-camera list.
6. Assign the physical Head / Dog camera.
7. Assign the physical Tail camera.
8. Select Encoder acquisition mode for both cameras.
9. Select **Save Camera Assignment and Reconnect**.
10. Select **Show Both Camera Previews**.
11. Confirm that the Head / Dog preview shows the Head / Dog side.
12. Confirm that the Tail preview shows the Tail side.
13. Close either preview when desired. Closing a preview hides the window and
    does not disconnect or destroy the active Sapera camera resource.
14. Continue to **Step 4 - Camera Alignment / Seam**.
15. Open **Camera Alignment / Seam**.
16. Configure Head/Tail image order and horizontal reversal.
17. Configure Head and Tail crop values.
18. Configure Tail vertical registration.
19. Configure the Head/Tail overlap.
20. Capture a combined preview and adjust the settings until a physical feature crosses the seam continuously with no duplicated or missing image region.
21. Verify and save the camera seam.
22. Verify the laser using the finalized combined-image geometry.
23. Measure and save the axial pixels-per-millimeter scale.
24. Capture at least two known constant-diameter references with different measured diameters.
25. Complete independent validation measurements when available.
26. Generate, validate, archive, and activate the scanner calibration package.
27. Perform controlled dry scans.
28. Complete PLC direction, travel-limit, knife-clearance, guarding, interlock, emergency-stop, and operator-display validation.
29. Run final production-host validation.
30. Record responsible-person commissioning approval.
31. Enable Production mode only after all required release gates pass.

Changing the Sapera camera identity, acquisition configuration, image crop, camera order, horizontal reversal, vertical offset, overlap, seam, or other calibration-critical geometry after scanner calibration invalidates the affected calibration. Restore the calibrated settings or repeat the scanner calibration before returning to production.


## Operator scan view

The separate lathe-operator form opens automatically, normally on display index `1` (the second monitor). It shows:

- current state: waiting, scanning, waiting for `ScanComplete`, processing, good, warning, or rescan;
- simple head-to-tail radius profile;
- missing profile sections in red;
- frames used, profile coverage, valid slices, block length, average diameter, and largest missing gap;
- PLC connection, `ScanStart`, and `ScanComplete` state.

The screen evaluates scan completeness only. Sweep, taper, ovality, and low wood describe the block and do not automatically mean the scan was poor.
