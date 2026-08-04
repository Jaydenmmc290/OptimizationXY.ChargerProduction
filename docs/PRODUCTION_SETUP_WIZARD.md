# Production Setup Wizard — Schema 10

The setup wizard now configures both Genie Nano cameras directly through Sapera LT. Normal production operation does **not** load a `.ccf` file. A legacy CCF mode remains in the camera service only for controlled troubleshooting and migration.

## Camera page

For each camera enter and verify:

- Sapera server name, for example `Nano-M1920_1`.
- Expected physical serial number. Leave blank only for the first controlled connection; the wizard learns it from the camera and saves it. Future mismatches fail closed.
- Zero-based Sapera device/resource index, normally `0`.
- Acquisition mode: `Encoder` for production or `FrameRate` for controlled testing.
- Pixel format: `Mono8`.
- Exposure time in microseconds.
- Gain.
- Trigger source, normally the wired frame/encoder trigger input such as `Line1`.
- Trigger activation, normally the commissioned physical edge such as `RisingEdge`.

Press **Save Camera Settings and Reconnect**. The application constructs `SapLocation(serverName, deviceIndex)`, creates `SapAcqDevice` without a configuration file, and applies the required camera features before creating buffers and transfer resources.

Changing a server, resource index, expected serial, pixel format, exposure, gain, trigger source, trigger activation, acquisition mode, crop, flip, overlap, or seam invalidates the active scanner calibration. This is intentional: those settings can change the image coordinate system or laser measurement.

Open both previews and physically confirm:

- Head preview = head/dog side.
- Tail preview = tail side.
- Laser stripe is visible and stable.
- Both images are Mono8 and have the expected dimensions.

## Calibration snapshot

Each successful camera connection creates an immutable JSON configuration snapshot containing the selected Sapera server/resource, requested settings, and the feature values read back from the camera when available. Calibration captures and production scans freeze these bytes. The active calibration stores SHA-256 hashes for both snapshots.

A camera feature or identity mismatch blocks processing until the calibrated settings are restored or calibration is repeated.

## Calibration workflow

1. Put the machine in calibration-safe state: motion inhibited, knife retracted, reference secured, guarded area clear.
2. Connect both cameras using their final production settings.
3. Confirm physical head/tail assignment.
4. Capture laser-verification frames.
5. Measure and save the axial pixels-per-millimeter scale.
6. Capture at least two known, constant-diameter references with different radii.
7. Complete independent validation measurements.
8. Generate, validate, archive, and activate the calibration package.
9. Complete the physical commissioning checklist before switching to Production mode.

The calibration package continues to preserve opaque camera-configuration snapshot files for integrity and backward compatibility. They are audit evidence; Sapera does not load them during normal programmatic camera connection.

## Operator scan view

The separate lathe-operator form opens automatically, normally on display index `1` (the second monitor). It shows:

- current state: waiting, scanning, waiting for `ScanComplete`, processing, good, warning, or rescan;
- simple head-to-tail radius profile;
- missing profile sections in red;
- frames used, profile coverage, valid slices, block length, average diameter, and largest missing gap;
- PLC connection, `ScanStart`, and `ScanComplete` state.

The screen evaluates scan completeness only. Sweep, taper, ovality, and low wood describe the block and do not automatically mean the scan was poor.
