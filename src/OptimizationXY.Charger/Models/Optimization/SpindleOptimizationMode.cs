namespace OptimizationXY.Charger.Models.Optimization;

/// <summary>
/// Selects how the final head/tail spindle position is chosen.
/// Geometry modes still run the complete measurement and defect-analysis
/// pipeline, but defect and penalty scores do not influence the position.
/// </summary>
/// <remarks>
/// Saved as AppSettings.OptimizationMode. Production runs
/// <see cref="CompareInscribedGeometry"/>. The numeric values are stored in
/// settings files and SQL records, so do not renumber them.
/// </remarks>
public enum SpindleOptimizationMode
{
    /// <summary>
    /// Legacy: search spindle positions and pick the one with the lowest penalty
    /// score built from defects, wane, adaptive risk and movement size.
    /// </summary>
    PenaltyScored = 0,
    /// <summary>
    /// Pick the head/tail axis around which the largest round cylinder fits
    /// completely inside the measured log (biggest guaranteed round core).
    /// </summary>
    LargestInscribedCylinder = 1,
    /// <summary>
    /// Pick the axis around which the largest oval (ellipse) cross-section fits.
    /// The lathe still peels in a circle around that axis; the oval is only the
    /// way the axis is chosen.
    /// </summary>
    LargestInscribedOval = 2,
    /// <summary>
    /// PRODUCTION MODE. Run both the cylinder and the oval search, virtually peel
    /// the log around each axis, and keep the one that gives more usable veneer
    /// (usable full sheets, then usable length, rejected sheets, severe wane,
    /// total wane).
    /// </summary>
    CompareInscribedGeometry = 3,
    /// <summary>
    /// Run all strategies (baseline, penalty, cylinder, oval, direct recovery
    /// search), virtually peel each, and keep the best veneer recovery.
    /// </summary>
    RecoveryOptimized = 4
} 
