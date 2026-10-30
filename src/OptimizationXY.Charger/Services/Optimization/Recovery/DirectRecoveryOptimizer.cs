using OptimizationXY.Charger.Models.Geometry;
using OptimizationXY.Charger.Models.Optimization.Recovery;
using OptimizationXY.Charger.Models.Optimization.Spindle;
using OptimizationXY.Charger.Models.Settings;

namespace OptimizationXY.Charger.Services.Optimization.Recovery;

/// <summary>
/// Performs a bounded, metrics-only local refinement around the candidate axes
/// produced by the baseline, penalty, cylinder, and oval strategies. Candidate
/// ranking is based directly on simulated usable veneer recovery, not penalty
/// score. Search remains XY-only; rotation is never commanded here.
/// Used only in RecoveryOptimized mode. Around EACH seed, every combination of
/// tail X/Y and head X/Y changes in +/- DirectRefinementRadiusMM steps of
/// DirectRefinementStepMM is tried (e.g. 3 x 3 x 3 x 3 = 81 per seed), small
/// changes first, until MaximumDirectRecoveryCandidates unique positions have
/// been attempted.
/// </summary>
public static class DirectRecoveryOptimizer
{
    /// <summary>
    /// Finds the best spindle movement within a bounded XY search space around the provided seed movements.
    /// </summary>
    /// <param name="radiusField">The list of binned radius field points used for evaluation.</param>
    /// <param name="seedMovements">The list of seed spindle movements to refine.</param>
    /// <param name="settings">The application settings containing machine limits and optimization parameters.</param>
    /// <param name="token">A cancellation token to observe while performing the search.</param>
    /// <returns>The result of the direct recovery search, including the best movement and associated metrics.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no seed movements are provided.</exception>
    public static DirectRecoverySearchResult FindBest(
        IReadOnlyList<BinnedRadiusFieldPoint> radiusField,
        IReadOnlyList<OptimizedSpindleMovement> seedMovements,
        AppSettings settings,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(radiusField);
        ArgumentNullException.ThrowIfNull(seedMovements);
        ArgumentNullException.ThrowIfNull(settings);

        if (seedMovements.Count == 0)
            throw new InvalidOperationException("Direct recovery refinement requires at least one seed movement.");

        RecoveryOptimizationSettings recoverySettings =
            settings.RecoveryOptimization;

        double radiusMM = recoverySettings.DirectRefinementRadiusMM;
        double stepMM = recoverySettings.DirectRefinementStepMM;
        int maximumCandidates = recoverySettings.MaximumDirectRecoveryCandidates;

        double[] deltas = BuildDeltas(radiusMM, stepMM);
        // Positions already tried (several seeds can lead to the same position).
        var testedKeys = new HashSet<string>(StringComparer.Ordinal);

        DirectRecoverySearchResult? best = null;
        int attempted = 0;
        int evaluated = 0;
        int rejected = 0;

        foreach (OptimizedSpindleMovement seed in seedMovements)
        {
            foreach (double tailDx in deltas)
            foreach (double tailDy in deltas)
            foreach (double headDx in deltas)
            foreach (double headDy in deltas)
            {
                token.ThrowIfCancellationRequested();

                if (attempted >= maximumCandidates)
                    // Candidate budget used up: stop searching all seeds.
                    goto SearchComplete;

                var movement = new OptimizedSpindleMovement
                {
                    TailMoveXMM = seed.TailMoveXMM + tailDx,
                    TailMoveYMM = seed.TailMoveYMM + tailDy,
                    HeadMoveXMM = seed.HeadMoveXMM + headDx,
                    HeadMoveYMM = seed.HeadMoveYMM + headDy
                };

                string key = BuildMovementKey(movement);
                if (!testedKeys.Add(key))
                    continue;

                attempted++;

                if (!WithinMachineLimits(movement, settings.MachineLimits))
                {
                    rejected++;
                    continue;
                }

                RecoverySearchMetrics metrics =
                    VeneerRecoveryMetricsEvaluator.Evaluate(
                        radiusField,
                        settings.PeelView,
                        settings.UsableRadius,
                        settings.Wane,
                        recoverySettings,
                        movement);

                evaluated++;

                if (!metrics.IsValid)
                {
                    rejected++;
                    continue;
                }

                double movementMagnitude = MaxMovement(movement);

                if (best is null ||
                    VeneerRecoveryMetricsEvaluator.IsBetter(
                        metrics,
                        movementMagnitude,
                        best.Metrics,
                        MaxMovement(best.Movement),
                        recoverySettings.PreferLowerMovementOnExactTie))
                {
                    best = new DirectRecoverySearchResult
                    {
                        Movement = movement,
                        Metrics = metrics
                    };
                }
            }
        }

    SearchComplete:
        if (best is null)
        {
            throw new InvalidOperationException(
                "Direct veneer-recovery refinement did not produce a valid candidate within machine limits.");
        }

        best.EvaluatedCandidates = evaluated;
        best.RejectedCandidates = rejected;
        best.SearchSummary =
            $"Direct recovery refinement attempted {attempted} unique positions, " +
            $"evaluated {evaluated}, and rejected {rejected}; best estimate was " +
            $"{best.Metrics.UsableFullSheets} " +
            "usable full 50-inch sheets.";

        return best;
    }

