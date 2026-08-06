# Production Deployment — Direct Sapera Camera Configuration

## Required production computer

- 64-bit Windows.
- .NET desktop runtime matching the published application, unless published self-contained.
- 64-bit Teledyne DALSA Sapera LT runtime/SDK compatible with the Genie Nano cameras.
- `DALSA.SaperaLT.SapClassBasic.Core.dll` available to the published application.
- The restricted PLC runtime libraries and license.
- SQL Server Express when scan database saving is enabled.
- Camera network interfaces configured and both cameras visible in Sapera Configuration/CamExpert.
- A second operator monitor is recommended, but the operator view falls back to the primary monitor.

## Publish

From elevated PowerShell on the build computer:

```powershell
.\scripts\publish-production.ps1 -SaperaSdkDir "C:\Program Files\Teledyne DALSA\Sapera\Components\NET\Bin"
```

"or powershell -ExecutionPolicy Bypass -File .\scripts\publish-production.ps1"

The publish script requires settings schema 10 and verifies that both camera templates use `Programmatic` initialization with server name, device index, Mono8, exposure, gain, and trigger configuration. CCF files are not required in a production package.

## Install

Copy the release ZIP to the production computer, extract it, and run the included installer from elevated PowerShell:

```powershell
.\install-production.ps1 `
  -PublishedApplicationDirectory "C:\Release\OptimizationXY.Charger\Application" `
  -InstallDirectory "C:\Program Files\OptimizationXY\Charger" `
  -RuntimeAccount "PRODUCTION-PC\ChargerOperator" `
  -SqlInstanceName "SQLEXPRESS"
```

The installer creates the machine data root and grants the runtime account write access only there. It does not install or copy camera CCF files; direct programmatic settings are the production path.

## First-run commissioning

1. Start in `Commissioning` mode.
2. Open Production Setup.
3. Enter the final Sapera server names and resource indices.
4. Set both cameras to `Encoder`, `Mono8`, final exposure/gain, and the verified wired trigger source/activation edge.
5. Reconnect and inspect both previews.
6. Physically confirm head/dog and tail sides.
7. Complete laser, axial scale, and known-radius calibration.
8. Perform dry scans and confirm that the operator profile follows the physical block from head to tail.
9. Confirm `ScanStart` begins capture and `ScanComplete` gates processing.
10. Verify PLC XY sign conventions, movement limits, carriage retract, knife clearance, interlocks, and emergency-stop behavior.
11. Run `validate-production-host.ps1`.
12. Record responsible-person approval and then enable Production mode.

## Production camera connection order

For each camera the application:

1. creates `SapLocation(server, resourceIndex)`;
2. creates `SapAcqDevice` without a CCF in programmatic mode;
3. calls `SapAcqDevice.Create()`;
4. checks feature availability and applies pixel format, exposure, gain, and trigger/frame-rate features;
5. reads back supported values and creates an immutable snapshot;
6. creates the buffer, transfer, and view;
7. starts acquisition.

A required feature that is missing or rejected blocks connection when `RequireConfiguredFeatures` is true.

## Host validation

```powershell
.\validate-production-host.ps1 `
  -ApplicationDirectory "C:\Program Files\OptimizationXY\Charger" `
  -DataRoot "D:\OptimizationXYData" `
  -SqlInstanceName "SQLEXPRESS"
```

The offline validator checks files, schema 10 settings, direct camera configuration, calibration package integrity, scanner geometry, 36-position encoder policy, SQL, PLC policy, safety gates, and commissioning approval. Exact camera feature readback is verified by the application after Sapera connects because an offline script cannot reproduce hardware readback.

## Required final testing

Do not authorize unrestricted machine motion from a build-only test. Final acceptance requires the real cameras, laser, PLC, lathe mechanism, guards, interlocks, and production network. Save commissioning evidence and at least one successful dry-run folder with its SHA-256 manifest.
