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

    // MeanRepeatabilityErrorMM: average error (mm) when the HELD-OUT frames of each reference (about the last
    // quarter, not used to build the table) are run through the table and compared with the known radius.
    // NaN when no reference had held-out frames (e.g. imported single images).
    public double MeanRepeatabilityErrorMM { get; set; }

    // MaximumRepeatabilityErrorMM: worst-case error (mm) of that same held-out check.
    public double MaximumRepeatabilityErrorMM { get; set; }

    // IndependentValidationCompleted: true when a separate validation (independent of the references used to
    // build the table) was done and passed in by the setup wizard; its errors are in the two fields below.
    public bool IndependentValidationCompleted { get; set; }

    // IndependentValidationMeanErrorMM: The mean error measured during independent validation of the calibration, in millimeters.
    public double? IndependentValidationMeanErrorMM { get; set; }

    // IndependentValidationMaximumErrorMM: The maximum error measured during independent validation of the calibration, in millimeters.
    public double? IndependentValidationMaximumErrorMM { get; set; }

    // Status: "CommissioningOnly" without independent validation, "ValidatedPendingEngineeringApproval" with it.
    public string Status { get; set; } = "CommissioningOnly";

    // Notes: Additional notes or comments regarding the calibration process, with a default value of an empty string.
    public string Notes { get; set; } = string.Empty;
}
