using OptimizationXY.Charger.Models.Optimization.Spindle;

namespace OptimizationXY.Charger.Models.Optimization.Geometry;

/// <summary>
/// Result of a geometry-only spindle search. No defect, adaptive-risk,
/// movement-penalty, or final penalty score is used to select this result.
/// </summary>
/// <remarks>
/// "Inscribed" means the shape fits completely INSIDE the measured log along
/// the whole block, so it is the guaranteed wood around that spindle axis.
/// The search compares many candidate head/tail spindle positions; this is
/// the winning one. Lengths in millimetres.
/// </remarks>
public sealed class InscribedGeometryResult
{
    /// <summary>Cylinder or oval search.</summary>
    public InscribedGeometryKind Kind { get; set; }

    /// <summary>The selected head/tail spindle position and the move from the current axis.</summary>
    public OptimizedSpindleMovement SpindleMovement { get; set; } = new();

    /// <summary>
    /// Guaranteed circular radius for the cylinder result. For an oval result,
    /// this is the minor semi-axis, which is the guaranteed circular radius
    /// available to a conventional fixed-knife rotary lathe.
    /// </summary>
    public double GuaranteedCircularRadiusMM { get; set; }


    /// <summary>
    /// Guaranteed major (long) semi-axis for the oval result. For a cylinder result
    /// this equals the radius.
    /// </summary>
    public double MajorSemiAxisMM { get; set; }

    /// <summary>
    /// Guaranteed minor (short) semi-axis for the oval result. For a cylinder result
    /// this equals the radius.
    /// </summary>
    public double MinorSemiAxisMM { get; set; }

    /// <summary>
    /// Direction of the oval's major semi-axis, in degrees (radius-field angle frame).
    /// Always 0 for a cylinder result (a circle has no direction).
    /// </summary>
    public double OrientationDeg { get; set; }

    /// <summary>
    /// Guaranteed area of the inscribed geometry in square millimeters. For a cylinder result,
    /// this is the area of the circle. For an oval result, this is the area of the ellipse.
    /// </summary>
    public double AreaMM2 { get; set; }


    /// <summary>
    /// Number of angle bins around the log that had valid radius data for the
    /// search. Few valid bins means the answer rests on less of the surface.
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
    /// Total number of candidate spindle positions evaluated (all search levels).
    /// </summary>
    public int TestedCandidates { get; set; }

    /// <summary>
    /// Candidate positions tested in the coarse (wide, big-step) grid.
    /// </summary>
    public int CoarseCandidates { get; set; }

    /// <summary>
    /// Candidate positions tested in the medium refinement around the coarse winner.
    /// </summary>
    public int MediumCandidates { get; set; }


    /// <summary>
    /// Candidate positions tested in the fine refinement around the medium winner.
    /// </summary>
    public int FineCandidates { get; set; }


    /// <summary>
    /// Largest single spindle move (head or tail, mm) needed to reach the selected
    /// position from the current axis.
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
