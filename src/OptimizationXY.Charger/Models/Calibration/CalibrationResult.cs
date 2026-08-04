namespace OptimizationXY.Charger.Models.Calibration;

/// <summary>
/// Calibrated axial scale used to convert image columns to millimeters.
/// </summary>
public sealed class CalibrationResult
{
    public double LengthPixelsPerMM { get; set; }
}
