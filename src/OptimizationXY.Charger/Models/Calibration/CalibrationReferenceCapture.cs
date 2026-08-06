namespace OptimizationXY.Charger.Models.Calibration;

/// <summary>
/// Represents a capture of calibration reference data, including a summary, lookup points, and held-out samples.
/// </summary>
public sealed class CalibrationReferenceCapture
{

    //CalibrationReferenceCapture is a record type that encapsulates the details of a calibration reference capture.
    //It includes a summary of the calibration reference, a list of lookup points, and a list of held-out samples.
    public required CalibrationReferenceSummary Summary { get; init; }

    // Lookup points are a collection of data points used for calibration reference.
    public required IReadOnlyList<LookupPoint> Points { get; init; }

    // Held-out samples are a collection of calibration laser samples that are not used in
    // the calibration process but are kept for validation or testing purposes.
    public required IReadOnlyList<CalibrationLaserSample> HeldOutSamples { get; init; }
}
