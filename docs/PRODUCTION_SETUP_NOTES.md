# Production Setup Notes — Schema 10

## Camera policy

Production uses direct Sapera acquisition-device initialization. The application opens each camera with its Sapera server name and zero-based device/resource index, verifies the physical serial assignment, applies required GenICam features programmatically, and then creates buffers and transfer resources.

No `.ccf` file is installed, copied, published, or loaded in normal production operation. A legacy configuration-file branch remains in source only for controlled migration troubleshooting and is blocked by production readiness.

Required per-camera values:

- physical position: Head or Tail;
- Sapera server name;
- resource index;
- expected serial number learned and locked by the setup wizard;
- `Programmatic` initialization;
- `Mono8` pixel format;
- positive exposure and non-negative gain;
- `Encoder` mode and the commissioned trigger source;
- `RequireConfiguredFeatures = true`.

Any camera identity or feature change invalidates the scanner calibration.

## Lathe operator view

The separate operator screen is read-only and contains no live camera image. It updates from the existing scan workflow and existing restricted PLC events:

- PLC connected/disconnected;
- `ScanStart` state;
- `ScanComplete` state;
- synchronized frame-pair count;
- processing state;
- processed head-to-tail radius profile;
- missing profile sections highlighted in red;
- frame, axial coverage, valid-slice, length, diameter, and largest-gap measurements.

No additional PLC tags or writes are required. The view uses the existing `ScanStartChanged`, `ScanCompleteChanged`, and connection-state events.

When `Plc.RequireGoodQuality` is enabled, only a `Good` operator scan-quality result can pass production readiness. A warning or failure requires a rescan and blocks automatic PLC result delivery.

## Runtime data

```text
<runtime data root>\
  Config\appsettings.json
  Config\Calibration\...
  Config\Setup\production-setup-status.json
  Logs\Application.log
  SavedScans\...
```

Set `OPTIMIZATION_XY_DATA_DIR` to a dedicated production data directory when desired. The runtime account receives Modify access only to this data root, not to Program Files.

## Release workflow

1. Build on Windows x64 with Sapera LT installed and `SAPERA_SDK_DIR` pointing to `Components\NET\Bin`.
2. Run `scripts\publish-production.ps1`.
3. Copy the generated release ZIP to the production computer.
4. Run `install-production.ps1` as administrator.
5. Start in Commissioning mode and complete Production Setup.
6. Reconnect both cameras and let the wizard learn/verify their serial numbers.
7. Recreate calibration using the final direct settings.
8. Complete dry scans, PLC sign checks, safety/interlock validation, and commissioning approval.
9. Run `validate-production-host.ps1` and retain its report.
10. Enable Production only after responsible-person approval.
