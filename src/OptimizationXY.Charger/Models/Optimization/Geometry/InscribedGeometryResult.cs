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

    public double MajorSemiAxisMM { get; set; }
    public double MinorSemiAxisMM { get; set; }
    public double OrientationDeg { get; set; }
    public double AreaMM2 { get; set; }

    public int ValidAngularBins { get; set; }
    public int TestedCandidates { get; set; }
    public int CoarseCandidates { get; set; }
    public int MediumCandidates { get; set; }
    public int FineCandidates { get; set; }

    public double MaxSpindleMovementMM { get; set; }

    public string StrategyName =>
        Kind == InscribedGeometryKind.Cylinder
            ? "Largest Inscribed Cylinder"
            : "Largest Inscribed Oval";
}
