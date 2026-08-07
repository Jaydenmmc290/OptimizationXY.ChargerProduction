# Production Deployment — Direct Sapera Camera Configuration

This guide explains the complete deployment process from the development computer to the production computer. Follow the sections in order. Do not copy the source-code folder directly to production.

## 1. Confirm the production computer is ready

Before installing the XY Charger application, confirm that the production computer has:

- 64-bit Windows.
- The .NET desktop runtime that matches the published application, unless the application was published self-contained.
- A compatible 64-bit Teledyne DALSA Sapera LT runtime/SDK for the Genie Nano cameras.
- `DALSA.SaperaLT.SapClassBasic.Core.dll` available to the published application.
- The required restricted PLC runtime libraries and license.
- SQL Server Express if scan database saving is enabled.
- Camera network interfaces configured correctly.
- Both cameras visible in Sapera Configuration or CamExpert.
- A second operator monitor when available. The operator view can fall back to the primary monitor.

Do not continue to machine commissioning until the cameras, PLC dependencies, SQL instance, and production network are available.

## 2. Create the production release on the development computer

### 2.1 Open PowerShell in the project folder

On the development computer, open PowerShell and go to the root of the project:

```powershell
cd "C:\Users\jmcbride\XYChargerJayden\OptimizationXY.Charger_Jayden_ProductionHardened_Source"
```

The project root should contain items such as:

```text
scripts\
src\
OptimizationXY.Charger.slnx
```

### 2.2 Run the production publish script

Because PowerShell script execution is restricted on this computer, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-production.ps1
```

The script already uses the normal Sapera SDK location when it can find it. Therefore, the command above is the normal command for this development computer.

Only specify `-SaperaSdkDir` when the Sapera SDK is installed somewhere else or the script cannot locate it:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-production.ps1 `
  -SaperaSdkDir "C:\Program Files\Teledyne DALSA\Sapera\Components\NET\Bin"
```

The publish process requires settings schema 10. It verifies that both camera templates use `Programmatic` initialization with the server name, device index, Mono8 pixel format, exposure, gain, and trigger configuration. Production CCF files are not required.

### 2.3 Confirm that the release ZIP was created

After the script finishes successfully, open:

```text
<project folder>\artifacts\production-release\
```

You should see a timestamped release folder and a matching ZIP file, similar to:

```text
artifacts\production-release\
├── OptimizationXY.Charger-20260806-134500\
└── OptimizationXY.Charger-20260806-134500.zip
```

The ZIP contains the same release package as the uncompressed folder.

**Copy the `.zip` file to the production computer.** You do not need to copy the uncompressed release folder from the development computer.

## 3. Copy the ZIP to the production computer

Use a trusted network share, approved USB drive, or other approved transfer method.

Copy the ZIP into a temporary release location on the production computer. A recommended location is:

```text
C:\Release\
```

For example:

```text
C:\Release\OptimizationXY.Charger-20260806-134500.zip
```

The ZIP is only the transfer package. Do not point the installer directly at the ZIP file.

## 4. Extract the ZIP on the production computer

### 4.1 Extract the package

In File Explorer:

1. Open `C:\Release`.
2. Right-click the release ZIP.
3. Select **Extract All**.
4. Extract it into `C:\Release`.

After extraction, the folder structure should look similar to:

```text
C:\Release\OptimizationXY.Charger-20260806-134500\
├── Application\
├── install-production.ps1
├── validate-production-host.ps1
├── OptimizationXY.Schema.sql
└── other release files
```

The exact timestamp in the folder name will be different for every release.

### 4.2 Confirm the required files exist

Before running the installer, confirm that the extracted release folder contains:

- `Application\`
- `install-production.ps1`
- `validate-production-host.ps1`

The `Application` folder is the published application directory that must be passed to the installer.

## 5. Identify the production runtime account

The runtime account is the Windows account that will operate or run the XY Charger application.

Sign in using that account, open PowerShell, and run:

```powershell
whoami
```

The result will look similar to:

```text
PRODUCTION-PC\ChargerOperator
```

Use the exact value returned by `whoami` for the `-RuntimeAccount` parameter. Do not leave the example computer name unchanged unless it matches the real production computer.

## 6. Run the installer on the production computer

### 6.1 Open PowerShell as Administrator

1. Open the Windows Start menu.
2. Search for **PowerShell**.
3. Right-click PowerShell.
4. Select **Run as administrator**.
5. Approve the User Account Control prompt.

The PowerShell window must be elevated because the installer writes to `C:\Program Files` and configures permissions.

### 6.2 Go to the extracted release folder

Replace the timestamp below with the timestamp of the folder you extracted:

```powershell
cd "C:\Release\OptimizationXY.Charger-20260806-134500"
```

Confirm that you are in the correct folder:

```powershell
Get-ChildItem
```

You should see `Application` and `install-production.ps1` in the output.

### 6.3 Run the installer

Because PowerShell script execution may be restricted, use this command from inside the extracted release folder:

```powershell
powershell -ExecutionPolicy Bypass -File .\install-production.ps1 `
  -PublishedApplicationDirectory ".\Application" `
  -InstallDirectory "C:\Program Files\OptimizationXY\Charger" `
  -RuntimeAccount "PRODUCTION-PC\ChargerOperator" `
  -SqlInstanceName "SQLEXPRESS"
