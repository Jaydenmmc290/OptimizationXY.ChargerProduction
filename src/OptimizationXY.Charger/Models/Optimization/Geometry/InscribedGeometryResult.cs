using OptimizationXY.Charger.Models.Optimization.Spindle;

namespace OptimizationXY.Charger.Models.Optimization.Geometry;

/// <summary>
/// Result of a geometry-only spindle search. No defect, adaptive-risk,
/// movement-penalty, or final penalty score is used to select this result.
/// </summary>
public sealed class InscribedGeometryResult
{
    public InscribedGeometryKind Kind { get; set; }

    public OptimizedSpindleMovement SpindleMovement { get; set; } = new();

    /// <summary>
    /// Guaranteed circular radius for the cylinder result. For an oval result,
    /// this is the minor semi-axis, which is the guaranteed circular radius
    /// available to a conventional fixed-knife rotary lathe.
    /// </summary>
    public double GuaranteedCircularRadiusMM { get; set; }


    /// <summary>
    /// Guaranteed major semi-axis for the oval result. For a cylinder result, this is
    /// </summary>
    public double MajorSemiAxisMM { get; set; }

    /// <summary>
    /// Guaranteed minor semi-axis for the oval result. For a cylinder result, this is
    /// </summary>
    public double MinorSemiAxisMM { get; set; }

    /// <summary>
    /// Orientation of the inscribed geometry in degrees. For a cylinder result, this is the orientation
    /// of the cylinder axis. For an oval result, this is the orientation of the major semi-axis.
    /// </summary>
    public double OrientationDeg { get; set; }

    /// <summary>
    /// Guaranteed area of the inscribed geometry in square millimeters. For a cylinder result,
    /// this is the area of the circle. For an oval result, this is the area of the ellipse.
    /// </summary>
    public double AreaMM2 { get; set; }


    /// <summary>
    /// Number of angular bins that were valid for the inscribed geometry search. This is a measure of how many orientations were considered during the search process.
    /// </summary>
    public int ValidAngularBins { get; set; }

    /// <summary>True when the search was anchored to the fitted block centreline.</summary>
    public bool CenterlineAnchorUsed { get; set; }

    /// <summary>Fitted block centreline in the machine frame (mm).</summary>
    public double CenterlineTailXMM { get; set; }
    public double CenterlineTailYMM { get; set; }
    public double CenterlineHeadXMM { get; set; }
    public double CenterlineHeadYMM { get; set; }

    /// <summary>Largest tail/head distance between the selected axis and the fitted centreline.</summary>
    public double CenterlineDeviationMM { get; set; }

    /// <summary>
    /// Set when the certified search ran (InscribedGeometry.UseCertifiedCylinderSearch):
    /// no position within machine travel can score more than this many mm better
    /// than the selected one. 0 means proven best within the search tolerance.
    /// </summary>
    public double? CertifiedGapMM { get; set; }

    /// <summary>Positions tested by the certified search.</summary>
    public int CertifiedEvaluations { get; set; }

    /// <summary>True when the certified search found a better position than the grid search.</summary>
    public bool CertifiedSearchImproved { get; set; }

    /// <summary>
    /// Number of candidate orientations that were tested during the inscribed geometry search.
    /// This is a measure of how many potential orientations were evaluated to find the optimal inscribed geometry.
    /// </summary>
    public int TestedCandidates { get; set; }

    /// <summary>
    /// Number of candidate orientations that were considered at a coarse level during the inscribed geometry search.
    /// </summary>
    public int CoarseCandidates { get; set; }

    /// <summary>
    /// Number of candidate orientations that were considered at a medium level during the inscribed geometry search.
    /// </summary>
    public int MediumCandidates { get; set; }


    /// <summary>
    /// Number of candidate orientations that were considered at a fine level during the inscribed geometry search.
    /// </summary>
    public int FineCandidates { get; set; }


    /// <summary>
    /// Maximum spindle movement in millimeters required to achieve the inscribed geometry. This value
    /// represents the maximum distance the spindle must move to accommodate the inscribed shape,
    /// which can be important for assessing the feasibility of the machining process.
    /// </summary>
    public double MaxSpindleMovementMM { get; set; }


    /// <summary>
    /// Gets the name of the strategy used for the inscribed geometry search based on the kind of geometry.
    /// If the kind is a cylinder, it returns "Largest Inscribed Cylinder"; if it's an oval, it returns "Largest Inscribed Oval".
    /// </summary>
    public string StrategyName =>
        Kind == InscribedGeometryKind.Cylinder
            ? "Largest Inscribed Cylinder"
            : "Largest Inscribed Oval";
}
