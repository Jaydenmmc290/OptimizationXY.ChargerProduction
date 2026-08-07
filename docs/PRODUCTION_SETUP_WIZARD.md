# Production Setup Wizard — Schema 10

The setup wizard now configures both Genie Nano cameras directly through Sapera LT. Normal production operation does **not** load a `.ccf` file. A legacy CCF mode remains in the camera service only for controlled troubleshooting and migration.

## Camera page

For each camera enter and verify:

- Sapera server name, for example `Nano-M1920_1`.
- Expected physical serial number. Leave blank only for the first controlled connection; the wizard learns it from the camera and saves it. Future mismatches fail closed.
- Zero-based Sapera device/resource index, normally `0`.
- Acquisition mode: `Encoder` for production or `FrameRate` for controlled testing.


Press **Save Camera Settings and Reconnect**. The application constructs `SapLocation(serverName, deviceIndex)`, creates `SapAcqDevice` without a configuration file, and applies the required camera feature of writing to TriggerSource before creating buffers and transfer resources.

Changing a server, resource index, expected serial, pixel format, exposure, gain, trigger source, trigger activation, acquisition mode, crop, flip, overlap, or seam invalidates the active scanner calibration. This is intentional: those settings can change the image coordinate system or laser measurement.

Open both previews and physically confirm:

- Head preview = head/dog side.
- Tail preview = tail side.
- Laser stripe is visible and stable.


## Calibration snapshot

Each successful camera connection creates an immutable JSON configuration snapshot containing the selected Sapera server/resource, requested settings, and the feature values read back from the camera when available. Calibration captures and production scans freeze these bytes. The active calibration stores SHA-256 hashes for both snapshots.

A camera feature or identity mismatch blocks processing until the calibrated settings are restored or calibration is repeated.

## Calibration workflow

Complete the setup wizard in order. Camera registration must be finalized before dimensional calibration because the combined Head/Tail image is the coordinate system used by later calibration steps.

1. Put the machine in a calibration-safe state: machine motion inhibited, knife retracted, reference secured, guarded area clear, and normal PLC production scanning inhibited.
2. Configure and verify the local SQL database.
3. Connect both cameras using their final Sapera server name and device index.
4. Select `Encoder` acquisition mode for production.
5. Reconnect both cameras and verify their physical serial identities.
6. Open both camera previews.
7. Physically confirm that the Head/Dog preview shows the Head/Dog side and the Tail preview shows the Tail side.
8. Open **Camera Alignment / Seam**.
9. Configure Head/Tail image order and horizontal reversal.
10. Configure Head and Tail crop values.
11. Configure Tail vertical registration.
12. Configure the Head/Tail overlap.
13. Capture a combined preview and adjust the settings until a physical feature crosses the seam continuously with no duplicated or missing image region.
14. Verify and save the camera seam.
15. Verify the laser using the finalized combined-image geometry.
16. Measure and save the axial pixels-per-millimeter scale.
17. Capture at least two known constant-diameter references with different measured diameters.
18. Complete independent validation measurements when available.
19. Generate, validate, archive, and activate the scanner calibration package.
20. Perform controlled dry scans.
21. Complete PLC direction, travel-limit, knife-clearance, guarding, interlock, emergency-stop, and operator-display validation.
22. Run final production-host validation.
23. Record responsible-person commissioning approval.
24. Enable Production mode only after all required release gates pass.

Changing the Sapera camera identity, acquisition configuration, image crop, camera order, horizontal reversal, vertical offset, overlap, seam, or other calibration-critical geometry after scanner calibration invalidates the affected calibration. Restore the calibrated settings or repeat the scanner calibration before returning to production.


## Operator scan view

The separate lathe-operator form opens automatically, normally on display index `1` (the second monitor). It shows:

- current state: waiting, scanning, waiting for `ScanComplete`, processing, good, warning, or rescan;
- simple head-to-tail radius profile;
- missing profile sections in red;
- frames used, profile coverage, valid slices, block length, average diameter, and largest missing gap;
- PLC connection, `ScanStart`, and `ScanComplete` state.

The screen evaluates scan completeness only. Sweep, taper, ovality, and low wood describe the block and do not automatically mean the scan was poor.
