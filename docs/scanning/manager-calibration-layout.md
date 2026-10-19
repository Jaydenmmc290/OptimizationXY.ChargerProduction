# Using the manager's calibration images and image format

Goal: this program uses the same image format (962 x 480) and the same
calibration references as the manager's program, so both work in one
coordinate system with the home-position cable as radius 0.

## What changes

| | Current | Manager format |
|---|---|---|
| Head image kept | all 640 px | 481 px (159 px cut from its right edge) |
| Tail image kept | all 640 px | 481 px (159 px cut from its left edge) |
| Overlap at the seam | 328 px | 0 px (images placed side by side) |
| Concatenated width | 952 px | 962 px |
| References | 12.50 in + 23.955 in tube | 12.50 in + 23.955 in tube + home cable (radius 0) |

Only software crops change. The cameras still capture 640 x 480 with offset 0;
the camera configuration files are not touched.

**Check with the manager** that his 481 px halves are the head camera's LEFT
481 px and the tail camera's RIGHT 481 px. If he cuts other edges, use his
values instead of the 159s below.

## Steps on the production computer

1. Get the manager's three combined images (962 x 480): small tube, large tube,
   cable. Several images per reference (different rotations) are better than one.
2. Back up `Config\appsettings.json`, then set in `Acquisition.Concatenation`:

   ```json
   "HeadCropLeftPixels": 0,
   "HeadCropRightPixels": 159,
   "TailCropLeftPixels": 159,
   "TailCropRightPixels": 0,
   "OverlapPixels": 0
   ```

3. Preview (changes nothing):

   ```
   OptimizationXY.Charger.exe --import-calibration-images ^
     --small "C:\Cal\small.png" --small-diameter-in 12.5 ^
     --large "C:\Cal\large.png" --large-diameter-in 23.955 ^
     --home  "C:\Cal\cable.png" ^
     --operator "Jayden" --notes "Manager reference images"
   ```

   Repeat `--small`, `--large` or `--home` to give more than one image.
   The report opens when it finishes (also saved in the Logs folder). Check:
   - the width check passed (images are 962 px and the settings produce 962 px),
   - each reference found the laser in most columns,
   - the cable row is the lowest line (largest Y) and the large tube the highest,
   - the OLD vs NEW radius lines, which show how far the two calibrations differ.

4. Run the same command with `--activate` added. The active calibration is
   archived to `Config\Calibration\Archive\<date-time>` first.
5. Scan a known tube or block and compare with the manager's program.

`LengthPixelsPerMM` is kept from the current calibration because cropping does
not change the pixel size. Pass `--length-pixels-per-mm <value>` to use another value.

## Going back

Restore the backed-up `appsettings.json`, then copy the five files from the
newest folder in `Config\Calibration\Archive` back into `Config\Calibration`.
