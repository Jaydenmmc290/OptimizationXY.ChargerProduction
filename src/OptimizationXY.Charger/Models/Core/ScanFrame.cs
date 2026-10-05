
using Emgu.CV;
using OptimizationXY.Charger.Models.Geometry;
namespace OptimizationXY.Charger.Models.Core;


/// <summary>
/// Represents a single frame captured during a log scanning process, including its associated profile and diagnostic information.
/// </summary>
public class ScanFrame
{
    // FrameIndex is the index of the frame in the scan sequence.
    public int FrameIndex { get; set; }

    // Rotation angle of the log when this image was taken
    public double AngleDeg { get; set; }

    // Original file path when the frame came from disk.
    public string ImagePath { get; set; } = "";

    // Immutable copy written into the saved Scan_#### result folder.
    public string SavedImagePath { get; set; } = "";

    // Profile extracted from this one image
    public List<LogProfileColumn> Profile { get; set; } = [];

    // Diagnostics
    public bool IsValid { get; set; } = true;

    // True when the frame was captured while the log was not turning (before the
    // revolution started or after it finished). Such frames are kept for the audit
    // record but are never used to build the point cloud.
    public bool ExcludedFromRevolution { get; set; }
    public string StatusMessage { get; set; } = "";

    // Optional images for debugging
    public Mat? GrayImage { get; set; }

    // Optional images for debugging
    public Mat? BinaryImage { get; set; }

    // Optional images for debugging
    public Mat? OverlayImage { get; set; }
}
