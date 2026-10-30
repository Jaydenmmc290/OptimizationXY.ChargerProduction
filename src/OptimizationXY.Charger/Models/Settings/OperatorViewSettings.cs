namespace OptimizationXY.Charger.Models.Settings;

/// <summary>
/// Operator-facing scan-quality display settings. These thresholds evaluate
/// whether the block was scanned completely; they do not judge log quality.
/// </summary>
public sealed class OperatorViewSettings
{
    /// <summary>Show the lathe-operator screen at all.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Open the operator screen automatically when the program starts.</summary>
    public bool AutoShow { get; set; } = true;
    /// <summary>Keep the operator screen on top of other windows.</summary>
    public bool TopMost { get; set; } = true;
    /// <summary>Open the operator screen full-size.</summary>
    public bool StartMaximized { get; set; } = true;

    /// <summary>
    /// Zero-based monitor index. The default selects the second monitor when
    /// present and safely falls back to the primary monitor.
    /// </summary>
    public int DisplayIndex { get; set; } = 1;

    /// <summary>
    /// Good/Warning limits. A scan is Good when every value meets its "Good"
    /// limit, Warning when every value meets at least its "Warning" limit, and
    /// Failed otherwise (see OperatorScanQualityAnalyzer). Frame percent =
    /// frames used / frames expected.
    /// </summary>
    public double GoodMinimumFramePercent { get; set; } = 99.0;
    public double WarningMinimumFramePercent { get; set; } = 80.0;
    /// <summary>Percentage of the block length that must have laser data (Good / Warning limits).</summary>
    public double GoodMinimumProfileCoveragePercent { get; set; } = 70.0;
    public double WarningMinimumProfileCoveragePercent { get; set; } = 65.0;
    /// <summary>Percentage of slices along the block that must be valid (Good / Warning limits).</summary>
    public double GoodMinimumValidSlicePercent { get; set; } = 90.0;
    public double WarningMinimumValidSlicePercent { get; set; } = 70.0;
    /// <summary>A slice is valid only if at least this percentage of its angle bins have data.</summary>
    public double MinimumAngularCoveragePercentPerSlice { get; set; } = 70.0;
    /// <summary>Longest stretch of the block (mm) allowed to have no data: 25.4 mm = 1 inch Good, 76.2 mm = 3 inch Warning.</summary>
    public double GoodMaximumMissingGapMM { get; set; } = 25.4;
    public double WarningMaximumMissingGapMM { get; set; } = 76.2;

    /// <summary>
    /// Largest continuous arc of the revolution, in degrees, that may have no
    /// measurements anywhere along the block. Beyond half a turn the off-centre
    /// direction of the block cannot be worked out, so the scan fails.
    /// </summary>
    public double MaximumAngularGapDeg { get; set; } = 180.0;
}
