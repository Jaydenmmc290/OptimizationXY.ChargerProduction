namespace OptimizationXY.Charger.Models.Calibration;

/// <summary>
/// Calibrated axial scale used to convert image columns to millimeters.
/// </summary>
public sealed class CalibrationResult
{

    /// <summary>
    /// Gets or sets the calibrated length in pixels per millimeter.
    /// </summary>
    public double LengthPixelsPerMM { get; set; }
}
