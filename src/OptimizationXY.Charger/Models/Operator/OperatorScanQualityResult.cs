using OptimizationXY.Charger.UI.Controls;


// OperatorScanQualityResult.cs - the scan-quality verdict shown to the lathe operator.
namespace OptimizationXY.Charger.Models.Operator;

/// <summary>
/// The scan-quality verdict for one scan, made by OperatorScanQualityAnalyzer
/// and shown on the operator screen. It is NOT made by the operator - it
/// tells the operator whether the scan was good enough.
/// </summary>
/// <remarks>
/// A <see cref="ScanQualityStatus.Failed"/> result is the scan-quality gate:
/// it blocks the PLC write so a bad scan is never sent.
/// </remarks>
public sealed class OperatorScanQualityResult
{

    /// <summary>
    /// Gets or sets the status of the scan quality assessment.
    /// </summary>
    public ScanQualityStatus Status { get; init; }

    /// <summary>
    /// Gets or sets the message intended for the operator, providing information about the scan quality assessment.
    /// </summary>
    public string OperatorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the detailed message providing additional information about the scan quality assessment.
    /// </summary>
    public string DetailMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets how many frames were actually used to build the point cloud.
    /// </summary>
    public int FramesUsed { get; init; }

    /// <summary>
    /// Gets or sets the number of frames the scan was expected to provide.
    /// </summary>
    public int ExpectedFrames { get; init; }

    /// <summary>
    /// Gets or sets the percentage of the block length that has laser measurements.
    /// </summary>
    public double ProfileCoveragePercent { get; init; }

    /// <summary>
    /// Gets or sets the percentage of Z slices along the block that had enough data to be valid.
    /// </summary>
    public double ValidSlicePercent { get; init; }

    /// <summary>
    /// Gets or sets the length of the log in inches, providing information about the scanned log's size.
    /// </summary>
    public double LogLengthInches { get; init; }

    /// <summary>
    /// Gets or sets the average diameter of the scanned log in inches, providing information about the log's size.
    /// </summary>
    public double AverageDiameterInches { get; init; }

    /// <summary>
    /// Gets or sets the minimum diameter of the scanned log in inches, providing information about the smallest diameter encountered during the scan.
    /// </summary>
    public double MinimumDiameterInches { get; init; }

    /// <summary>
    /// Gets or sets the maximum diameter of the scanned log in inches, providing information about the largest diameter encountered during the scan.
    /// </summary>
    public double MaximumDiameterInches { get; init; }


    /// <summary>
    /// Gets or sets the largest missing gap in inches, indicating the size of the largest gap in the scanned log where data may be missing or incomplete.
    /// </summary>
    public double LargestMissingGapInches { get; init; }

    /// <summary>
    /// Number of angle bins around the revolution that have at least one measurement anywhere along the block.
    /// </summary>
    public int AngularBinsCovered { get; init; }

    /// <summary>
    /// Number of angle bins in a full revolution for the configured bin size.
    /// </summary>
    public int ExpectedAngularBins { get; init; }

    /// <summary>
    /// Largest continuous arc of the revolution, in degrees, with no measurements anywhere along the block.
    /// </summary>
    public double LargestAngularGapDeg { get; init; }

    /// <summary>
    /// Gets or sets the processing time in seconds, indicating the duration taken to process the scan.
    /// </summary>
    public double ProcessingSeconds { get; init; }

    /// <summary>
    /// Gets or sets the side-view profile points drawn on the operator screen (LogProfileControl).
    /// </summary>
    public IReadOnlyList<LogProfilePoint> ProfilePoints { get; init; } =
        [];
}
