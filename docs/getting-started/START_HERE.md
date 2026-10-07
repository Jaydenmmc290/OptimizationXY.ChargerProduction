# Start Here

This release uses settings schema 10, a separate lathe-operator scan-quality screen, direct Sapera camera initialization without loading a CCF in normal production operation, and ClickOnce as the supported production deployment method.

## Development computer

1. Install Visual Studio/.NET SDK and the compatible 64-bit Sapera LT SDK.
2. Build and test the current approved source revision before publishing.
3. Open the existing Visual Studio ClickOnce publish profile for `OptimizationXY.Charger`.
4. Confirm that the profile points to the approved production ClickOnce deployment location.
5. Confirm that ClickOnce updates are enabled and the publish version will increase.
6. Publish the new version.
7. Do not use the old ZIP plus `install-production.ps1` workflow for normal production deployment.
8. Do not pre-enable Production mode or commissioning approval in a new-machine template.

See [`PRODUCTION_DEPLOYMENT.md`](../deployment/PRODUCTION_DEPLOYMENT.md) for the complete ClickOnce installation and update procedure.

## Production computer

1. Install the required 64-bit Sapera runtime and verify both cameras appear in Sapera Configuration/CamExpert.
2. Install the application from the approved ClickOnce deployment location while signed in as the Windows account that will normally run OptimizationXY.Charger.
3. For the first installation, use the approved ClickOnce entry point, normally `setup.exe` when it is present.
4. Start the installed application normally from Windows.
5. Confirm the application is in Commissioning mode for initial machine setup.
6. In Production Setup, refresh the discovered Sapera cameras.
7. Assign one discovered physical camera to Head / Dog and one to Tail.
8. Save the assignments and reconnect. The application learns each serial number directly from the camera. The assignment itself is saved as the Sapera server name and device index.
9. Open both previews and physically verify Head / Dog versus Tail.
10. Close and reopen each preview to verify that preview visibility does not affect camera acquisition.
11. Configure and verify Head/Tail crop, orientation, vertical offset, overlap, and camera seam.
12. Verify the laser using the finalized combined image.
13. Complete axial length scale, known-radius calibration, and independent validation when available.
14. Verify the operator form on the lathe monitor and test good, warning, and failed scans.
15. Complete the required machine commissioning and safety validation before enabling Production mode.
16. Record responsible-person approval, then enable Production mode only after the required release gates pass.

## Future software updates

After ClickOnce is installed once on the production computer:

1. Make the software change on the development computer.
2. Build and test it.
3. Publish the next version using the same approved Visual Studio ClickOnce profile.
4. Start the installed production application.
5. Allow ClickOnce to detect and install the new version according to the configured update policy.
6. Perform the post-update validation appropriate for the change.

Do not copy the source repository to the production computer and do not manually replace ClickOnce-managed application binaries.

See [`PRODUCTION_SETUP_WIZARD.md`](../deployment/PRODUCTION_SETUP_WIZARD.md), [`PRODUCTION_DEPLOYMENT.md`](../deployment/PRODUCTION_DEPLOYMENT.md), [`CALIBRATION.md`](../scanning/CALIBRATION.md), [`COMMISSIONING_CHECKLIST.md`](../deployment/COMMISSIONING_CHECKLIST.md), and [`IMPLEMENTATION_VALIDATION_REPORT.md`](../design/IMPLEMENTATION_VALIDATION_REPORT.md) for the complete setup and machine-acceptance requirements.
