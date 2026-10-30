
using Emgu.CV;
using OptimizationXY.Charger.Models.Geometry;
namespace OptimizationXY.Charger.Models.Core;


/// <summary>
/// One processing frame: a full-length (head + tail joined) picture of the log
/// at one rotation angle, plus what the pipeline measured from it.
/// </summary>
/// <remarks>
/// A scan is a list of these, one per head/tail pair. The pipeline fills in
/// <see cref="Profile"/> (laser-line measurements along the log) and
/// <see cref="AngleDeg"/> (where the log was in its turn). Frames with
/// <see cref="IsValid"/> = false or <see cref="ExcludedFromRevolution"/> = true
/// are kept for the record but not used to build the 3D point cloud.
/// </remarks>
public class ScanFrame
{
    // Position of the frame in capture order (0 = first pair after ScanStart).
    public int FrameIndex { get; set; }

    // Rotation angle of the log (degrees) when this image was taken. Set from the
    // measured revolution (RevolutionAngleAssigner), not just frame index x step.
    public double AngleDeg { get; set; }

    // Original file path when the frame came from disk.
    public string ImagePath { get; set; } = "";

    // Immutable copy written into the saved Scan_#### result folder.
    public string SavedImagePath { get; set; } = "";

    // Laser-line measurements extracted from this one image: one entry per
    // image column along the log (see LogProfileColumn).
    public List<LogProfileColumn> Profile { get; set; } = [];

    // False when the frame could not be used (bad image, no laser line, ...).
    public bool IsValid { get; set; } = true;

    // True when the frame was captured while the log was not turning (before the
    // revolution started or after it finished). Such frames are kept for the audit
    // record but are never used to build the point cloud.
    public bool ExcludedFromRevolution { get; set; }
    // Plain-language reason when the frame is invalid or excluded.
    public string StatusMessage { get; set; } = "";

    // Optional debug image: the greyscale frame used for laser detection.
    public Mat? GrayImage { get; set; }

    // Optional debug image: the thresholded (black/white) laser mask.
    public Mat? BinaryImage { get; set; }

    // Optional debug image: the frame with the detected laser line drawn on it.
    public Mat? OverlayImage { get; set; }
}
