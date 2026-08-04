using OptimizationXY.Charger.Models.Optimization.Recovery;

namespace OptimizationXY.Charger.Services.Optimization.Recovery;

public static class RecoveryStrategySelector
{
    public static RecoveryOptimizationResult Select(
        IReadOnlyList<RecoveryCandidateResult> candidates,
        bool preferLowerMovementOnExactTie)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
            throw new InvalidOperationException("No recovery candidates were provided.");

        RecoveryCandidateResult selected = candidates[0];

        for (int index = 1; index < candidates.Count; index++)
        {
            RecoveryCandidateResult candidate = candidates[index];
            if (IsBetter(candidate, selected, preferLowerMovementOnExactTie))
                selected = candidate;
        }

        return new RecoveryOptimizationResult
        {
            Selected = selected,
            Candidates = [.. candidates],
            SelectionReason = BuildReason(selected)
        };
    }

    private static bool IsBetter(
        RecoveryCandidateResult candidate,
        RecoveryCandidateResult current,
        bool preferLowerMovementOnExactTie)
    {
        if (candidate.Peel.UsableFullSheets != current.Peel.UsableFullSheets)
            return candidate.Peel.UsableFullSheets > current.Peel.UsableFullSheets;

        if (Math.Abs(candidate.Peel.UsableVeneerLengthMM - current.Peel.UsableVeneerLengthMM) > 0.001)
            return candidate.Peel.UsableVeneerLengthMM > current.Peel.UsableVeneerLengthMM;

        if (candidate.Peel.RejectedFullSheets != current.Peel.RejectedFullSheets)
            return candidate.Peel.RejectedFullSheets < current.Peel.RejectedFullSheets;

        if (Math.Abs(candidate.Peel.SevereWaneAreaPercent - current.Peel.SevereWaneAreaPercent) > 0.0001)
            return candidate.Peel.SevereWaneAreaPercent < current.Peel.SevereWaneAreaPercent;

        if (Math.Abs(candidate.Peel.TotalWaneAreaPercent - current.Peel.TotalWaneAreaPercent) > 0.0001)
            return candidate.Peel.TotalWaneAreaPercent < current.Peel.TotalWaneAreaPercent;

        return preferLowerMovementOnExactTie &&
               candidate.MaxMovementMM < current.MaxMovementMM;
    }

    private static string BuildReason(RecoveryCandidateResult selected) =>
        $"{selected.StrategyName} produced the best physical circular virtual peel: " +
        $"{selected.Peel.UsableFullSheets} usable full 50-inch sheets, " +
        $"{selected.Peel.UsableVeneerLengthMM:F1} mm usable veneer, " +
        $"{selected.Peel.RejectedFullSheets} rejected sheets, " +
        $"{selected.Peel.SevereWaneAreaPercent:F2}% severe wane.";
}
