# Implementation and Validation Report

## Scope completed

This source release integrates two related production changes:

1. A separate read-only lathe-operator scan-quality display that updates throughout the existing PLC-controlled scan lifecycle and shows the processed head-to-tail block profile after every scan.
2. Direct Genie Nano initialization through Sapera LT using the configured server name and zero-based acquisition-device resource index, without loading a CCF during normal production operation.

## Operator-view implementation

The operator display uses the existing production pipeline instead of creating a second measurement path:

- frame completion comes from `PipelineState.ScanFrames`;
- the displayed profile comes from `PipelineState.BinnedRadiusField`;
- axial and angular resolution come from the existing geometry settings;
- length and diameter are calculated from the same binned radius values used by the optimization pipeline;
- invalid or under-covered axial slices are highlighted in red;
- the result is classified as `Good`, `Warning`, or `Failed` using configurable `OperatorView` thresholds;
- `operator-scan-quality.json` is stored with each production scan;
- a warning or failed result blocks automatic PLC result delivery when `Plc.RequireGoodQuality` is enabled.

The view reuses the existing restricted PLC lifecycle events and does not add new PLC writes:

- `ScanStartChanged` updates the operator status and the existing rising-edge request starts capture;
- `ScanCompleteChanged` updates the operator status and the existing handshake gates processing;
- `ConnectionStateChanged` shows PLC connection state;
- no component writes `ScanStart` or `ScanComplete`.

## Direct-camera implementation

For each camera, programmatic connection now follows this order:

1. create `SapLocation(serverName, deviceIndex)`;
2. create `SapAcqDevice(location)` with no CCF;
3. call `SapAcqDevice.Create()`;
4. read and lock the physical camera serial number;
5. check and apply production features such as `Mono8`, exposure, gain, and trigger/frame-rate mode;
6. read back supported feature and image-geometry values;
7. freeze a deterministic JSON configuration snapshot for calibration and scan auditing;
8. create the Sapera buffer, transfer, and view resources.

A controlled legacy CCF mode remains in the service for migration/troubleshooting, but production readiness and production publishing require `Programmatic` initialization with required-feature enforcement.

## Static validation completed in this package

The source package was checked for:

- valid JSON settings/templates;
- valid XML project, resource, configuration, and solution files;
- balanced C# comments, strings, delimiters, and preprocessor blocks;
- balanced PowerShell delimiters and strings;
- unresolved merge-conflict markers;
- missing required implementation files;
- production-template schema and direct-camera invariants;
- accidental distribution of `.ccf` files;
- ZIP contents and excluded build/cache folders.

These checks completed without a detected structural error.

## Required Windows and machine acceptance testing

The supplied environment is not a Windows x64 production host and does not contain the installed Sapera LT SDK/runtime, PLC runtime/license, cameras, laser, SQL instance, or lathe. Therefore the following checks must be completed before Production approval:

- build and publish with `RequireSapera=true` against the exact installed `DALSA.SaperaLT.SapClassBasic.Core.dll`;
- connect both cameras by the commissioned server/resource values and confirm distinct serial-number binding;
- verify the actual camera firmware accepts every required feature name and value;
- verify `TriggerSource` and `TriggerActivation` against the physical encoder/frame-trigger wiring;
- confirm the final width, height, offsets, pixel format, exposure, and gain readbacks;
- capture exactly 36 synchronized head/tail pairs through a complete PLC rotation cycle;
- verify `ScanComplete` gates processing and neither lifecycle tag is written by this application;
- test known-good, warning, obstructed, incomplete, camera-disconnected, and PLC-disconnected scans;
- verify the operator form on the selected lathe monitor and confirm HEAD / DOG and TAIL orientation;
- verify calibration compatibility, saved snapshots, scan audit files, SQL saving, restricted PLC readback, XY signs, travel limits, knife clearance, guarding, interlocks, emergency stop, and safe restart;
- run `validate-production-host.ps1` with no blocking result;
- record authorized commissioning approval before enabling Production mode.

`FrameRate` acquisition is intended only for controlled preview or commissioning tests. Production `Encoder` mode must not be approved until the real trigger line and activation edge have been observed to produce one correctly synchronized frame pair at each required angular position.
