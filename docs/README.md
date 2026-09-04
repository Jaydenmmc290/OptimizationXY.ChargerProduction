# Optimization XY Charger Documentation

Windows Forms software for dual-camera rotating-log acquisition, calibrated 3D geometry analysis, defect mapping, virtual peel simulation, and head/tail spindle-axis recommendations.

## Documentation map

Start with:

- `START_HERE.md` — first installation, commissioning, and update overview.
- `PRODUCTION_DEPLOYMENT.md` — supported ClickOnce deployment and update process.
- `PRODUCTION_SETUP_WIZARD.md` — in-program production setup and camera assignment.
- `CALIBRATION.md` — scanner calibration requirements.
- `COMMISSIONING_CHECKLIST.md` — final machine commissioning checks.
- `SAFETY_AND_INTERLOCKS.md` — production safety requirements.
- `ARCHITECTURE.md` — system architecture and major components.
- `DUAL_CAMERA_CONCATENATION.md` — head/tail image registration and concatenation.
- `OPTIMIZATION_MODES.md` — optimization strategy behavior.

PLC and restricted-library documentation is maintained separately and is not changed as part of the ClickOnce documentation cleanup.

## Current measurement flow

Each complete scan captures 36 synchronized positions:

```text
36 head frames + 36 tail frames
  -> pair by capture index
  -> crop/orient/register each pair
  -> concatenate each pair into one full-length image
  -> 36 concatenated frames enter the processing pipeline
```

The live combined preview uses the same crop, orientation, overlap, vertical-offset, camera-side, and equal-height rules as the saved processing images. See `DUAL_CAMERA_CONCATENATION.md`.

## Production deployment

ClickOnce is the supported production deployment method.

The normal production flow is:

```text
Development computer
  -> build and test
  -> Visual Studio ClickOnce Publish
  -> approved deployment location
  -> production computer installs once
  -> future published versions are delivered as ClickOnce updates
```

The previous ZIP-based process using `publish-production.ps1` and `install-production.ps1` is legacy and should not be used for normal production installation or updates.

See `PRODUCTION_DEPLOYMENT.md` for the full process.

## First-run production setup

The main operator form includes **Setup / Calibration** and opens it automatically when required setup or calibration evidence is missing.

The wizard can:

- create or upgrade the local OptimizationXY SQL Express database when database saving is enabled;
- discover Sapera acquisition cameras;
- assign the physical Head / Dog and Tail cameras;
- learn and verify physical camera serial numbers;
- reconnect the final head/tail cameras;
- open separate head/tail previews for physical-side confirmation;
- configure and verify camera crop, orientation, vertical registration, overlap, and seam;
- verify the laser line with the production preprocessing path;
- capture a concatenated image and calculate axial pixels/mm from two clicked marks;
- capture known round references and build the scanner lookup calibration;
- stage, hash, validate, archive, activate, and roll back calibration packages.

See `PRODUCTION_SETUP_WIZARD.md` and `CALIBRATION.md`.

## Runtime data

Writable production data is kept outside the ClickOnce-managed application installation.

The configured runtime data root contains items such as:

```text
Config\
Config\Calibration\
Config\Setup\
Logs\
SavedScans\
```

Do not use the ClickOnce installation directory as permanent storage for machine configuration, calibration packages, logs, or scan records.

## Updates

After the production computer has installed the application once through ClickOnce:

1. make and test the software change on the development computer;
2. publish the next version using the same approved Visual Studio ClickOnce profile;
3. start the installed production application;
4. allow ClickOnce to install the newer published version according to the configured update policy;
5. perform the validation appropriate for the change before returning the machine to normal production.

Do not copy the Git repository to the production computer for normal updates.

## Commissioning

Installation and software update success do not replace machine commissioning.

Before enabling Production mode, complete the required camera, calibration, machine-direction, travel-limit, clearance, guarding, interlock, emergency-stop, and controlled-scan checks described in `COMMISSIONING_CHECKLIST.md` and `SAFETY_AND_INTERLOCKS.md`.
