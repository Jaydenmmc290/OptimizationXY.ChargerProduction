namespace OptimizationXY.Charger.Models.Optimization.Geometry;


/// <summary>
/// Which shape the inscribed-geometry search fits inside the log.
/// </summary>
public enum InscribedGeometryKind
{
    /// <summary>A round cylinder (same radius all round).</summary>
    Cylinder = 0,
    /// <summary>An elliptical (oval) cross-section with a major and minor semi-axis.</summary>
    Oval = 1
}
