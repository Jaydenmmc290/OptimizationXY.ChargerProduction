# Production Deployment — ClickOnce

ClickOnce is the supported production deployment method for OptimizationXY.Charger.

The old ZIP-based workflow that used `scripts\publish-production.ps1` and `install-production.ps1` is no longer the production deployment procedure. Those scripts may remain in the repository for legacy engineering reference, but operators and maintainers should not use them to install or update the production application.

## 1. Deployment model

The production flow is:

```text
Development computer
  -> build and test the current source
  -> Visual Studio ClickOnce Publish
  -> approved ClickOnce deployment location
  -> production computer installs once
  -> future published versions are delivered as ClickOnce updates
```

Do not copy the source-code repository to the production computer.

Do not create or extract a production ZIP for normal deployment.

Do not manually copy application binaries into `C:\Program Files`.

ClickOnce manages the installed application files for the Windows account that installed the application.

## 2. Production-computer prerequisites

Before the first ClickOnce installation, confirm that the production computer has the machine-level prerequisites required by the application and hardware.

Required or conditionally required items include:

- 64-bit Windows;
- the required .NET Desktop Runtime when it is not installed automatically as a ClickOnce prerequisite;
- the compatible 64-bit Teledyne DALSA Sapera LT runtime for the Genie Nano cameras;
- required production PLC runtime/licensing and restricted runtime dependencies;
- SQL Server Express when database saving is enabled;
- correctly configured camera network interfaces;
- both physical cameras visible through the installed Sapera tools;
- the intended operator account available on the production computer;
- the second operator monitor when the machine uses one.

The application project currently targets `net10.0-windows`, `win-x64`, and is not self-contained, so the required .NET runtime must be available on the production computer unless the ClickOnce prerequisite configuration installs it.

## 3. Use the correct Windows account

ClickOnce installation is per-user.

Install the application while signed in as the same Windows account that will normally run OptimizationXY.Charger on the production computer.

Do not install it under an administrator account and then expect a different operator account to automatically have the same ClickOnce installation.

Machine-level prerequisites such as Sapera, SQL Server Express, device/network configuration, and licensed runtime components should still be installed/configured with the appropriate administrative permissions.

## 4. Publish from Visual Studio

On the development computer:

1. Pull or open the current approved source revision.
2. Open `OptimizationXY.Charger.slnx` in Visual Studio.
3. Build the project in the production hardware configuration.
4. Resolve build errors before publishing.
5. Run the engineering tests and machine-safe validation appropriate for the change.
6. Right-click the `OptimizationXY.Charger` project and open **Publish**.
7. Select the existing **ClickOnce** publish profile used for production.
8. Confirm that the profile points to the approved production ClickOnce deployment location.
9. Confirm that ClickOnce updates are enabled.
10. Confirm that the publish version will increase for the new release.
11. Select **Publish**.

The exact ClickOnce publish/install location is maintained by the Visual Studio publish profile used on the development computer. The repository currently does not contain a committed publish-profile file, so do not replace a known working production location with a guessed path from this document.

## 5. Publish-folder and installation-location rules

Keep the production ClickOnce installation location stable between releases.

If Visual Studio publishes directly to the approved network deployment folder, no separate ZIP or installer-copy step is required.

If the working Visual Studio profile publishes to a local staging folder because direct network publishing is not permitted, publish locally and then copy the complete ClickOnce publish output to the approved network installation folder as one release set. Do not copy only the `.exe` or only selected files.

A ClickOnce publish set normally includes items such as:

```text
setup.exe
OptimizationXY.Charger.application
Application Files\
```

The exact generated contents are controlled by Visual Studio and the ClickOnce profile.

Do not manually edit files inside `Application Files` after publishing.

## 6. First installation on the production computer

After the production ClickOnce deployment location contains the approved published version:

1. Sign in to the production computer using the normal OptimizationXY.Charger runtime/operator account.
2. Open the approved ClickOnce deployment location.
3. Run `setup.exe` when it is present and is the approved installation entry point.
4. Complete the ClickOnce installation prompts.
5. Start OptimizationXY.Charger from the installed Start-menu/Windows application shortcut.
6. Confirm the application starts in the expected commissioning state for a new machine setup.

If Windows presents a publisher or security warning, verify that the application came from the approved deployment location before continuing.

Do not run the application directly from a copied build folder as a substitute for the ClickOnce installation.

## 7. First-run commissioning

The first ClickOnce installation does not automatically make the machine production-ready.

Use the in-program **Production Setup / Calibration** workflow and the commissioning documentation to complete the required machine setup.

At minimum, the commissioning process includes:

