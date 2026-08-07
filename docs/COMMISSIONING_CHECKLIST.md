# XY Charger Production Commissioning Checklist

## Software and host

- [ ] Production package was built `win-x64` with Sapera support required.
- [ ] `OptimizationXY.Charger.exe`, Sapera assembly, PLC libraries, NLog files, and native dependencies are installed.
- [ ] Settings schema is 10 or newer.
- [ ] Production runtime account can write the configured data root but not application binaries.
- [ ] SQL Express schema/test passes when database saving is enabled.
- [ ] Host validation script has no BLOCK results.

## Cameras, seam, and laser

* [ ] Head camera uses the correct Sapera server and resource index.
* [ ] Tail camera uses the correct Sapera server and resource index.
* [ ] Head and Tail expected serial numbers are populated, distinct, and match the physical cameras.
* [ ] Both cameras use `Programmatic` initialization.
* [ ] Both cameras use Encoder mode and the verified wired trigger configuration.
* [ ] Head preview physically shows the Head/Dog end.
* [ ] Tail preview physically shows the Tail end.
* [ ] Head/Tail image order is correct.
* [ ] Head horizontal reversal is correct.
* [ ] Tail horizontal reversal is correct.
* [ ] Head crop values are verified.
* [ ] Tail crop values are verified.
* [ ] Tail vertical offset is verified.
* [ ] Head/Tail overlap is verified.
* [ ] A common physical feature crosses the camera seam continuously.
* [ ] No physical region is duplicated at the camera seam.
* [ ] No physical region is missing at the camera seam.
* [ ] Camera seam verification is saved in Production settings.
* [ ] Laser stripe is visible, stable, and unsaturated across the usable combined field.
* [ ] Camera and laser mounts are secured and marked against movement.

## Scanner calibration

* [ ] Camera seam was finalized before dimensional calibration.
* [ ] Axial pixels-per-millimeter scale was measured with a traceable reference.
* [ ] At least two different known radius bands were captured.
* [ ] Repeatability and independent validation passed as required.
* [ ] Active calibration ID matches runtime settings.
* [ ] Camera configuration snapshots match the active calibration after connection.
* [ ] Crop, orientation, vertical offset, seam, overlap, and image dimensions match the active calibration.


## Scanner calibration

- [ ] Axial pixels-per-millimeter scale was measured with a traceable reference.
- [ ] At least two different known radius bands were captured.
- [ ] Repeatability and independent validation passed.
- [ ] Active calibration ID matches runtime settings.
- [ ] Camera configuration snapshots match the active calibration after connection.
- [ ] Crop, orientation, seam, overlap, and image dimensions match calibration.

## Operator scan-quality display

- [ ] Operator form opens on the intended lathe monitor.
- [ ] HEAD / DOG is shown on the correct side and TAIL on the other.
- [ ] A known good scan displays a continuous profile and SCAN GOOD.
- [ ] A deliberately obstructed/missing region is highlighted red.
- [ ] A failed scan displays RESCAN BLOCK and blocks production PLC delivery.
- [ ] Frame, coverage, valid-slice, length, diameter, and largest-gap values are reasonable.
- [ ] PLC connection, ScanStart, and ScanComplete indicators follow the actual tags.

## PLC and machine safety

- [ ] PLC ScanStart rising edge starts exactly one capture cycle.
- [ ] Exactly 36 synchronized head/tail pairs are captured at the verified positions.
- [ ] Processing waits for a good ScanComplete signal.
- [ ] No software component writes ScanStart or ScanComplete.
- [ ] Head X/Y and tail X/Y signs match physical movement.
- [ ] Travel limits and warning limits are verified at the machine.
- [ ] Carriage retract/knife clearance is verified before movement data is acted upon.
- [ ] PLC readback verification passes for every permitted write.
- [ ] Guarding, interlocks, emergency stop, and safe restart behavior are tested by authorized personnel.

## Release approval

- [ ] At least one complete dry run was saved with calibration, camera snapshots, settings, software identity, result, readiness report, and SHA-256 manifest.
- [ ] A failed scan was proven to block PLC command delivery.
- [ ] Backup and rollback procedures were tested.
- [ ] Responsible person and timestamp are recorded in Production settings.
- [ ] Production mode was enabled only after every required check passed.
