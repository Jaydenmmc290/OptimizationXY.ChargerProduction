# Scanner Calibration — Direct Camera Configuration

Scanner calibration is a machine-specific integrity package. It binds the laser lookup table to the assigned physical cameras, their direct Sapera feature settings, and the concatenation geometry.

```text
<runtime data root>\Config\Calibration\
  scanner-lookup-table.json
  scanner-calibration.json
  head-camera-configuration.snapshot
  tail-camera-configuration.snapshot
  sha256-manifest.txt
  Archive\
```

The two `.snapshot` files are deterministic JSON records created from the live direct Sapera connections. They contain camera identity, server/resource assignment, requested settings, applied/read-back features, and image geometry read-back where supported. They are audit evidence and are not loaded into the cameras.

## Create and activate calibration

Use **Setup / Calibration** in Commissioning mode:

1. Connect the final head and tail cameras with their production server names, resource indices, serial assignments, Mono8 format, exposure, gain, and trigger settings.
2. Confirm the physical head/dog and tail assignments in the previews.
3. Verify the laser line.
4. Capture the final concatenated image and calculate axial pixels/mm from two known-distance marks.
5. Capture at least two accurately measured constant-diameter references with different radii.
6. Complete independent validation measurements.
7. Generate and activate the package. The program stages it, validates dimensions and radius bands, writes SHA-256 hashes, archives the prior package, and performs atomic replacement.

## Fail-closed compatibility checks

Processing is blocked when any of the following no longer matches the active package:

- assigned head or tail camera serial number;
- Sapera server name or device/resource index;
- programmatic/legacy initialization mode;
- pixel format, acquisition mode, or trigger source;
- head/tail orientation, crop, reversal, overlap, seam, vertical offset, or equal-height policy;
- capture-time direct camera snapshot hash;
- concatenated image width or height;
- 36-position acquisition geometry;
- axial pixels/mm or lookup-table structure.

Changing a camera, direct feature configuration, or concatenation setting intentionally invalidates calibration. Reconnect, verify the machine geometry, and recalibrate rather than editing package files manually.

## Saved scans

Every production scan preserves the exact lookup table and the exact head/tail direct camera snapshots frozen when capture began. These files are included in the scan SHA-256 manifest before automatic PLC result delivery can pass readiness.

A loose lookup JSON is not accepted as a production calibration. Use the in-program activation workflow so metadata, serial binding, snapshots, dimensions, geometry fingerprint, archive, and manifest remain consistent.
