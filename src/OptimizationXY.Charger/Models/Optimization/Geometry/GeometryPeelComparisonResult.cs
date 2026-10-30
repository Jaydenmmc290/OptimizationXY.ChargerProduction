using OptimizationXY.Charger.Models.Analysis;


// GeometryPeelComparisonResult.cs - CompareInscribedGeometry mode's result.
namespace OptimizationXY.Charger.Models.Optimization.Geometry;

/// <summary>
/// Result of the CompareInscribedGeometry mode (production): the cylinder
/// search result and the oval search result, the virtual peel of each, and
/// which one was chosen and why (see GeometryPeelSelector).
/// </summary>
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
    /// Gets or sets which geometry won the peel comparison; its spindle position is the one sent to the PLC.
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
    /// Gets or sets a plain-language reason for the choice (logged and saved with the scan).
    /// </summary>
    public string SelectionReason { get; set; } = string.Empty;
}
