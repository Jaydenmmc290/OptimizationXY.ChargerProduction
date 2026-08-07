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
4. In Production Setup, configure each camera's Sapera server, device index, expected physical serial number, and   acquisition mode.
5. Reconnect both cameras, open both previews, and physically confirm Head/Dog versus Tail.
6. Configure and verify Head/Tail crop, orientation, vertical offset, overlap, and camera seam.
7. Verify the laser using the finalized combined image.
8. Complete axial length scale, known-radius calibration, and independent validation when available.
9. Verify the operator form on the lathe monitor and test good, warning, and failed scans.
10. Verify PLC ScanStart/ScanComplete behavior, XY signs, limits, readback, knife clearance, guarding, and interlocks.
11. Run `validate-production-host.ps1`.
12. Record approval, then enable Production mode.

See `PRODUCTION_SETUP_WIZARD.md`, `PRODUCTION_DEPLOYMENT.md`, `COMMISSIONING_CHECKLIST.md`, and `IMPLEMENTATION_VALIDATION_REPORT.md` for the complete procedure and remaining machine-acceptance requirements.
