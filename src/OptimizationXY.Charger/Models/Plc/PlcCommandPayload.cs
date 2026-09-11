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
    /// Gets or sets the head and tail coordinates in millimeters, as well as the small and large diameters,
    /// largest and smallest measured radii, knife clearance, and safe carriage retract radius.
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
    /// Gets or sets the spindle rotation amount in degrees.
    /// This value is sent to the PLC as the SpindleRotate tag when kinematics rotation is enabled.
    /// The new rotation DLL expects this as a single REAL (float) value.
    /// </summary>
    public double SpindleRotationDegrees { get; set; }

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

    /// <summary>
    /// Gets the spindle rotation value as a single float.
    /// This is the value sent to the PLC's SpindleRotate tag.
    /// </summary>
    public float SpindleRotateValue => (float)SpindleRotationDegrees;
}
