using OptimizationXY.Charger.Models.Optimization.Recovery;

namespace OptimizationXY.Charger.Services.Optimization.Recovery;

/// <summary>
/// Selects the best recovery strategy based on simulated circular virtual peel recovery metrics.
/// </summary>
public static class RecoveryStrategySelector
{
    /// <summary>
    /// Selects the best recovery strategy from a list of candidates based on their simulated circular virtual peel recovery metrics.
    /// </summary>
    /// <param name="candidates">The list of recovery candidate results to evaluate.</param>
    /// <param name="preferLowerMovementOnExactTie">Indicates whether to prefer candidates with lower movement in the event of an exact tie.</param>
    /// <returns>The result of the recovery optimization, including the selected candidate and the reason for the selection.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no recovery candidates are provided.</exception>
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

    /// <summary>
    /// Determines if the candidate is better than the current best candidate based on recovery metrics.
    /// </summary>
    /// <param name="candidate">The recovery candidate result to evaluate.</param>
    /// <param name="current">The current best recovery candidate result.</param>
    /// <param name="preferLowerMovementOnExactTie">Indicates whether to prefer candidates with lower movement in the event of an exact tie.</param>
    /// <returns>True if the candidate is better than the current best candidate; otherwise, false.</returns>
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

    /// <summary>
    /// Builds a descriptive reason for why the selected recovery candidate was chosen based on its recovery metrics.
    /// </summary>
    /// <param name="selected">The selected recovery candidate result.</param>
    /// <returns>A string describing the reason for the selection.</returns>
    private static string BuildReason(RecoveryCandidateResult selected) =>
        $"{selected.StrategyName} produced the best physical circular virtual peel: " +
        $"{selected.Peel.UsableFullSheets} usable full 50-inch sheets, " +
        $"{selected.Peel.UsableVeneerLengthMM:F1} mm usable veneer, " +
        $"{selected.Peel.RejectedFullSheets} rejected sheets, " +
        $"{selected.Peel.SevereWaneAreaPercent:F2}% severe wane.";
}
