# Restricted PLC Integration

## Supplied interface

The application references `Arbor.Common.PLC.XYRestricted` and creates one `XY` instance for the application lifetime. The restricted public interface exposes five tags only:

| Enum | PLC tag | Application access |
|---|---|---|
| `ScanStart` | `TruLog_R_ScanStart` | read only |
| `ScanComplete` | `TruLog_R_ScanComplete` | read only |
| `ScannedBlockData` | `TruLog_W_ScannedBlockData` | read/write |
| `CarriageRetract` | `TruLog_W_CarriageRetract` | read/write |
| `RoundupComplete` | `TruLog_W_RoundupComplete` | read/write |

The library targets PLC device path `172.16.60.2` on the `172.16.0.0/16` network.

## Input handshake

1. On startup, the first `ScanStart` value establishes a baseline only. A stale high value does not start acquisition.
2. A fresh good-quality low-to-high `ScanStart` edge opens a new scan cycle.
3. In Production mode, manual start and manual early-stop are disabled.
4. Both cameras capture frames until the PLC reports `ScanComplete`.
5. `ScanComplete` stops frame intake (a few more frames per camera are accepted, `RotatingScan.FramesAcceptedAfterScanComplete`) and processing starts. In Production mode a good `ScanComplete` from the active PLC cycle is required.
6. Timeout, bad quality, missing active cycle, or PLC disconnect blocks processing and result transmission. The scan is reported as failed and the application is ready for the next `ScanStart` (see [`SCAN_CYCLE.md`](../operation/SCAN_CYCLE.md)).

The application never writes `ScanStart` or `ScanComplete`.

## Output payloads

All dimensional values are sent in inches (the application calculates in millimeters and converts when the payload is built). The final XY values are offsets relative to the commissioned home convention. `blockCount` is a persistent sequence number kept below 16,000,000 so it remains exactly representable in a PLC `REAL`/single-precision field.

### `ScannedBlockData` — 10 floats

```text
[0] Head X offset
[1] Head Y offset
[2] Tail X offset
[3] Tail Y offset
[4] Small diameter
[5] Large diameter
[6] Safe carriage-retract radius
[7] Smallest measured radius
[8] Reserved = 0
[9] Block count
```

### `CarriageRetract` — 5 floats

```text
[0] Safe carriage-retract radius
[1] Reserved = 0
[2] Reserved = 0
[3] Reserved = 0
[4] Block count
```

### `RoundupComplete` — 5 floats

```text
[0] Smallest measured radius
[1] Reserved = 0
[2] Reserved = 0
[3] Reserved = 0
[4] Block count
```

The safe carriage-retract radius is:

```text
the larger of: the block's largest distance from the CURRENT spindle axis
           and its largest distance from the COMMANDED spindle axis
+ max(veneer thickness × max(10, Safety.KnifeClearancePeelThicknessMultiplier),
      Safety.MinimumKnifeClearanceMM)
+ Safety.AdditionalCarriageRetractBufferMM
```

Using both axes means the retract radius covers the block as it actually swings after the
move and is never smaller than before the move (see `PlcPayloadBuilder`).

## Write/readback policy

The production policy requires:

- PLC connected.
- Good tag quality.
- Supporting radius arrays written before `ScannedBlockData`.
- Every array requested back through `ReadTag`.
- Exact array length and each float within configured tolerance.
- Timeout or mismatch reported as a failed transmission.

Readback confirms the command values stored in the exposed tags; it does **not** prove physical movement occurred.

## No rotation command

There is no rotation field in `PlcCommandPayload`, no rotation tag in the service, and no rotation value in any output array. Rotation remains exclusively PLC-controlled.

## Current feedback limitation

The supplied restricted library does not expose approved tags for actual head/tail position, knife position, peel diameter, encoder count, motion complete, or PLC acceptance. The software stores command-readback status, but true closed-loop actual-versus-commanded validation requires a future approved DLL/tag surface from the PLC owner.

Recommended future read-only tags:

```text
ActualHeadX, ActualHeadY, ActualTailX, ActualTailY
KnifePosition
EncoderCount
MotionComplete
ResultAccepted / ResultRejected reason
ActualPeelDiameter
```

Do not bypass `XYRestricted.dll` with unrestricted hand-typed PLC tags. Extend the approved restricted library and repeat the controls/safety review.
