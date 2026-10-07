# Direct Camera and Lathe Operator View Implementation

## Implemented

- Direct `SapAcqDevice` initialization by Sapera server name and device/resource index.
- Physical camera identity binding: the setup wizard saves the head/tail assignment as Sapera server name and device index; the serial number is recorded in the camera snapshot, and a snapshot that differs from the calibrated one blocks processing.
- Required-feature fail-closed behavior with per-feature diagnostics.
- Immutable, deterministic camera-configuration snapshots for calibration and production audit hashing.
- Setup-wizard fields for server, resource index, acquisition mode, and trigger source.
- Calibration invalidation whenever camera identity/features or acquisition geometry changes.
- Separate full-screen operator form with processed radius profile, missing-section highlighting, scan metrics, and PLC tag state.
- Scan-quality analyzer built from the existing `PipelineState.BinnedRadiusField`, `ScanFrames`, geometry bin sizes, and acquisition frame target.
- PLC lifecycle integration using the existing restricted `ScanStartChanged` and `ScanCompleteChanged` events; no additional PLC tags or writes were added.
- A failed operator scan quality is always a production-readiness BLOCK and prevents automatic PLC result delivery; a warning blocks it when `Plc.RequireGoodQuality` is enabled.
- Schema 10 settings, production template, publish checks, host validation, and commissioning documentation.

## Validation status

Source structure, delimiters, JSON, and cross-file references were statically checked in the provided environment. A final hardware build and runtime test must be completed on a Windows x64 computer with the exact Sapera LT SDK, both Genie Nano cameras, laser, PLC runtime/license, SQL instance, and lathe safety system. Hardware feature names and trigger wiring must be confirmed against the installed camera firmware and machine wiring before Production approval.
