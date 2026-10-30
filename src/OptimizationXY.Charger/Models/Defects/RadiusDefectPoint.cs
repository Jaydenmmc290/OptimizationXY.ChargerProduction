namespace OptimizationXY.Charger.Models.Defects;

/// <summary>
/// One classified cell of the binned radius field: where it is on the log
/// (Z along the log, angle around it), its measured radius, the radius it was
/// expected to have, and whether that difference counts as a defect.
///
/// This class captures all relevant data for a point on the log surface,
/// including its location (axial Z position and angular position) and
/// measurements comparing actual vs. expected radius values.
///
/// Multiple RadiusDefectPoints collected across a cross-section create
/// a RadiusDefectPatch, and multiple patches across the log length create
/// the complete RadiusDefectMap.
/// </summary>
public class RadiusDefectPoint
{
    /// <summary>
    /// Gets or sets the axial position (Z coordinate) of this measurement point along the log, measured in millimeters.
    ///
    /// Represents which cross-sectional slice this point belongs to.
    /// Used to spatially locate defects along the log length.
    /// </summary>
    public double ZBinMM { get; set; }

    /// <summary>
    /// Gets or sets the circumferential/angular position of this measurement point around the log surface, measured in degrees.
    ///
    /// 0-360 degree range representing the angular location around the log's circumference.
    /// Used to determine where on the cross-section the defect is located.
    /// </summary>
    public double AngleBinDeg { get; set; }

    /// <summary>
    /// Gets or sets the actual measured radius at this point, measured in millimeters.
    ///
    /// This is the binned mean radius from the laser scanner after calibration and
    /// geometric processing. Represents the physical distance from centerline to
    /// the log surface at this angular and axial position.
    /// </summary>
    public double MeasuredRadiusMM { get; set; }

    /// <summary>
    /// Gets or sets the theoretical/expected radius at this point, measured in millimeters.
    ///
    /// This is the MEDIAN radius of all valid cells in the same Z slice. It is
    /// recomputed for every slice, so it follows the log's taper.
    /// Used to calculate deviation and defect severity.
    /// </summary>
    public double ExpectedRadiusMM { get; set; }

    /// <summary>
    /// Gets or sets the radius error (deviation) at this point, measured in millimeters.
    ///
    /// Calculated as: MeasuredRadiusMM - ExpectedRadiusMM
    /// Positive values indicate the measured radius is larger than expected.
    /// Negative values indicate the measured radius is smaller than expected (wane/material loss).
    /// This value drives the defect classification.
    /// </summary>
    public double RadiusErrorMM { get; set; }

    /// <summary>
    /// Gets or sets the classification type of the defect at this point.
    ///
    /// Classifies the severity and nature of the radius error:
    /// - Good: Within acceptable limits
    /// - LowRadius: Minor material deficiency
    /// - SevereLowRadius: Severe material deficiency
    /// - HighRadius: Excess material
    /// - Noise: reserved; RadiusDefectMapBuilder does not currently assign it
    ///
    /// Used for optimization penalty calculations and filtering.
    /// </summary>
    public RadiusDefectType DefectType { get; set; }
}
