# Start Here

This release uses settings schema 10, a separate lathe-operator scan-quality screen, and direct Sapera camera initialization without loading a CCF in normal production operation.

## Build computer

1. Install Visual Studio/.NET SDK and the 64-bit Sapera LT SDK.
2. Set `SAPERA_SDK_DIR` to the Sapera `.NET\Bin` folder if it is not in the default location.
3. Run the release build and production publish scripts.
4. Do not pre-enable Production mode or commissioning approval in the template.

## Production computer

1. Install the 64-bit Sapera runtime and verify both cameras appear in Sapera Configuration/CamExpert.
2. Install the application with `install-production.ps1`.
3. Start the application in Commissioning mode.
4. In Production Setup, enter each camera's Sapera server, resource index, Mono8, exposure, gain, Encoder mode, and trigger source. Leave serial blank only for the first controlled connection so the wizard can learn and lock the physical assignment.
5. Reconnect, show both previews, and physically confirm head/dog versus tail.
6. Complete laser verification, axial scale, known-radius calibration, and independent validation.
7. Verify the operator form on the lathe monitor and test good, warning, and failed scans.
8. Verify PLC ScanStart/ScanComplete behavior, XY signs, limits, readback, knife clearance, guarding, and interlocks.
9. Run `validate-production-host.ps1`.
10. Record approval, then enable Production mode.

See `PRODUCTION_SETUP_WIZARD.md`, `PRODUCTION_DEPLOYMENT.md`, `COMMISSIONING_CHECKLIST.md`, and `IMPLEMENTATION_VALIDATION_REPORT.md` for the complete procedure and remaining machine-acceptance requirements.
