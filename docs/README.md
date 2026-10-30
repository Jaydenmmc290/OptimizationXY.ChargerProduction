# Optimization XY Charger Documentation

Windows Forms software for dual-camera rotating-log acquisition, calibrated 3D geometry analysis, defect mapping, virtual peel simulation, and head/tail spindle-axis recommendations.

## Documentation map

New to the project? Read [Start here](getting-started/START_HERE.md) first.

### Getting started

- [Start here](getting-started/START_HERE.md): first installation, commissioning and update overview.

### Deployment and commissioning

- [Production deployment](deployment/PRODUCTION_DEPLOYMENT.md): the supported ClickOnce deployment and update process.
- [Production setup wizard](deployment/PRODUCTION_SETUP_WIZARD.md): in-program production setup and camera assignment.
- [Commissioning checklist](deployment/COMMISSIONING_CHECKLIST.md): final machine commissioning checks.
- [Safety and interlocks](deployment/SAFETY_AND_INTERLOCKS.md): production safety requirements and software gates.
- [Production readiness review](deployment/PRODUCTION_READINESS.md): what was hardened for production and the release gates.

### Scanning and measurement

- [Calibration](scanning/CALIBRATION.md): scanner calibration requirements and packages.
- [Dual camera concatenation](scanning/DUAL_CAMERA_CONCATENATION.md): head/tail image registration and concatenation.
- [Rotation angles and scan geometry](scanning/ROTATION_ANGLES.md): how frame angles, the centreline and positions are worked out.
- [Direct camera and operator view](scanning/DIRECT_CAMERA_AND_OPERATOR_VIEW_IMPLEMENTATION.md): direct Sapera initialization and the lathe-operator screen.

### Running a scan

- [Scan cycle](operation/SCAN_CYCLE.md): the order of a scan, which checks block the PLC write, send-then-save, and what happens when a scan fails.

### PLC

- [PLC integration](plc/PLC_INTEGRATION.md): handshake, payloads and the write/readback policy.
- [PLC tag payloads](plc/PLC_TAG_PAYLOADS.txt): the payload layouts.
- [XYRestricted consumer guide](plc/XYRESTRICTED_CONSUMER_GUIDE.md): the supplied restricted PLC library (vendor document).

### Design

- [Code map](design/CODE_MAP.md): what every source file does, the words used in the code, and where to look for common questions.
- [Architecture](design/ARCHITECTURE.md): system architecture and major components.
- [Optimization modes](design/OPTIMIZATION_MODES.md): optimization strategy behavior.
- [Implementation and validation report](design/IMPLEMENTATION_VALIDATION_REPORT.md): what was implemented and what must be validated on the machine.

### Development

- [Offline replay](development/OFFLINE_REPLAY.md): re-running a saved scan on the development computer, and how to build.

### Archive

Old change notes kept for history. They are not current guides.

- [Setup summary](archive/SETUP_SUMMARY.md)
- [Production configuration changes](archive/PRODUCTION_CONFIG_CHANGES.md)

## Current measurement flow

Every head/tail pair between PLC ScanStart and ScanComplete is captured (typically 85-100 pairs):

```text
head frames + tail frames
  -> pair by capture index
  -> crop/orient/register each pair
  -> concatenate each pair into one full-length image
  -> the concatenated frames enter the processing pipeline
  -> frame angles are assigned from one measured revolution
```

The live combined preview uses the same crop, orientation, overlap, vertical-offset, camera-side, and equal-height rules as the saved processing images. See [Dual camera concatenation](scanning/DUAL_CAMERA_CONCATENATION.md) and [Rotation angles](scanning/ROTATION_ANGLES.md).

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

See [Production deployment](deployment/PRODUCTION_DEPLOYMENT.md) for the full process.

## First-run production setup

The main operator form includes **Setup / Calibration** and opens it automatically when required setup or calibration evidence is missing.

The wizard can:

- create or upgrade the local OptimizationXY SQL Express database when database saving is enabled;
- discover Sapera acquisition cameras;
- assign the physical Head / Dog and Tail cameras;
- save the Head/Tail assignment by Sapera server name and device index (each camera's serial number is read when it connects and recorded in its configuration snapshot);
- reconnect the final head/tail cameras;
- open separate head/tail previews for physical-side confirmation;
- configure and verify camera crop, orientation, vertical registration, overlap, and seam;
- verify the laser line with the production preprocessing path;
- capture a concatenated image and calculate axial pixels/mm from two clicked marks;
- capture known round references and build the scanner lookup calibration;
- stage, hash, validate, archive, activate, and roll back calibration packages.

See [Production setup wizard](deployment/PRODUCTION_SETUP_WIZARD.md) and [Calibration](scanning/CALIBRATION.md).

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

Before enabling Production mode, complete the required camera, calibration, machine-direction, travel-limit, clearance, guarding, interlock, emergency-stop, and controlled-scan checks described in the [Commissioning checklist](deployment/COMMISSIONING_CHECKLIST.md) and [Safety and interlocks](deployment/SAFETY_AND_INTERLOCKS.md).
