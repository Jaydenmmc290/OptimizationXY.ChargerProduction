namespace OptimizationXY.Charger.Models.Calibration;

/// <summary>
/// Quality numbers saved with a scanner calibration: how many reference
/// objects and lookup points it has, the radius range it covers, its
/// repeatability errors, and independent validation results.
/// </summary>
public sealed class CalibrationQualitySummary
{

    // ReferenceCount: number of physical references captured (e.g. small tube, large tube, home cable).
    public int ReferenceCount { get; set; }

    // LookupPointCount: number of (column, laser row) -> radius points in the lookup table.
    public int LookupPointCount { get; set; }

    // MinimumRadiusMM: smallest reference radius in the table, mm (0 when the home cable is included).
    public double MinimumRadiusMM { get; set; }

    // MaximumRadiusMM: largest reference radius in the table, mm. Logs bigger than this are outside the calibrated range.
    public double MaximumRadiusMM { get; set; }

    // MeanRepeatabilityErrorMM: average error (mm) when the reference images are run back through the table.
    public double MeanRepeatabilityErrorMM { get; set; }

    // MaximumRepeatabilityErrorMM: worst-case error (mm) of that same check.
    public double MaximumRepeatabilityErrorMM { get; set; }

    // IndependentValidationCompleted: true once held-out samples (not used to build the table) were checked against it.
    public bool IndependentValidationCompleted { get; set; }

    // IndependentValidationMeanErrorMM: The mean error measured during independent validation of the calibration, in millimeters.
    public double? IndependentValidationMeanErrorMM { get; set; }

    // IndependentValidationMaximumErrorMM: The maximum error measured during independent validation of the calibration, in millimeters.
    public double? IndependentValidationMaximumErrorMM { get; set; }

    // Status: calibration status text; "CommissioningOnly" until it has been validated for production.
    public string Status { get; set; } = "CommissioningOnly";

    // Notes: Additional notes or comments regarding the calibration process, with a default value of an empty string.
    public string Notes { get; set; } = string.Empty;
}