    /// <summary>
    /// Step values from -radius to +radius (always including 0 and both ends),
    /// ordered smallest change first so the budget is spent near the seed first.
    /// </summary>
    /// <param name="radiusMM">The radius of the search space in millimeters.</param>
    /// <param name="stepMM">The step size for generating delta values in millimeters.</param>
    /// <returns>An array of delta values for the search space.</returns>
    private static double[] BuildDeltas(double radiusMM, double stepMM)
    {
        radiusMM = Math.Max(0.0, radiusMM);
        stepMM = Math.Max(0.001, stepMM);

        if (radiusMM <= 0.000001)
            return [0.0];

        var values = new List<double>();
        for (double value = -radiusMM;
             value <= radiusMM + stepMM * 0.25;
             value += stepMM)
        {
            values.Add(Math.Clamp(value, -radiusMM, radiusMM));
        }

        if (!values.Any(value => Math.Abs(value) <= 0.000001))
            values.Add(0.0);

        if (!values.Any(value => Math.Abs(value - radiusMM) <= 0.000001))
            values.Add(radiusMM);

        if (!values.Any(value => Math.Abs(value + radiusMM) <= 0.000001))
            values.Add(-radiusMM);

        return [.. values
            .DistinctBy(value => Math.Round(value, 6))
            .OrderBy(value => Math.Abs(value))
            .ThenBy(value => value)];
    }

    /// <summary>
    /// Determines whether the specified spindle movement is within the machine limits defined in the settings.
    /// </summary>
    /// <param name="movement">The spindle movement to evaluate.</param>
    /// <param name="limits">The machine limit settings to compare against.</param>
    /// <returns>True if the movement is within the machine limits; otherwise, false.</returns>
    private static bool WithinMachineLimits(
        OptimizedSpindleMovement movement,
        MachineLimitSettings limits) =>
        Math.Abs(movement.TailMoveXMM) <= limits.MaxTailMoveXMM &&
        Math.Abs(movement.TailMoveYMM) <= limits.MaxTailMoveYMM &&
        Math.Abs(movement.HeadMoveXMM) <= limits.MaxHeadMoveXMM &&
        Math.Abs(movement.HeadMoveYMM) <= limits.MaxHeadMoveYMM;

    /// <summary>
    /// Calculates the maximum absolute movement in any direction for the given spindle movement.
    /// </summary>
    /// <param name="movement">The spindle movement to evaluate.</param>
    /// <returns>The maximum absolute movement in any direction.</returns>
    private static double MaxMovement(OptimizedSpindleMovement movement) =>
        new[]
        {
            Math.Abs(movement.TailMoveXMM),
            Math.Abs(movement.TailMoveYMM),
            Math.Abs(movement.HeadMoveXMM),
            Math.Abs(movement.HeadMoveYMM)
        }.Max();

    /// <summary>
    /// Builds a unique key string for the given spindle movement, used to track tested candidates and avoid duplicates.
    /// </summary>
    /// <param name="movement">The spindle movement for which to build the key.</param>
    /// <returns>A unique key string representing the spindle movement.</returns>
    private static string BuildMovementKey(OptimizedSpindleMovement movement) =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{movement.TailMoveXMM:F4}|{movement.TailMoveYMM:F4}|{movement.HeadMoveXMM:F4}|{movement.HeadMoveYMM:F4}");
}
