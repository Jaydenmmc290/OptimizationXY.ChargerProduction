# Safety and Interlock Design

## Safety boundary

This Windows application is an optimization and command-data producer. It is **not** a safety-rated controller and must not be used as the sole protection against hazardous motion, knife contact, crushing, entanglement, or unexpected startup.

Safety functions belong in the machine's safety-rated hardware/PLC architecture. The standard PLC program must independently decide whether movement is permitted.

## Ten-peel-thickness knife clearance

The requested minimum separation is implemented as a fail-safe lower bound:

```text
effective multiplier = max(10.0, configured multiplier)
clearance = max(minimum absolute clearance,
                veneer thickness × effective multiplier)
            + additional commissioned carriage buffer
safe carriage retract radius = clearance + the larger of
    the block's largest distance from the current spindle axis and
    its largest distance from the commanded spindle axis
```

Measuring from both axes means the retract radius covers the block as it actually swings
after the move, and it is never smaller than it would have been without the move.

The settings validator rejects a configured multiplier below 10, and the runtime calculation still clamps it to at least 10 for defense in depth.

Example with the default 3.2 mm veneer:

```text
10 × 3.2 mm = 32.0 mm minimum clearance
```

An extra buffer can be added only after measuring machine uncertainty, stopping distance, backlash, scanner error, fixture geometry, and thermal/mechanical tolerances.

## Fail-closed software gates

All of the checks below run on every scan and are written to the readiness report.

By default (`Production.EnforceOnlyEssentialReadinessChecks = true`) only these can block a production PLC write: Production mode, PLC connected, operator scan-quality gate, PLC payload built, final recommendation allowed, finite XY values, hard travel limits, knife clearance, carriage-retract payload, PLC write order, and reserved payload fields zero. Any other check that fails is logged as NOT ENFORCED and does not stop the write. Set the setting to `false` to make every check blocking. See [`SCAN_CYCLE.md`](../operation/SCAN_CYCLE.md).

The checks:

- Operating mode is `Production`.
- Optimization mode is `CompareInscribedGeometry` (not enforced by default).
- All physical commissioning flags and approval fields are complete.
- Both cameras use encoder acquisition.
- Frame count: exactly `Acquisition.TargetFramePairsPerLog` when `Production.RequireExactFrameCount` is true, otherwise at least `RotatingScan.MinimumValidFrames` (not enforced by default).
- Dropped-frame count is within the approved limit.
- A good `ScanComplete` follows the active `ScanStart` cycle.
- Calibration is loaded and valid.
- Minimum point-cloud, centerline, and radius-field coverage is met.
- Final XY values are finite and inside hard travel limits.
- Required knife clearance is at least 10 peel thicknesses.
- Saved record: by default the result is sent first and saved immediately afterwards (`Plc.SendResultsBeforeSave`). Save-before-send applies only when `EnforceOnlyEssentialReadinessChecks` is false.
- PLC is connected.
- PLC policy requires good-quality verified readback.
- Restricted output payload is valid.

In Production mode, manual capture start, manual early-stop, and optimization-mode changes are disabled.

## Required PLC-side checks

The PLC must independently enforce at least:

- Machine-ready and automatic-mode state.
- Guard/gate/light-curtain/safety relay status.
- E-stop chain status.
- Knife and carriage safe positions.
- Charger/spindle/dog mechanical state.
- Hydraulic/pneumatic pressure and motion permissives.
- Hard X/Y bounds and rate-of-change limits.
- Valid payload array sizes, finite values, units, block count, and freshness.
- Safe sequencing so partial tag updates cannot authorize motion.
- Timeout/watchdog behavior and communication-loss safe state.
- Rotation ownership and angular sequence.

The PLC must reject a result when any check fails and should expose a read-only acceptance/rejection status in a future approved restricted interface.

## Recovery algorithm safety

The recovery optimizer may choose baseline, penalty-search, cylinder, oval, or direct recovery-refinement axis candidates. The direct search is bounded around established seeds and hard-limited by commissioned head/tail travel. It never commands rotation. Even when the oval strategy is selected, the virtual peel models a fixed axis with circular rotary-lathe motion; the program does not request elliptical knife or log motion.

Geometry and recovery selection do not override machine hard limits. If the best candidate exceeds a hard limit, the output is zeroed/blocked and the PLC packet is not sent.

## Residual risks requiring physical validation

The software cannot determine by code alone:

- Exact knife/arm/dog/spindle envelope.
- Mechanical deflection and backlash.
- Actual braking/stopping distance.
- Failure modes of hydraulic, pneumatic, electrical, and safety systems.
- Correct PLC interpretation of units and signs.
- Whether measured largest radius includes every possible obstruction.
- Runtime licensing compliance.

Those items remain release blockers until verified on the real machine and signed off under the plant's risk assessment and change-management process.
