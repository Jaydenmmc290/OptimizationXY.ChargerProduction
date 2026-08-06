namespace OptimizationXY.Charger.Models.Calibration;

/// <summary>
/// Represents a summary of the quality of a calibration process, including reference counts,
/// lookup points, radius measurements, repeatability errors, and validation results.
/// </summary>
public sealed class CalibrationQualitySummary
{

    // ReferenceCount: The number of reference points used in the calibration process.
    public int ReferenceCount { get; set; }

    // LookupPointCount: The number of lookup points used in the calibration process.
    public int LookupPointCount { get; set; }

    // MinimumRadiusMM: The minimum radius measured during the calibration process, in millimeters.
    public double MinimumRadiusMM { get; set; }

    // MaximumRadiusMM: The maximum radius measured during the calibration process, in millimeters.
    public double MaximumRadiusMM { get; set; }

    // MeanRadiusMM: The mean radius measured during the calibration process, in millimeters.
    public double MeanRepeatabilityErrorMM { get; set; }

    // MaximumRepeatabilityErrorMM: The maximum repeatability error measured during the calibration process, in millimeters.
    public double MaximumRepeatabilityErrorMM { get; set; }

    // IndependentValidationCompleted: Indicates whether independent validation of the calibration has been completed.
    public bool IndependentValidationCompleted { get; set; }

    // IndependentValidationMeanErrorMM: The mean error measured during independent validation of the calibration, in millimeters.
    public double? IndependentValidationMeanErrorMM { get; set; }

    // IndependentValidationMaximumErrorMM: The maximum error measured during independent validation of the calibration, in millimeters.
    public double? IndependentValidationMaximumErrorMM { get; set; }

    // Status: The current status of the calibration process, with a default value of "CommissioningOnly".
    public string Status { get; set; } = "CommissioningOnly";

    // Notes: Additional notes or comments regarding the calibration process, with a default value of an empty string.
    public string Notes { get; set; } = string.Empty;
}
