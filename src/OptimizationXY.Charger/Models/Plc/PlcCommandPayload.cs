namespace OptimizationXY.Charger.Models.Plc;

/// <summary>
/// Exact restricted PLC payload. This application intentionally contains no
/// rotation command; log rotation remains exclusively in the PLC program.
/// </summary>
public sealed class PlcCommandPayload
{
    public int BlockCount { get; set; }

    public double HeadXMM { get; set; }
    public double HeadYMM { get; set; }
    public double TailXMM { get; set; }
    public double TailYMM { get; set; }

    public double SmallDiameterMM { get; set; }
    public double LargeDiameterMM { get; set; }
    public double LargestMeasuredRadiusMM { get; set; }
    public double SmallestMeasuredRadiusMM { get; set; }
    public double KnifeClearanceMM { get; set; }
    public double SafeCarriageRetractRadiusMM { get; set; }

    public float[] ScannedBlockData =>
    [
        (float)HeadXMM,
        (float)HeadYMM,
        (float)TailXMM,
        (float)TailYMM,
        (float)SmallDiameterMM,
        (float)LargeDiameterMM,
        (float)SafeCarriageRetractRadiusMM,
        (float)SmallestMeasuredRadiusMM,
        0.0f,
        BlockCount
    ];

    public float[] CarriageRetract =>
    [
        (float)SafeCarriageRetractRadiusMM,
        0.0f,
        0.0f,
        0.0f,
        BlockCount
    ];

    public float[] RoundupComplete =>
    [
        (float)SmallestMeasuredRadiusMM,
        0.0f,
        0.0f,
        0.0f,
        BlockCount
    ];
}
