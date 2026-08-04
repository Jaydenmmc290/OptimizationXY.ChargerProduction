# Production Readiness Review

## Production-control additions in version 5.0

- Added one lifetime `XYRestricted` connection and the exact five-tag restricted contract.
- Added fresh-edge `ScanStart` handling that suppresses stale high startup values.
- Added good-quality `ScanComplete` gating after all 36 synchronized pairs are present.
- Added readback verification, quality enforcement, tolerance checks, timeout handling, and fail-closed disconnected behavior for all writable result arrays.
- Added a persistent block sequence and exact PLC payload builders for spindle XY, diameters, retract radius, and roundup radius.
- Added a mandatory knife/carriage clearance of at least 10 peel thicknesses, with an optional separately commissioned extra buffer.
- Added recovery-optimized selection across baseline, penalty-search, cylinder, oval, and a bounded direct XY recovery-refinement candidate using full-sheet virtual-peel output.
- Added per-log production-readiness reports, capture audit, redacted settings snapshot, PLC payload/transmission record, feedback-availability record, exact calibration/direct-camera snapshots, software-build identity, and SHA-256 scan manifest. Database connection strings are not stored in audit files.
- Added production/commissioning modes. Production disables manual capture start, manual early-stop, and optimization-mode changes, and requires the direct recovery strategy.
- Added a named single-instance mutex and fail-closed persistent block-sequence validation. The block sequence never silently wraps; exhaustion requires an approved controls reset.
- Added production publish, install, and non-writing host-validation scripts plus detailed commissioning/safety/PLC documentation.
- Host validation now parses the calibration contents, rejects legacy estimated calibration, verifies the configured head/tail direct camera identities and settings, and confirms that the runtime data root is writable.
- A production result cannot pass readiness unless its saved folder contains the exact scanner calibration and both direct camera snapshots frozen from the live Sapera connections at capture start.
- Confirmed no application rotation command or rotation payload field exists.


## Completed in this pass

### Dual-camera acquisition

- Restored the required dual-camera concatenation boundary.
- Changed completed capture from 72 interleaved half-log images to 36 paired, full-length processing images.
- Added persisted crop, horizontal reversal, camera-side, overlap, and vertical-registration settings.
- Made the live combined preview use the same concatenation implementation and settings as the measurement path.
- Kept strict equal-height validation enabled by default so measurement registration problems fail loudly.
- Removed obsolete frame-group/raw-save settings that were not part of the active pipeline.

### Optimization modes

- Preserved the existing adaptive penalty-scored optimizer as an operator-selectable mode.
- Added a conservative largest-inscribed-cylinder search.
- Added a conservative largest-inscribed-oval axis-selection search.
- Added a compare mode that creates both physical rotary-lathe peels and chooses the final recommendation by usable full 50-inch sheet recovery.
- Kept defect and patch analysis active for reporting while excluding all defect/risk/penalty scores from inscribed-geometry spindle selection.
- Applied machine movement limits as hard feasibility constraints rather than penalty terms.
- Added separate saved baseline, penalty, cylinder, oval, direct-refinement, and selected peel images plus per-strategy sheet metrics.

### Build and dependency hygiene

- Removed developer-specific Sapera paths and added `SAPERA_SDK_DIR` / `SaperaSdkDir`.
- Added deterministic `UseSapera=false` builds with a non-hardware camera stub.
- Removed unused packages and inactive legacy code identified in the earlier cleanup.
- Added an x64 project target, pinned SDK, analyzers, NuGet audit, CI, Dependabot, and repeatable scripts.

### Runtime correctness and safety

- Moved settings, calibration, logs, and saved scans out of the installation directory.
- Added legacy settings/calibration migration and atomic JSON writes.
- Removed all automatic estimated calibration generation.
- Unified live and folder processing behind one ordered stage list.
- Reuses one calibration table per run rather than deserializing it for every frame.
- Separates raw measured point-cloud construction from display-only gap filling.
- Activates rotating file logging and global exception logging.
- Consolidates file and optional SQL persistence and preserves file saves when SQL fails.
- Leaves SQL persistence disabled with no development connection string.
- Writes CSV measurements with invariant culture.

## Required release gates

These require the real Windows, camera, lathe, PLC, and reference-data environment:

1. Run `./scripts/build.ps1 -SkipSapera` and a Release hardware build with the installed Sapera SDK; resolve every compiler/analyzer finding.
2. Confirm the Sapera assembly/API version matches `GenieNanoCameraService`.
3. Commission head/tail registration with a certified target. Tune crop, reversal, vertical offset, overlap, and camera side until the 36 concatenated images match the supplied 40.zip geometry and contain no duplicated or missing seam region.
4. Confirm frame-pair synchronization against the encoder: head frame N and tail frame N must represent the same physical rotation angle.
5. Verify the scanner XY-plane angle basis used by `RadiusFieldGenerator` against the installed camera/laser geometry, especially when the fitted centerline is tilted.
6. Validate inscribed-cylinder and inscribed-oval dimensions against certified circular and elliptical reference artifacts.
7. Compare both geometry-selected spindle axes and their virtual peels against approved machine measurements and actual veneer output.
8. Confirm the physical PLC sign convention and head/tail coordinate mapping before enabling automatic charger moves.
9. Run dual-camera soak tests covering dropped frames, unequal image dimensions, cancellation, reconnect, repeated logs, and shutdown.
10. Verify calibration provenance and compare dimensional output against certified reference artifacts.
11. Load-test scan retention and available disk space on the intended data drive.
12. Validate SQL schema, least-privilege credentials, encryption, timeout behavior, and rollback if database saving is enabled.
13. Run and retain approved regression tests for concatenation, calibration, geometry envelopes, peel-sheet classification, scoring, storage, settings, PLC handshake, payload readback, and all injected-fault cases.
14. Obtain an expanded approved restricted PLC tag surface if true actual-position/knife/encoder closed-loop feedback is required; the supplied DLL exposes command readback but not those machine signals.

## Validation performed in this rework

- Confirmed the supplied 40.zip reference contains 36 JPEG processing frames, each 962 × 480 pixels.
- Traced the capture ownership path from camera callbacks through paired concatenation and pipeline input.
- Checked XML and solution/project structure.
- Checked C# delimiter, string/comment, and preprocessor balance.
- Scanned for stale removed settings and obsolete 72-frame interleaving calls.
- Verified clean-archive integrity after removing `.vs`, `bin`, `obj`, and other local build artifacts.

A compiler-backed build could not be run in this execution environment because no .NET SDK was installed and the production Sapera SDK/hardware were unavailable. Static validation is not a substitute for the release gates above.