```

Replace:

```text
PRODUCTION-PC\ChargerOperator
```

with the exact runtime account found using `whoami`.

Using:

```powershell
-PublishedApplicationDirectory ".\Application"
```

works because PowerShell is currently inside the extracted release folder. It also avoids editing the timestamped release path every time a new ZIP is created.

The installer copies the application from the extracted `Application` folder into:

```text
C:\Program Files\OptimizationXY\Charger
```

The installer also creates the machine data root and grants the runtime account write access there. It does not install or copy camera CCF files because direct programmatic camera settings are the production configuration path.

### 6.4 Confirm the application was installed

After the installer completes, verify that this folder exists:

```text
C:\Program Files\OptimizationXY\Charger
```

In elevated PowerShell, you can check it with:

```powershell
Get-ChildItem "C:\Program Files\OptimizationXY\Charger"
```

Do not delete the extracted release folder until installation and validation are complete. It provides the installer, validator, SQL file, and the exact release package used for deployment.

## 7. Run production-host validation

From the extracted release folder, run the included validation script from elevated PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\validate-production-host.ps1 `
  -ApplicationDirectory "C:\Program Files\OptimizationXY\Charger" `
  -DataRoot "D:\OptimizationXYData" `
  -SqlInstanceName "SQLEXPRESS"
```

Use the real production data-root path if it is not `D:\OptimizationXYData`.

The offline validator checks:

- Required application files.
- Settings schema 10.
- Direct camera configuration.
- Calibration package integrity.
- Scanner geometry.
- The 36-position encoder policy.
- SQL availability.
- PLC policy.
- Safety gates.
- Commissioning approval state.

The offline validator cannot reproduce live camera feature readback. The application verifies exact camera feature values after Sapera connects to the hardware.

Do not continue to unrestricted production operation if validation reports a failed required check.

## 8. Complete first-run commissioning

The first launch must be treated as commissioning, not normal production operation.

1. Start the application in `Commissioning` mode.
2. Open **Production Setup**.
3. Enter the final Sapera server name and resource index for each camera.
4. Set both cameras to `Encoder` trigger mode.
5. 
6. 
7. Select the verified wired trigger source.
8. Reconnect both cameras.
9. Confirm that both live previews appear and update correctly.
10. Physically confirm which camera is the head/dog camera and which is the tail camera.
11. Complete laser calibration.
12. Complete axial-scale calibration.
13. Complete known-radius calibration.
14. Perform dry scans without authorizing unrestricted machine movement.
15. Confirm that the operator profile follows the physical block from head to tail.
16. Confirm that `ScanStart` begins image capture.
17. Confirm that `ScanComplete` ends acquisition and gates processing.
18. Verify PLC X and Y sign conventions on both head and tail sides.
19. Verify maximum movement limits.
20. Verify carriage-retract behavior.
21. Verify knife clearance.
22. Verify all machine interlocks.
23. Verify emergency-stop behavior.
24. Run `validate-production-host.ps1` again after final settings and calibration are saved.
25. Record approval from the responsible person.
26. Enable Production mode only after commissioning and safety validation pass.

## 9. Understand the production camera connection sequence

For each camera, the application performs this sequence:

1. Creates `SapLocation(server, resourceIndex)`.
2. Creates `SapAcqDevice` without a CCF when using programmatic mode.
3. Calls `SapAcqDevice.Create()`.
4. Checks whether required camera features are available.
5. Reads Trigger source and writes to it if needed for Encoder or Fixed Rate
6. Creates the image buffer, transfer object, and view.
7. Starts acquisition.


When `RequireConfiguredFeatures` is enabled, a missing or rejected required feature blocks the camera connection. Do not bypass this failure without determining which feature or value was rejected.

## 10. Required final testing before production use

A successful build or installation does not authorize unrestricted machine motion.

Final acceptance requires testing with the real:

- Head and tail cameras.
- Laser system.
- PLC.
- XY charger mechanism.
- Lathe mechanism.
- Physical guards.
- Safety interlocks.
- Emergency-stop circuit.
- Production network.

Save:

- Commissioning evidence.
- Validation results.
- Camera and PLC configuration records.
- At least one successful dry-run output folder.
- The dry-run folder's SHA-256 manifest.
- The exact release ZIP used for the installation.

## 11. Normal update process after the first installation

For each future software update:

1. Close the production application.
2. Create a new release ZIP on the development computer by running:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\publish-production.ps1
   ```

3. Copy only the new timestamped ZIP to the production computer.
4. Extract the ZIP into a new folder under `C:\Release`.
5. Open PowerShell as Administrator.
6. Change into the newly extracted release folder.
7. Run the included `install-production.ps1` using `-PublishedApplicationDirectory ".\Application"`.
8. Run `validate-production-host.ps1`.
9. Confirm the cameras, PLC connection, saved settings, calibration, and database operation.
10. Perform a controlled dry scan before returning the system to production.

Keep the previous known-good release ZIP and extracted release folder until the new version passes validation and controlled testing.
