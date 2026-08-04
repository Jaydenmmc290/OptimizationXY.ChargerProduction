namespace OptimizationXY.Charger.Models.Optimization;

/// <summary>
/// Selects how the final head/tail spindle position is chosen.
/// Geometry modes still run the complete measurement and defect-analysis
/// pipeline, but defect and penalty scores do not influence the position.
/// </summary>
public enum SpindleOptimizationMode
{
    PenaltyScored = 0,
    LargestInscribedCylinder = 1,
    LargestInscribedOval = 2,
    CompareInscribedGeometry = 3,
    RecoveryOptimized = 4
}
