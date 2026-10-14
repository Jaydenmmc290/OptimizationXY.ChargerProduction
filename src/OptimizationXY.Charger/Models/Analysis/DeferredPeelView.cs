namespace OptimizationXY.Charger.Models.Analysis;

/// <summary>
/// Which saved peel image a deferred render fills in.
/// </summary>
public enum PeelViewSlot
{
    Unwrapped,
    Cylinder,
    Oval,
    Baseline,
    Penalty,
    DirectRecovery
}

/// <summary>
/// A peel image to draw after the PLC write. The spindle position was chosen
/// from <see cref="DecisionMetrics"/> (numbers only); the image is drawn later
/// for the same axis, and its numbers are checked against these.
/// </summary>
public sealed record DeferredPeelView(
    PeelViewSlot Slot,
    PeelSimulationResult DecisionMetrics);
