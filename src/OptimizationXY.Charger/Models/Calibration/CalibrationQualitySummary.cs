namespace OptimizationXY.Charger.Models.Calibration;

public sealed class CalibrationQualitySummary
{
    public int ReferenceCount { get; set; }
    public int LookupPointCount { get; set; }
    public double MinimumRadiusMM { get; set; }
    public double MaximumRadiusMM { get; set; }
    public double MeanRepeatabilityErrorMM { get; set; }
    public double MaximumRepeatabilityErrorMM { get; set; }
    public bool IndependentValidationCompleted { get; set; }
    public double? IndependentValidationMeanErrorMM { get; set; }
    public double? IndependentValidationMaximumErrorMM { get; set; }
    public string Status { get; set; } = "CommissioningOnly";
    public string Notes { get; set; } = string.Empty;
}
