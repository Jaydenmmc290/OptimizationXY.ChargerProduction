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

The first launch on a production computer is a commissioning procedure, not normal production operation.

Keep the application in **Commissioning** mode until every required scanner, PLC, machine-safety, and approval check has passed.

### 8.1 Open Production Setup

1. Start the installed XY Charger application.
2. Confirm that the application is in **Commissioning** mode.
3. Open **Production Setup / Calibration**.
4. On **Step 1 - Overview**, confirm that the displayed runtime data root, settings path, calibration path, and saved-scan path are the XY Charger application.
5. intended production locations.

### 8.2 Configure SQL

5. Open **Step 2 - Local SQL Express**.
6. Test the configured SQL Server connection.
7. Create or upgrade the `OptimizationXY` database if required.
8. Save and enable SQL only after the expected schema version is verified.

### 8.3 Connect and identify both cameras

### Connect and assign both cameras

1. Verify both physical cameras are powered and visible to Sapera.
2. Open **Production Setup**.
3. Open **Step 3 - Cameras**.
4. Select **Refresh Connected Cameras**.
5. Confirm that two acquisition cameras are discovered.
6. Select the physical camera mounted at Head / Dog.
7. Select the physical camera mounted at Tail.
8. Confirm that the two selections have different serial numbers.
9. Select Encoder mode for both cameras.
10. Select **Save Camera Assignment and Reconnect**.
11. Open both previews.
12. Physically verify the Head / Dog and Tail assignments.

Do not manually depend on Sapera names such as `Nano-M1920_1` or
`Nano-M1920_2`. Those names are runtime device locations and are discovered
by the application.

The physical camera serial number is the persistent Head / Tail identity.

Do not continue to dimensional calibration until the two physical camera assignments are correct.

### 8.4 Align and verify the Head/Tail camera seam

18. Open **Step 4 - Camera Alignment / Seam**.
19. Place a straight or recognizable reference so that a common physical feature can be seen in the overlap region between both cameras.
20. Set the Head/Tail image order and horizontal reversal options correctly.
21. Adjust the Head and Tail crop values to remove unusable camera edges.
22. Adjust the Tail vertical offset so the same physical feature appears at the same vertical position in both images.
23. Adjust the overlap value so the two images meet without duplicating or removing part of the physical block.
24. Select **Apply Settings and Capture Combined Preview**.
25. Inspect the combined image at the seam.
26. Repeat the adjustments and capture another preview until the same physical feature crosses the seam continuously with no duplicated region and no missing region.
27. Check the camera-seam verification box.
28. Select **Verify and Save Camera Seam**.

The camera seam must be finalized before axial or known-reference calibration. Changing crop, reversal, camera order, vertical offset, overlap, or seam after calibration invalidates the affected scanner calibration and requires calibration to be repeated.

### 8.5 Verify the laser

29. With the final seam settings still active, select **Verify Laser**.
30. Confirm that the laser stripe is visible, stable, and usable across the complete combined Head-to-Tail image.
31. Do not continue until both **Camera Seam** and **Laser** report ready.

### 8.6 Measure the axial length scale

32. Open **Step 5 - Axial Length Scale**.
33. Place two visible reference marks a precisely known physical distance apart.
34. Capture the combined calibration image.
35. Click the first reference mark.
36. Click the second reference mark.
37. Enter the true physical distance between the marks.
38. Calculate `LengthPixelsPerMM`.
39. Review the result and select **Save Length Scale**.

### 8.7 Capture known diameter references

40. Open **Step 6 - Known References**.
41. Install the first known round reference.
42. Measure its physical diameter accurately.
43. Enter the operator name, reference name, and measured diameter.
44. Confirm that the entered diameter is valid for the scanned reference.
45. Select **Capture 36 Pairs and Add Reference**.
46. Repeat the process using at least one additional reference with a different known diameter.
47. Confirm that at least two distinct radius bands are accepted by the wizard.

Do not change camera position, crop, orientation, seam, overlap, or vertical registration between reference captures.

### 8.8 Generate and activate scanner calibration

48. Open **Step 7 - Review and Activate**.
49. Complete independent reference validation when available.
50. Enter calibration notes and commissioning information as required.
51. Confirm that SQL, Cameras, Physical Sides, Camera Seam, Laser, Length Scale, and the required known references all report ready.
52. Select **Generate, Validate, Archive, and Activate Calibration**.
53. Confirm that a valid calibration ID is generated and activated.

Calibration activation does not authorize unrestricted machine operation. The system remains in Commissioning mode until physical machine commissioning is approved.

### 8.9 Perform controlled machine commissioning

54. Perform controlled dry scans without authorizing unrestricted charger or lathe movement.
55. Confirm that the operator profile follows the physical block from Head/Dog to Tail.
56. Confirm that the scanner acquires the expected synchronized Head/Tail frame pairs.
57. Confirm that the PLC `ScanStart` rising edge starts exactly one capture cycle.
58. Confirm that processing waits for the correct `ScanComplete` condition.
59. Verify Head X and Y physical sign conventions.
60. Verify Tail X and Y physical sign conventions.
61. Verify machine travel and software movement limits.
62. Verify carriage-retract behavior.
63. Verify knife clearance.
64. Verify PLC readback behavior.
65. Verify guarding and machine interlocks.
66. Verify emergency-stop and safe-restart behavior.
67. Verify the operator scan-quality display using known good and deliberately failed scans.

### 8.10 Final production validation

68. Run the production-host validator again after the final settings and scanner calibration have been saved:

```powershell
powershell -ExecutionPolicy Bypass -File .\validate-production-host.ps1 `
  -ApplicationDirectory "C:\Program Files\OptimizationXY\Charger" `
  -DataRoot "D:\OptimizationXYData" `
  -SqlInstanceName "SQLEXPRESS"
```

69. Resolve every required production-readiness failure.
70. Save at least one successful controlled dry-run package and its SHA-256 manifest.
71. Record the responsible person's commissioning approval.
72. Enable **Production** mode only after scanner calibration, PLC validation, machine-safety testing, controlled dry scans, host validation, and responsible-person approval have all passed.

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
