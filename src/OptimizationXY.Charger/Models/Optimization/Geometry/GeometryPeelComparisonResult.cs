using OptimizationXY.Charger.Models.Analysis;

namespace OptimizationXY.Charger.Models.Optimization.Geometry;

public sealed class GeometryPeelComparisonResult
{
    public InscribedGeometryResult Cylinder { get; set; } = new()
    {
        Kind = InscribedGeometryKind.Cylinder
    };

    public InscribedGeometryResult Oval { get; set; } = new()
    {
        Kind = InscribedGeometryKind.Oval
    };

    public PeelSimulationResult CylinderPeel { get; set; } = new();
    public PeelSimulationResult OvalPeel { get; set; } = new();

    public InscribedGeometryKind SelectedKind { get; set; }

    public InscribedGeometryResult SelectedGeometry =>
        SelectedKind == InscribedGeometryKind.Cylinder
            ? Cylinder
            : Oval;

    public PeelSimulationResult SelectedPeel =>
        SelectedKind == InscribedGeometryKind.Cylinder
            ? CylinderPeel
            : OvalPeel;

    public string SelectionReason { get; set; } = string.Empty;
}
