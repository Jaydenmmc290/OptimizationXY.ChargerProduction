namespace OptimizationXY.Charger.Models.Production;

/// <summary>
/// Feedback available from the supplied restricted PLC surface. Command
/// readback is verified. Actual encoder, spindle, knife, and peel-diameter
/// signals remain null until the PLC library exposes approved read tags.
/// </summary>
public sealed class MachineFeedbackSnapshot
{
    public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;
    public bool CommandReadbackVerified { get; set; }

    public double? ActualHeadXMM { get; set; }
    public double? ActualHeadYMM { get; set; }
    public double? ActualTailXMM { get; set; }
    public double? ActualTailYMM { get; set; } 
    public double? ActualKnifePositionMM { get; set; }
    public double? ActualPeelDiameterMM { get; set; }
    public long? EncoderCount { get; set; }

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
