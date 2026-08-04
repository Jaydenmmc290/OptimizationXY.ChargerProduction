# Dual-Camera Concatenation

## Required frame contract

One rotation position consists of one head-camera image and one tail-camera image. The capture boundary combines each synchronized pair before any measurement processing:

```text
36 head frames + 36 tail frames
                |
                v
36 full-length concatenated ScanFrame images
                |
                v
profile -> point cloud -> centerline -> radius field -> defects -> optimization
```

The processing pipeline must never receive the old interleaved 72-frame sequence. `CompletedLogCapture.Frames` now contains only the 36 full-length images.

## Example validation

The supplied `40.zip` reference set was inspected during this rework:

- 36 numbered JPEG images
- every image is 962 x 480 pixels
- every image is already a full-length concatenated view

That is the input shape the live head/tail capture now reproduces.

## Shared registration implementation

`DualCameraFrameConcatenator` is used for both:

- the live operator preview; and
- the raw grayscale images sent to the measurement pipeline.

The preview may be contrast-normalized for display, but it uses the same crop, orientation, overlap, side order, and vertical-offset settings as production concatenation. Measurement frames are concatenated from raw grayscale data without display normalization or rescaling.

## Configuration

The settings are stored under `Acquisition.Concatenation` in the runtime `appsettings.json` file:

```json
{
  "Acquisition": {
    "TargetFramePairsPerLog": 36,
    "Concatenation": {
      "HeadOnLeft": true,
      "ReverseHeadHorizontal": false,
      "ReverseTailHorizontal": false,
      "HeadCropLeftPixels": 0,
      "HeadCropRightPixels": 0,
      "TailCropLeftPixels": 0,
      "TailCropRightPixels": 0,
      "TailVerticalOffsetPixels": 0,
      "OverlapPixels": 0,
      "RequireEqualHeights": true
    }
  }
}
```

### Setting meanings

- `HeadOnLeft`: places the head image before the tail image when true.
- `ReverseHeadHorizontal` / `ReverseTailHorizontal`: corrects camera orientation without changing measurement scale.
- crop values: remove fixture-only or duplicate columns at the outside/seam edges.
- `TailVerticalOffsetPixels`: aligns the tail laser/profile vertically to the head image. Positive values move the tail image downward.
- `OverlapPixels`: overlaps the two registered halves; the seam uses the maximum grayscale value so a laser stripe is not averaged away.
- `RequireEqualHeights`: rejects mismatched measurement heights instead of silently rescaling one camera.

## Commissioning check

Use a straight reference target spanning both cameras. Adjust crop, orientation, vertical offset, and overlap until the profile crosses the seam continuously. Then confirm all 36 output images have the expected full width and the same physical millimeters-per-pixel calibration along the complete log length.
