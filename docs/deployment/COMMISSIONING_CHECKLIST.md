# XY Charger Production Commissioning Checklist

## Software and host

- [ ] Application was installed through the approved ClickOnce deployment location using the normal production Windows account.
- [ ] The installed ClickOnce version matches the approved production release.
- [ ] Required machine-level prerequisites are installed, including the compatible 64-bit Sapera runtime and licensed PLC runtime dependencies.
- [ ] Settings schema is 10 or newer.
- [ ] Writable runtime data is stored outside the ClickOnce-managed application directory.
- [ ] SQL Express schema/test passes when database saving is enabled.
- [ ] Application starts without a ClickOnce deployment or dependency error.

## Cameras, seam, and laser

- [ ] Both physical cameras appear in automatic Sapera discovery.
- [ ] The correct physical camera is assigned to Head / Dog.
- [ ] The correct physical camera is assigned to Tail.
- [ ] After connecting, each camera's configuration snapshot shows its serial number (`Identity.DeviceSerialNumber`).
- [ ] The Head and Tail serial numbers are populated and different from each other.
- [ ] Camera assignments resolve successfully after a reconnect.
- [ ] After a reconnect or reboot each camera is still found at its assigned Sapera server name and device index. If a server name changes, reassign in Production Setup; this invalidates the calibration.
- [ ] Both camera previews can be hidden and reopened without a Sapera resource-lock error.
- [ ] Both cameras use `Programmatic` initialization.
- [ ] Both cameras use Encoder mode and the verified wired trigger configuration.
- [ ] Head preview physically shows the Head / Dog end.
- [ ] Tail preview physically shows the Tail end.
- [ ] Head/Tail image order is correct.
- [ ] Head horizontal reversal is correct.
- [ ] Tail horizontal reversal is correct.
- [ ] Head crop values are verified.
- [ ] Tail crop values are verified.
- [ ] Tail vertical offset is verified.
- [ ] Head/Tail overlap is verified.
- [ ] A common physical feature crosses the camera seam continuously.
- [ ] No physical region is duplicated at the camera seam.
- [ ] No physical region is missing at the camera seam.
- [ ] Camera seam verification is saved in Production settings.
- [ ] Laser stripe is visible, stable, and unsaturated across the usable combined field.
- [ ] Camera and laser mounts are secured and marked against movement.

## Scanner calibration

- [ ] Camera seam was finalized before dimensional calibration.
- [ ] Axial pixels-per-millimeter scale was measured with a traceable reference.
- [ ] At least two different known radius bands were captured.
- [ ] Repeatability and independent validation passed as required.
- [ ] Active calibration ID matches runtime settings.
- [ ] Camera configuration snapshots match the active calibration after connection.
- [ ] Crop, orientation, vertical offset, seam, overlap, and image dimensions match the active calibration.

## Operator scan-quality display

- [ ] Operator form opens on the intended lathe monitor.
- [ ] HEAD / DOG is shown on the correct side and TAIL on the other.
- [ ] A known good scan displays a continuous profile and SCAN GOOD.
- [ ] A deliberately obstructed or missing region is highlighted red.
- [ ] A failed scan displays RESCAN BLOCK and blocks production PLC delivery.
- [ ] Frame, coverage, valid-slice, length, diameter, and largest-gap values are reasonable.
- [ ] PLC connection, ScanStart, and ScanComplete indicators follow the actual tags.

## PLC and machine safety

- [ ] PLC ScanStart rising edge starts exactly one capture cycle.
- [ ] Head and tail capture the same number of frames from ScanStart to ScanComplete, and the readiness report shows `Rotation coverage` as PASS.
- [ ] Processing waits for a good ScanComplete signal.
- [ ] No software component writes ScanStart or ScanComplete.
- [ ] Head X/Y and tail X/Y signs match physical movement.
- [ ] Travel limits and warning limits are verified at the machine.
- [ ] Carriage retract/knife clearance is verified before movement data is acted upon.
- [ ] PLC readback verification passes for every permitted write.
- [ ] Guarding, interlocks, emergency stop, and safe restart behavior are tested by authorized personnel.

## ClickOnce update verification

For any software update that can affect production behavior:

- [ ] New version was published using the same approved ClickOnce profile and deployment location.
- [ ] Production computer detected and installed the new ClickOnce version.
- [ ] Runtime settings and active calibration remained available after the update.
- [ ] Cameras reconnect correctly when the change could affect acquisition.
- [ ] A controlled scan was completed when the change could affect scan results or machine output.
- [ ] Any production-readiness block was resolved before returning to normal operation.

## Release approval

- [ ] At least one complete dry run was saved with calibration, camera snapshots, settings, software identity, result, readiness report, and SHA-256 manifest.
- [ ] A failed scan was proven to block PLC command delivery.
- [ ] A known-good software version/source revision is recorded for rollback purposes.
- [ ] Responsible person and timestamp are recorded in Production settings.
- [ ] Production mode was enabled only after every required check passed.
