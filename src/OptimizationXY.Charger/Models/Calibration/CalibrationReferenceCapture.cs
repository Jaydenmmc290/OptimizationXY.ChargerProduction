namespace OptimizationXY.Charger.Models.Calibration;

public sealed class CalibrationReferenceCapture
{
    public required CalibrationReferenceSummary Summary { get; init; }
    public required IReadOnlyList<LookupPoint> Points { get; init; }
    public required IReadOnlyList<CalibrationLaserSample> HeldOutSamples { get; init; }
}
