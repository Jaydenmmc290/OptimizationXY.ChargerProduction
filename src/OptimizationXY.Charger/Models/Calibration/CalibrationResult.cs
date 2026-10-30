namespace OptimizationXY.Charger.Models.Calibration;

/// <summary>
/// Calibrated axial (along-the-log) scale used to convert image columns to
/// millimetres. It is measured in the setup wizard by clicking two marks a
/// known distance apart on a joined image.
/// </summary>
public sealed class CalibrationResult
{

    /// <summary>
    /// Gets or sets the calibrated length in pixels per millimeter.
    /// </summary>
    public double LengthPixelsPerMM { get; set; }
}
