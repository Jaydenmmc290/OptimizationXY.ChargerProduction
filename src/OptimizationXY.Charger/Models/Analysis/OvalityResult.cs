namespace OptimizationXY.Charger.Models.Analysis;

/// <summary>
/// Represents the results of an ovality analysis performed on a log cross-section.
///
/// Ovality refers to the deviation of the log's cross-section from a perfect circle.
/// This class captures both direct measurements (diameters) and harmonic decomposition
/// of the cross-sectional shape, allowing for detailed shape analysis.
///
/// The harmonic components break down the shape deviations into sinusoidal patterns:
/// - Harmonic 0 (A0): Average radius component
/// - Harmonic 1 (A1, B1): Asymmetric components
/// - Harmonic 2 (A2, B2): Ovality (elliptical) components
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
    /// This represents the mean diameter component (harmonic 0) of the shape.
    /// </summary>
    public double MeanRadiusMM { get; set; }

    /// <summary>
    /// Gets or sets the largest diameter measured across any direction in the cross-section, measured in millimeters.
    ///
    /// This is the maximum width of the oval-shaped cross-section.
    /// </summary>
    public double MajorDiameterMM { get; set; }

    /// <summary>
    /// Gets or sets the smallest diameter measured across any direction in the cross-section, measured in millimeters.
    ///
    /// This is the minimum width of the oval-shaped cross-section.
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
    /// Gets or sets the ovality expressed as a percentage of the mean diameter.
    ///
    /// Formula: (OvalityMM / (MeanRadiusMM * 2)) * 100
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
    /// In harmonic analysis, this represents asymmetry in the cross-section.
    /// Non-zero values indicate the oval is oriented off-center.
    /// </summary>
    public double HarmonicA1 { get; set; }

    /// <summary>
    /// Gets or sets the first harmonic sine component (B1).
    ///
    /// In harmonic analysis, this represents asymmetry in the cross-section.
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
    /// Indicates which direction the major axis of the oval is pointing (0-360 degrees).
    /// Useful for aligning correction movements.
    /// </summary>
    public double OrientationDeg { get; set; }

    /// <summary>
    /// Gets or sets the total number of cross-sectional slices analyzed in this ovality analysis.
    ///
    /// More slices generally provide better resolution for detecting variations along
    /// the log length.
    /// </summary>
    public int SliceCount { get; set; }

    /// <summary>
    /// Gets or sets the number of measurement points (e.g., from laser scan) in the slice
    /// where the worst ovality was detected.
    ///
    /// Higher point counts provide better angular resolution for ovality measurement.
    /// </summary>
    public int WorstSlicePointCount { get; set; }
}
