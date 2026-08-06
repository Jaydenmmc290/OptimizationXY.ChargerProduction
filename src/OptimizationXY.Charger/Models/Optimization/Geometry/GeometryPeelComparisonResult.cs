using OptimizationXY.Charger.Models.Analysis;


/// <summary>
/// Represents the result of a comparison between different inscribed geometries and their corresponding peel simulations.
/// </summary>
namespace OptimizationXY.Charger.Models.Optimization.Geometry;

public sealed class GeometryPeelComparisonResult
{

    /// <summary>
    /// Gets or sets the result of the inscribed geometry analysis for a cylinder.
    /// </summary>
    public InscribedGeometryResult Cylinder { get; set; } = new()
    {
        Kind = InscribedGeometryKind.Cylinder
    };

    /// <summary>
    /// Gets or sets the result of the inscribed geometry analysis for an oval.
    /// </summary>
    public InscribedGeometryResult Oval { get; set; } = new()
    {
        Kind = InscribedGeometryKind.Oval
    };

    /// <summary>
    /// Gets or sets the result of the peel simulation for a cylinder.
    /// </summary>
    public PeelSimulationResult CylinderPeel { get; set; } = new();

    /// <summary>
    /// Gets or sets the result of the peel simulation for an oval.
    /// </summary>
    public PeelSimulationResult OvalPeel { get; set; } = new();


    /// <summary>
    /// Gets or sets the selected inscribed geometry kind based on the comparison results.
    /// </summary>
    public InscribedGeometryKind SelectedKind { get; set; }


    /// <summary>
    /// Gets the selected inscribed geometry result based on the selected kind.
    /// </summary>
    public InscribedGeometryResult SelectedGeometry =>
        SelectedKind == InscribedGeometryKind.Cylinder
            ? Cylinder
            : Oval;

    /// <summary>
    /// Gets the selected peel simulation result based on the selected kind.
    /// </summary>
    public PeelSimulationResult SelectedPeel =>
        SelectedKind == InscribedGeometryKind.Cylinder
            ? CylinderPeel
            : OvalPeel;

    /// <summary>
    /// Gets or sets the reason for the selection of the inscribed geometry kind.
    /// </summary>
    public string SelectionReason { get; set; } = string.Empty;
}
