namespace OptimizationXY.Charger.Models.Settings;

/// <summary>
/// Operator-facing scan-quality display settings. These thresholds evaluate
/// whether the block was scanned completely; they do not judge log quality.
/// </summary>
public sealed class OperatorViewSettings
{
    public bool Enabled { get; set; } = true;
    public bool AutoShow { get; set; } = true;
    public bool TopMost { get; set; } = true;
    public bool StartMaximized { get; set; } = true;

    /// <summary>
    /// Zero-based monitor index. The default selects the second monitor when
    /// present and safely falls back to the primary monitor. 
    /// </summary>
    public int DisplayIndex { get; set; } = 1;

    public double GoodMinimumFramePercent { get; set; } = 99.0;
    public double WarningMinimumFramePercent { get; set; } = 80.0;
    public double GoodMinimumProfileCoveragePercent { get; set; } = 70.0;
    public double WarningMinimumProfileCoveragePercent { get; set; } = 65.0;
    public double GoodMinimumValidSlicePercent { get; set; } = 90.0;
    public double WarningMinimumValidSlicePercent { get; set; } = 70.0;
    public double MinimumAngularCoveragePercentPerSlice { get; set; } = 70.0;
    public double GoodMaximumMissingGapMM { get; set; } = 25.4;
    public double WarningMaximumMissingGapMM { get; set; } = 76.2;

    /// <summary>
    /// Largest continuous arc of the revolution, in degrees, that may have no
    /// measurements anywhere along the block. Beyond half a turn the off-centre
    /// direction of the block cannot be worked out, so the scan fails.
    /// </summary>
    public double MaximumAngularGapDeg { get; set; } = 180.0;
}
