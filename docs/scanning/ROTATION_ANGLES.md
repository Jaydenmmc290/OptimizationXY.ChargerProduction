# Rotation Angles and Scan Geometry

## Why this changed

The cameras deliver frames at a steady rate (about 65 per second) that is not tied to
log rotation. A capture normally contains frames of the resting log, one revolution,
and more frames of the resting log. Assigning `frame index x 10 degrees` therefore put
every frame at the wrong angle.

## How angles are assigned now

`RevolutionAngleAssigner` runs after the radius profiles are built:

1. The last frame that still matches the first frame is the 0 degree frame.
2. The first later frame whose profile returns to that starting profile is 360 degrees.
3. The frame slots in between are divided evenly over 360 degrees.
4. Resting frames before and after are excluded (`ExcludedFromRevolution`), kept in the
   saved scan for audit, and never used for geometry.
5. A frame that is invalid for another reason keeps its angle slot.

If no frame returns to the starting profile within `RevolutionClosureToleranceMM`, the frame
that comes closest is still used as the 360 degree frame when it is within
`ApproximateClosureToleranceMM`; the revolution is then reported as approximate, not verified.
If even that fails, the frames between rotation start and rotation stop are divided evenly over
360 degrees and the readiness report shows a `Rotation coverage` warning.

After the PLC reports ScanComplete only a few more frames per camera are accepted
(`FramesAcceptedAfterScanComplete`); the rest are the stationary block and are not captured.

## Settings (`RotatingScan`)

All have defaults, so an existing `appsettings.json` keeps working unchanged.

| Setting | Default | Meaning |
|---|---|---|
| `UseMeasuredRevolutionAngles` | `true` | `false` restores the legacy fixed step |
| `StationaryProfileChangeMM` | `0.8` | RMS profile change below this is a resting log |
| `RevolutionClosureToleranceMM` | `1.5` | RMS match needed to accept the 360 degree frame |
| `ApproximateClosureToleranceMM` | `2.5` | Closest return accepted as an approximate (unverified) revolution; `0` disables |
| `SubFrameRevolutionClosure` | `false` | `true` places the 360 degree point between frames (for example 84.6 frames) |
| `MinimumFramesPerRevolution` | `20` | Shortest revolution accepted |
| `MaximumCalibrationExtrapolationPixels` | `15` | Laser pixels a radius may be extrapolated past the largest measured reference; `0` disables |
| `FramesAcceptedAfterScanComplete` | `8` | Frames per camera still accepted after ScanComplete, so the frame that closes the revolution is not cut off |
| `ReverseRotationDirection` | `false` | Set after the off-centre reference test if Y is mirrored |
| `LaserAngleOffsetDeg` | `0` | Machine angle at which the laser strikes the log |
| `KeepAllStripeSegments` | `false` | `true` also keeps laser-line pieces that line up with the longest piece |
| `SubPixelStripeCenter` | `false` | `true` uses the brightness-weighted stripe centre (about 0.1 px) |

`SubFrameRevolutionClosure`, `KeepAllStripeSegments` and `SubPixelStripeCenter` are off by default. Compare them with `--replay-batch` before turning
them on (see [Offline replay](../development/OFFLINE_REPLAY.md)).

## Other geometry changes

- Z is measured from the tail end. With `HeadOnLeft = true` it is measured from the
  right-hand image edge, so Z = 0 is tail everywhere.
- Local centerline slices use a least-squares circle fit instead of the average of
  surface points.
- Inscribed-geometry samples are placed at the centre of each angular bin.

## Centreline and the machine origin

- **Shape analysis** (radius field, roundness, low-wood map, diameters) is measured from the
  block centreline: the PCA axis, refined so it passes through the circle-fit centre of each
  slice. The report prints both the raw PCA origin and the refined one.
- **Spindle moves** are measured from the machine origin, the current spindle axis at
  X = 0, Y = 0. The report prints where the block centreline sits relative to that origin at
  the tail and head ends, and the selected axis as an offset from the same origin.

## Repeatable positions (`InscribedGeometry.CenterlinePullWeight`)

The largest inscribed cylinder is set by a few contact points, so many axes give almost
the same diameter and small scan differences used to move the chosen position by many
millimetres. The search is now centred on the fitted centreline of the block (circle fit
per slice, straight line through the centres), and a candidate must gain
`CenterlinePullWeight` mm of radius for every mm it sits away from that centreline.

| Value | Behaviour |
|---|---|
| `0` | Old behaviour: largest cylinder anywhere, search centred on the current spindle axis |
| `0.5` (default) | Same diameter as the old search on the test block, about a quarter of the position scatter |
| `1.0` or more | Effectively uses the fitted centreline itself: most repeatable, a few mm less diameter |

The report lists the fitted centreline and the chosen axis's distance from it.

## Blocks larger than the largest calibration reference

A radius up to `MaximumCalibrationExtrapolationPixels` beyond the largest measured
reference is calculated by continuing the straight line through the two nearest
references (about 2.1 mm per pixel with the current package, so 15 px is about 32 mm of
radius). Values inside the calibrated range are unchanged. The diagnostics report lists
how many points were extrapolated under `CALIBRATION RANGE USE`. For blocks beyond this
allowance, calibrate with a larger measured reference rather than raising the setting.

Calibration files, the calibration fingerprint, camera configuration and the image
geometry are not changed.

## Required verification before production use

This branch changes the commanded positions substantially. Before enabling it on the
machine, scan a round reference mounted a known distance off-centre at a marked angle
and confirm head/tail, X/Y sign and rotation direction.

## Reading the laser line once per scan

The calibration bands for an image column depend only on the calibration table, not on the frame.
`LookupTableInterpolator` now works them out once for all columns when a scan starts reading its frames,
and reuses them for every frame of that scan, instead of searching the whole table again for every column
of every frame. The calculation itself is unchanged (`BuildColumnBands` is the original code), so every
radius is identical to before. The remembered bands belong to the loaded calibration table object, and the
calibration file is read fresh for every scan, so a scan can never use bands from another calibration.
The calibration files, their fingerprint and the camera settings are not touched.

## Related documents

- [Scan cycle](../operation/SCAN_CYCLE.md): readiness checks, the PLC write, saving, and what happens when a scan fails.
- [Offline replay](../development/OFFLINE_REPLAY.md): re-running a saved scan on the development computer.
- [Calibration](CALIBRATION.md) and [Dual camera concatenation](DUAL_CAMERA_CONCATENATION.md).