- SQL setup when database saving is enabled;
- discovery and assignment of the physical Head / Dog and Tail cameras;
- physical camera-side verification;
- final camera crop, orientation, vertical registration, overlap, and seam setup;
- laser verification;
- axial length-scale calibration;
- known-reference scanner calibration;
- controlled dry scans;
- machine-direction, travel-limit, clearance, guarding, interlock, emergency-stop, and restart verification;
- responsible-person approval before enabling unrestricted Production mode.

See:

- [`START_HERE.md`](../getting-started/START_HERE.md)
- [`PRODUCTION_SETUP_WIZARD.md`](PRODUCTION_SETUP_WIZARD.md)
- [`CALIBRATION.md`](../scanning/CALIBRATION.md)
- [`COMMISSIONING_CHECKLIST.md`](COMMISSIONING_CHECKLIST.md)
- [`SAFETY_AND_INTERLOCKS.md`](SAFETY_AND_INTERLOCKS.md)

## 8. Normal software updates

After the first ClickOnce installation, normal software updates do not require reinstalling the application from a ZIP.

For each approved software change:

1. Make the source change on the development computer.
2. Build and test the change.
3. Open the existing Visual Studio ClickOnce publish profile.
4. Publish the next version to the same approved ClickOnce deployment location.
5. Confirm the new deployment files are present at that location.
6. Close OptimizationXY.Charger on the production computer if it is currently running.
7. Start the installed application normally.
8. Allow the ClickOnce update check to detect and install the newer published version according to the configured update policy.
9. Confirm the application version and perform the appropriate post-update validation before returning the machine to normal production.

Do not copy the Git repository to the production computer for an update.

Do not overwrite the ClickOnce-managed installation directory manually.

Do not run `install-production.ps1` for a normal ClickOnce update.

## 9. Visual Studio update settings

The production ClickOnce profile should be configured so installed clients use the same approved deployment location for updates.

For the normal production behavior where the application updates when it is opened, configure the ClickOnce profile to check for updates at application startup or before the application starts, as appropriate for the approved profile.

When changing ClickOnce settings, verify all of the following before publishing:

- installation/update location is correct;
- updates are enabled;
- the new publish version is greater than the installed version;
- the production application files are included;
- required prerequisites are configured correctly;
- signing/publisher settings are the expected production settings.

Do not create a second unrelated ClickOnce profile for the same production machine unless the deployment location is intentionally being migrated.

## 10. What must not be stored inside the ClickOnce installation

ClickOnce controls the application installation files and can replace them during an update.

Writable production data must remain outside the ClickOnce-managed application directory.

The application stores runtime data under its configured data root, including items such as:

```text
Config\
Config\Calibration\
Config\Setup\
Logs\
SavedScans\
```

Do not use the ClickOnce application folder as the permanent storage location for calibration packages, logs, scan records, or machine configuration that must survive application updates.

## 11. Post-update validation

Every software update should be treated according to the risk of the change.

For a documentation-only or non-hardware change, a basic application launch/version check may be sufficient.

For changes that affect cameras, acquisition, calibration, geometry, optimization, persistence, machine commands, safety gating, or other production behavior, perform the corresponding controlled validation before normal machine operation.

At minimum after a production software update:

1. Confirm the ClickOnce version updated successfully.
2. Confirm the application opens without a deployment or dependency error.
3. Confirm the expected runtime settings and active calibration are still available.
4. Confirm both cameras reconnect correctly when the change could affect acquisition.
5. Confirm the operator display is functioning.
6. Perform a controlled scan when the change can affect scan or production results.
7. Resolve any production-readiness block before normal operation.

## 12. Rollback strategy

Do not rely on manually replacing ClickOnce-managed binaries with files from an older release.

For a controlled rollback, use the previously approved source/build and publish that known-good application again as a new ClickOnce publish version that is higher than the currently deployed version.

Keep enough release history to identify the source revision and application version of the last known-good production build.

Do not delete the only known-good deployment/version record until the replacement version has passed production validation.

## 13. Legacy deployment scripts

The repository still contains older deployment scripts and documentation history from the previous ZIP-based deployment method.

For the current production process:

```text
ClickOnce = supported deployment/update method
ZIP + install-production.ps1 = legacy deployment method
```

Do not use the legacy ZIP installer instructions unless the team intentionally decides to abandon ClickOnce and formally restores that deployment method.

## 14. Quick production update summary

After ClickOnce has been installed once on the production computer, the normal update workflow is:

```text
Edit code
  -> Build and test
  -> Visual Studio Publish
  -> New ClickOnce version reaches the approved deployment location
  -> Start the installed production application
  -> ClickOnce installs the newer version
  -> Validate the change
```

That is the supported production deployment path for this project.
