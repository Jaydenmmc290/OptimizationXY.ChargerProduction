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

If the return to the starting profile cannot be found, the frames between rotation start
and rotation stop are divided evenly over 360 degrees and the readiness report shows a
`Rotation coverage` warning.

Frame intake also stops the moment the PLC reports ScanComplete.

## Settings (`RotatingScan`)

All have defaults, so an existing `appsettings.json` keeps working unchanged.

| Setting | Default | Meaning |
|---|---|---|
| `UseMeasuredRevolutionAngles` | `true` | `false` restores the legacy fixed step |
| `StationaryProfileChangeMM` | `0.8` | RMS profile change below this is a resting log |
| `RevolutionClosureToleranceMM` | `1.5` | RMS match needed to accept the 360 degree frame |
| `MinimumFramesPerRevolution` | `20` | Shortest revolution accepted |
| `MaximumCalibrationExtrapolationPixels` | `15` | Laser pixels a radius may be extrapolated past the largest measured reference; `0` disables |
| `ReverseRotationDirection` | `false` | Set after the off-centre reference test if Y is mirrored |
| `LaserAngleOffsetDeg` | `0` | Machine angle at which the laser strikes the log |

## Other geometry changes

- Z is measured from the tail end. With `HeadOnLeft = true` it is measured from the
  right-hand image edge, so Z = 0 is tail everywhere.
- Local centerline slices use a least-squares circle fit instead of the average of
  surface points.
- Inscribed-geometry samples are placed at the centre of each angular bin.

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
