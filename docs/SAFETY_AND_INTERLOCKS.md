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
safe carriage retract radius = largest measured radius + clearance
```

The settings validator rejects a configured multiplier below 10, and the runtime calculation still clamps it to at least 10 for defense in depth.

Example with the default 3.2 mm veneer:

```text
10 × 3.2 mm = 32.0 mm minimum clearance
```

An extra buffer can be added only after measuring machine uncertainty, stopping distance, backlash, scanner error, fixture geometry, and thermal/mechanical tolerances.

## Fail-closed software gates

A production PLC write is blocked unless all required checks pass, including:

- Operating mode is `Production`.
- `InscribedGeomotry` mode and direct virtual-peel refinement are enabled.
- All physical commissioning flags and approval fields are complete.
- Both cameras use encoder acquisition.
- Exactly 36 valid concatenated frames are present.
- Dropped-frame count is within the approved limit.
- A good `ScanComplete` follows the active `ScanStart` cycle.
- Calibration is loaded and valid.
- Minimum point-cloud, centerline, and radius-field coverage is met.
- Final XY values are finite and inside hard travel limits.
- Required knife clearance is at least 10 peel thicknesses.
- Results have been saved before transmission.
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
