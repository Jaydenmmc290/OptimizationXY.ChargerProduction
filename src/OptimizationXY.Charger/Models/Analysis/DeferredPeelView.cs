namespace OptimizationXY.Charger.Models.Analysis;

/// <summary>
/// Which saved peel image a deferred render fills in.
/// </summary>
public enum PeelViewSlot
{
    /// <summary>The unwrapped (flattened ribbon) peel picture for the chosen axis.</summary>
    Unwrapped,
    /// <summary>Peel picture for the largest-inscribed-cylinder axis.</summary>
    Cylinder,
    /// <summary>Peel picture for the largest-inscribed-oval axis.</summary>
    Oval,
    /// <summary>Peel picture for the baseline (fitted centreline) axis, for comparison.</summary>
    Baseline,
    /// <summary>Peel picture for the legacy penalty-scored search axis.</summary>
    Penalty,
    /// <summary>Peel picture for the direct-recovery search axis.</summary>
    DirectRecovery
}

/// <summary>
/// A peel image to draw AFTER the PLC write. Drawing images is slow, so to get
/// the spindle position to the PLC quickly the decision is made from
/// <see cref="DecisionMetrics"/> (numbers only, no image). The picture is
/// drawn later for exactly the same axis, and its numbers are checked against
/// these so the saved picture always matches what was sent.
/// </summary>
public sealed record DeferredPeelView(
    PeelViewSlot Slot,
    PeelSimulationResult DecisionMetrics);
