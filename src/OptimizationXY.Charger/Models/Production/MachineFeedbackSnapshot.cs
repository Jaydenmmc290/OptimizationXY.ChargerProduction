namespace OptimizationXY.Charger.Models.Production;

/// <summary>
/// Feedback available from the supplied restricted PLC surface. Command
/// readback is verified. Actual encoder, spindle, knife, and peel-diameter
/// signals remain null until the PLC library exposes approved read tags.
/// </summary>
public sealed class MachineFeedbackSnapshot
{
    /// <summary>When the snapshot was taken (UTC).</summary>
    public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;
    /// <summary>True when the command tags read back from the PLC equal what was written.</summary>
    public bool CommandReadbackVerified { get; set; }

    /// <summary>Actual machine positions - always null today (no approved read tags yet).</summary>
    public double? ActualHeadXMM { get; set; }
    public double? ActualHeadYMM { get; set; }
    public double? ActualTailXMM { get; set; }
    public double? ActualTailYMM { get; set; } 
    public double? ActualKnifePositionMM { get; set; }
    public double? ActualPeelDiameterMM { get; set; }
    public long? EncoderCount { get; set; }

    /// <summary>Names of the signals above that the PLC library cannot provide yet.</summary>
    public string[] UnavailableSignals { get; set; } =
    [
        "ActualHeadXMM",
        "ActualHeadYMM",
        "ActualTailXMM",
        "ActualTailYMM",
        "ActualKnifePositionMM",
        "ActualPeelDiameterMM",
        "EncoderCount"
    ];
}
