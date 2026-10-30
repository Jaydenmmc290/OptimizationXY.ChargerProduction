namespace OptimizationXY.Charger.Models.Calibration;

/// <summary>
/// Everything measured from ONE calibration reference (one tube or the home
/// cable): its summary, the lookup points it contributes to the table, and
/// held-out samples kept back for checking the finished table.
/// </summary>
public sealed class CalibrationReferenceCapture
{

    // Summary: name, known size, image size and laser statistics for this reference.
    public required CalibrationReferenceSummary Summary { get; init; }

    // Lookup points: (column, laser row) -> known radius, added to the calibration table.
    public required IReadOnlyList<LookupPoint> Points { get; init; }

    // Held-out samples are a collection of calibration laser samples that are not used in
    // the calibration process but are kept for validation or testing purposes.
    public required IReadOnlyList<CalibrationLaserSample> HeldOutSamples { get; init; }
}
