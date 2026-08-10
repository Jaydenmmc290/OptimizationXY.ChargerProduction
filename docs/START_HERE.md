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
4. In Production Setup, refresh the discovered Sapera cameras.
5. Assign one discovered physical camera to Head / Dog and one to Tail.
6. Save the assignments and reconnect. The application learns each serial number directly from the camera.
7. Open both previews and physically verify Head / Dog versus Tail.
8. Close and reopen each preview to verify that preview visibility does not affect camera acquisition.
9. Configure and verify Head/Tail crop, orientation, vertical offset, overlap, and camera seam.
10. Verify the laser using the finalized combined image.
11. Complete axial length scale, known-radius calibration, and independent validation when available.
12. Verify the operator form on the lathe monitor and test good, warning, and failed scans.
13. Verify PLC ScanStart/ScanComplete behavior, XY signs, limits, readback, knife clearance, guarding, and interlocks.
14. Run `validate-production-host.ps1`.
15. Record approval, then enable Production mode.

See `PRODUCTION_SETUP_WIZARD.md`, `PRODUCTION_DEPLOYMENT.md`, `COMMISSIONING_CHECKLIST.md`, and `IMPLEMENTATION_VALIDATION_REPORT.md` for the complete procedure and remaining machine-acceptance requirements.
