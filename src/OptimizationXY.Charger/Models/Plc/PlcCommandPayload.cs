namespace OptimizationXY.Charger.Models.Plc;

/// <summary>
/// Exact restricted PLC payload. This application intentionally contains no
/// rotation command; log rotation remains exclusively in the PLC program.
/// </summary>
public sealed class PlcCommandPayload
{
    /// <summary>
    /// Gets or sets the number of blocks in the scanned data.
    /// </summary>
    public int BlockCount { get; set; }

    /// <summary>
    /// Head/tail commands, diameters, radii, knife clearance and safe carriage
    /// retract radius sent to the PLC.
    ///
    /// UNITS: these values are in INCHES. PlcPayloadBuilder converts from
    /// millimetres before filling them. The "MM" in the property names is
    /// historical and is kept only so saved audit records and existing tools
    /// keep reading the same field names. Convert with x 25.4 before comparing
    /// with any millimetre value.
    /// </summary>
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


    /// <summary>
    /// Gets the scanned block data as an array of floats, including head and tail coordinates, diameters, radii, and block count.
    /// </summary>
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

    /// <summary>
    /// Gets the carriage retract data as an array of floats, including the safe carriage retract radius and block count.
    /// </summary>
    public float[] CarriageRetract =>
    [
        (float)SafeCarriageRetractRadiusMM,
        0.0f,
        0.0f,
        0.0f,
        BlockCount
    ];

    /// <summary>
    /// Gets the roundup complete data as an array of floats, including the smallest measured radius and block count.
    /// </summary>
    public float[] RoundupComplete =>
    [
        (float)SmallestMeasuredRadiusMM,
        0.0f,
        0.0f,
        0.0f,
        BlockCount
    ];
}
