
namespace OptimizationXY.Charger.Models.Analysis;

/// <summary>
/// Represents the results of a centerline sweep analysis performed on a log.
///
/// This class captures measurements of how much the log's centerline deviates
/// (sweeps) in different directions along its length. It provides both directional
/// sweep measurements (X, Y) and aggregate measures (total, average, max).
///
/// How the numbers are made (see CenterlineSweepAnalyzer): the local centre
/// slices are sorted by Z (position along the log). The average centre of the
/// first few slices (lowest Z) is called the "tail" end and the average of the
/// last few slices (highest Z) the "head" end. "Sweep" is the head-minus-tail
/// shift of the centre; "bow" is how far each slice centre sits off the
/// straight tail-to-head line. All values are in millimetres in the point-cloud
/// (scanner) coordinate frame.
///
/// Sweep measurements are useful for detecting bow and other longitudinal defects
/// in the log that affect its straightness and geometric quality.
/// </summary>
public class CenterlineSweepResult
{
    /// <summary>
    /// Signed shift of the log centre in X from the tail end to the head end, in
    /// millimetres (head centre X minus tail centre X).
    ///
    /// This is NOT an absolute or maximum value: it is a signed end-to-end
    /// difference, so it is positive when the head-end centre has a larger X.
    /// A large magnitude means the log axis is tilted (or swept) in X.
    /// </summary>
    public double XSweepMM { get; set; }

    /// <summary>
    /// Signed shift of the log centre in Y from the tail end to the head end, in
    /// millimetres (head centre Y minus tail centre Y).
    ///
    /// Like <see cref="XSweepMM"/> this is a signed end-to-end difference, not a
    /// maximum. A large magnitude means the log axis is tilted (or swept) in Y.
    /// </summary>
    public double YSweepMM { get; set; }

    /// <summary>
    /// Gets or sets the total combined sweep, calculated as the vector magnitude of XSweepMM and YSweepMM.
    ///
    /// Formula: sqrt(XSweepMM² + YSweepMM²)
    /// This provides a single value representing the overall straightness quality of the log.
    /// Measured in millimeters.
    /// </summary>
    public double TotalSweepMM { get; set; }

    /// <summary>
    /// Gets or sets the X coordinate of the log's tail end centerline position, measured in millimeters.
    ///
    /// "Tail" here means the low-Z end of the scanned slices. This coordinate is
    /// the average centre X of the end slices there.
    /// </summary>
    public double TailCenterXMM { get; set; }

    /// <summary>
    /// Gets or sets the Y coordinate of the log's tail end centerline position, measured in millimeters.
    ///
    /// "Tail" here means the low-Z end of the scanned slices. This coordinate is
    /// the average centre Y of the end slices there.
    /// </summary>
    public double TailCenterYMM { get; set; }

    /// <summary>
    /// Gets or sets the X coordinate of the log's head end centerline position, measured in millimeters.
    ///
    /// "Head" here means the high-Z end of the scanned slices. This coordinate is
    /// the average centre X of the end slices there.
    /// </summary>
    public double HeadCenterXMM { get; set; }

    /// <summary>
    /// Gets or sets the Y coordinate of the log's head end centerline position, measured in millimeters.
    ///
    /// "Head" here means the high-Z end of the scanned slices. This coordinate is
    /// the average centre Y of the end slices there.
    /// </summary>
    public double HeadCenterYMM { get; set; }

    /// <summary>
    /// Gets or sets the maximum bow sweep deviation detected along the log's length, measured in millimeters.
    ///
    /// Bow refers to the curvature of the log. This value represents the worst-case
    /// distance (looking down the log, in the X/Y plane) of any slice centre from
    /// the straight line through the tail and head centre positions.
    /// </summary>
    public double MaxBowSweepMM { get; set; }

    /// <summary>
    /// Gets or sets the average bow sweep deviation calculated across all measurement points, measured in millimeters.
    ///
    /// This is computed as the mean of all bow deviations sampled along the log's length,
    /// providing an overall assessment of average curvature severity.
    /// </summary>
    public double AverageBowSweepMM { get; set; }

    /// <summary>
    /// Gets or sets WHERE the worst bow is: the Z position (distance along the log,
    /// millimetres) of the slice with <see cref="MaxBowSweepMM"/>.
    ///
    /// This is a position, not a size of deviation. Useful for locating the
    /// critical bow along the block.
    /// </summary>
    public double WorstBowZMM { get; set; }
}
