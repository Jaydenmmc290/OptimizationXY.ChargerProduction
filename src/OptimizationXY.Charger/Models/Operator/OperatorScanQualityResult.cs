using OptimizationXY.Charger.UI.Controls;


/// <summary>
/// Represents the result of a scan quality assessment performed by an operator,
/// including status, messages, and various metrics related to the scan.
/// </summary>
namespace OptimizationXY.Charger.Models.Operator;

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
    /// Gets or sets the number of frames used during the scan process.
    /// </summary>
    public int FramesUsed { get; init; }

    /// <summary>
    /// Gets or sets the expected number of frames to be used during the scan process.
    /// </summary>
    public int ExpectedFrames { get; init; }

    /// <summary>
    /// Gets or sets the percentage of the profile coverage achieved during the scan process.
    /// </summary>
    public double ProfileCoveragePercent { get; init; }

    /// <summary>
    /// Gets or sets the percentage of valid slices obtained during the scan process.
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
    /// Gets or sets the processing time in seconds, indicating the duration taken to process the scan.
    /// </summary>
    public double ProcessingSeconds { get; init; }

    /// <summary>
    /// Gets or sets the list of profile points obtained during the scan process, providing detailed information about the scanned log's profile.
    /// </summary>
    public IReadOnlyList<LogProfilePoint> ProfilePoints { get; init; } =
        [];
}
