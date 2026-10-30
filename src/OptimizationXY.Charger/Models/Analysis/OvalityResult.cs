namespace OptimizationXY.Charger.Models.Analysis;

/// <summary>
/// Ovality ("out-of-roundness") of the WORST cross-section of the log.
///
/// Ovality refers to the deviation of the log's cross-section from a perfect circle.
/// How it is made (see OvalityAnalyzer): for every Z slice, the radius at each
/// angle is fitted with r(theta) = A0 + A1 cos(theta) + B1 sin(theta)
/// + A2 cos(2 theta) + B2 sin(2 theta) (a least-squares Fourier fit). The slice
/// with the largest <see cref="OvalityMM"/> is reported. Diameters are taken
/// from the fit, not measured directly across the log.
///
/// The harmonic components break down the shape deviations into sinusoidal patterns:
/// - Harmonic 0 (A0): Average radius component
/// - Harmonic 1 (A1, B1): how far the fitted circle's centre is off the axis
///   the radii were measured from (eccentricity), not a shape change
/// - Harmonic 2 (A2, B2): ovality (elliptical) components
/// Radii come from the binned radius field, so all lengths are in millimetres.
/// </summary>
public class OvalityResult
{
    /// <summary>
    /// Gets or sets the axial position (Z-coordinate) along the log where the worst ovality is detected, measured in millimeters.
    ///
    /// This indicates which cross-section slice exhibits the maximum ovality deviation.
    /// Useful for pinpointing problem areas along the log length.
    /// </summary>
    public double WorstZMM { get; set; }

    /// <summary>
    /// Gets or sets the average radius of the log cross-section, measured in millimeters.
    ///
    /// Calculated from all measured radius points in the cross-section.
    /// This is the A0 term of the fit (mean RADIUS, not diameter).
    /// </summary>
    public double MeanRadiusMM { get; set; }

    /// <summary>
    /// Gets or sets the major (largest) diameter of the fitted oval, in millimetres:
    ///
    /// 2 x (A0 + second-harmonic amplitude).
    /// </summary>
    public double MajorDiameterMM { get; set; }

    /// <summary>
    /// Gets or sets the minor (smallest) diameter of the fitted oval, in millimetres:
    ///
    /// 2 x (A0 - second-harmonic amplitude).
    /// </summary>
    public double MinorDiameterMM { get; set; }

    /// <summary>
    /// Gets or sets the ovality magnitude, calculated as (MajorDiameterMM - MinorDiameterMM), measured in millimeters.
    ///
    /// This is the primary metric for ovality severity. Higher values indicate more
    /// pronounced deviation from a circular shape.
    /// </summary>
    public double OvalityMM { get; set; }

    /// <summary>
    /// Gets or sets the ovality expressed as a percentage of the MAJOR diameter.
    ///
    /// Formula: (OvalityMM / MajorDiameterMM) * 100 (0 if the major diameter is not positive).
    /// Provides a normalized measure of ovality relative to log size.
    /// </summary>
    public double OvalityPercent { get; set; }

    /// <summary>
    /// Gets or sets the zeroth harmonic component (A0), representing the average radius.
    ///
    /// In Fourier harmonic decomposition, this is the constant term that represents
    /// the mean size of the cross-section.
    /// </summary>
    public double HarmonicA0 { get; set; }

    /// <summary>
    /// Gets or sets the first harmonic cosine component (A1).
    ///
    /// Together with B1 this is the offset of the fitted circle's centre from the
    /// measurement axis (cosine part). Non-zero means the slice is off-centre, not oval.
    /// </summary>
    public double HarmonicA1 { get; set; }

    /// <summary>
    /// Gets or sets the first harmonic sine component (B1).
    ///
    /// Sine part of the same centre offset (see <see cref="HarmonicA1"/>).
    /// Combined with A1, describes the eccentric position of the center.
    /// </summary>
    public double HarmonicB1 { get; set; }

    /// <summary>
    /// Gets or sets the second harmonic cosine component (A2).
    ///
    /// In harmonic analysis, this represents the primary ovality (elliptical) component.
    /// This is the dominant component for truly oval cross-sections.
    /// </summary>
    public double HarmonicA2 { get; set; }

    /// <summary>
    /// Gets or sets the second harmonic sine component (B2).
    ///
    /// In harmonic analysis, this represents the orientation of the ovality.
    /// Combined with A2, describes the direction of the major/minor axes.
    /// </summary>
    public double HarmonicB2 { get; set; }

    /// <summary>
    /// Gets or sets the amplitude of the second harmonic component, measured in millimeters.
    ///
    /// Calculated as sqrt(A2² + B2²), this represents the magnitude of ovality
    /// independent of its orientation.
    /// </summary>
    public double SecondHarmonicAmplitudeMM { get; set; }

    /// <summary>
    /// Gets or sets the orientation angle of the ovality, measured in degrees.
    ///
    /// Indicates which direction the major axis of the oval points, 0 to 180 degrees
    /// (an oval looks the same after a half turn), in the same angle frame as the
    /// radius-field angle bins: 0.5 x atan2(B2, A2).
    /// Useful for aligning correction movements.
    /// </summary>
    public double OrientationDeg { get; set; }

    /// <summary>
    /// Gets or sets how many Z slices had enough valid angle bins to be analysed.
    ///
    /// More slices generally provide better resolution for detecting variations along
    /// the log length.
    /// </summary>
    public int SliceCount { get; set; }

    /// <summary>
    /// Gets or sets the number of valid angle bins in the slice
    /// where the worst ovality was detected.
    ///
    /// Higher point counts provide better angular resolution for ovality measurement.
    /// </summary>
    public int WorstSlicePointCount { get; set; }
}
