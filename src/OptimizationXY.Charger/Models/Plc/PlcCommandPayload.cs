namespace OptimizationXY.Charger.Models.Plc;

/// <summary>
/// The exact numbers written to the PLC for one scan (built by
/// PlcPayloadBuilder, written by XyRestrictedPlcService). This application
/// intentionally contains no
/// rotation command; log rotation remains exclusively in the PLC program.
/// </summary>
public sealed class PlcCommandPayload
{
    /// <summary>
    /// Gets or sets the block (scan) sequence number for this command. It
    /// increments every production scan and is sent in every array so the PLC and
    /// the readback check can tell this command from the previous one. Must stay
    /// at or below 16,000,000 so it is exact when sent as a 32-bit FLOAT.
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
    /// The "scanned block data" array, in this exact order (all INCHES except the count):
    /// [0] head X, [1] head Y, [2] tail X, [3] tail Y, [4] small diameter,
    /// [5] large diameter, [6] safe carriage retract radius, [7] smallest measured
    /// radius, [8] spare (0), [9] block count. The PLC reads by position, so do
    /// not reorder. See docs/plc/PLC_TAG_PAYLOADS.txt.
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
    /// The "carriage retract" array: [0] safe carriage retract radius (inches),
    /// [1..3] spare (0), [4] block count.
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
    /// The "roundup complete" array: [0] smallest measured radius (inches),
    /// [1..3] spare (0), [4] block count.
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
