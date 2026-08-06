using OptimizationXY.Charger.Models.Defects;
using OptimizationXY.Charger.Models.Geometry;
using OptimizationXY.Charger.Models.Optimization.Results;
using OptimizationXY.Charger.Models.Optimization.Spindle;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Optimization.Movement;
using OptimizationXY.Charger.Services.Optimization.Scoring;
using OptimizationXY.Charger.Services.Optimization.Wane;
using System.Diagnostics;

namespace OptimizationXY.Charger.Services.Optimization.Search;
/// <summary>
/// This class provides methods to optimize the spindle-axis setup for a charger system using a coarse-to-fine search strategy.
/// /// It evaluates candidate setups based on wane scores, defect penalties, and spindle movement, applying scoring settings to determine the best configuration.
public static class ChargerOptimizer
{
    
 
    public static ChargerOptimizationResult FindBestSetupCoarseToFine(
        List<BinnedRadiusFieldPoint> binnedRadiusField,
        List<RadiusDefectPatch> patches,
        double baselineFinalScore,
        OptimizationSettings optimizationSettings,
        DefectSettings defectSettings,
        WaneSettings waneSettings,
        ScoringSettings scoringSettings,
        UsableRadiusSettings usableRadiusSettings)
    {
        ValidateInputs(
            binnedRadiusField,
            patches,
            optimizationSettings,
            defectSettings,
            scoringSettings);

        double baselineWaneScore =
            SpindleAxisWaneOptimizer.ComputeAxisAdjustedWaneScore(
                binnedRadiusField,
                tailXMM: 0.0,
                tailYMM: 0.0,
                headXMM: 0.0,
                headYMM: 0.0,
                minAngleBinsPerSlice: waneSettings.MinAngleBinsPerWaneSlice);

        double baselineDefectPenalty =
            PatchAwareWanePenalty.ComputeSpindleAxisAdjusted(
                patches,
                tailXMM: 0.0,
                tailYMM: 0.0,
                headXMM: 0.0,
                headYMM: 0.0,
                depthWeight: defectSettings.PatchDepthWeight,
                cellWeight: defectSettings.PatchCellWeight,
                severityWeight: defectSettings.PatchSeverityWeight,
                spindleReliefWeight: defectSettings.SpindleReliefWeight);

        double baselineSearchScore = ComputeCandidateAwareSearchScore(
            candidateWaneScore: baselineWaneScore,
            baselineWaneScore: baselineWaneScore,
            candidateDefectPenalty: baselineDefectPenalty,
            baselineDefectPenalty: baselineDefectPenalty,
            maxSpindleMovementMM: 0.0,
            maxMoveForPenaltyMM: optimizationSettings.CoarseMaxMoveMM,
            optimizationSettings: optimizationSettings,
            scoringSettings: scoringSettings);

        // Pass 1: wide but rough.
        var coarse = FindBestSetup(
            binnedRadiusField,
            patches,
            baselineSearchScore,
            baselineWaneScore,
            baselineDefectPenalty,
            searchRangeMM: optimizationSettings.CoarseMaxMoveMM,
            offsetStepMM: optimizationSettings.CoarseStepMM,
            maxMoveForPenaltyMM: optimizationSettings.CoarseMaxMoveMM,
            optimizationSettings: optimizationSettings,
            defectSettings: defectSettings,
            waneSettings: waneSettings,
            scoringSettings: scoringSettings,
            usableRadiusSettings: usableRadiusSettings);

        // Pass 2: medium refine around coarse winner.
        var medium = FindBestSetupAround(
            binnedRadiusField,
            patches,
            baselineSearchScore,
            baselineWaneScore,
            baselineDefectPenalty,
            center: coarse,
            radiusMM: optimizationSettings.MediumSearchRadiusMM,
            stepMM: optimizationSettings.MediumStepMM,
            maxMoveForPenaltyMM: optimizationSettings.CoarseMaxMoveMM,
            optimizationSettings: optimizationSettings,
            defectSettings: defectSettings,
            waneSettings: waneSettings,
            scoringSettings: scoringSettings,
            usableRadiusSettings: usableRadiusSettings);

        // Pass 3: fine refine around medium winner.
        var fine = FindBestSetupAround(
            binnedRadiusField,
            patches,
            baselineSearchScore,
            baselineWaneScore,
            baselineDefectPenalty,
            center: medium,
            radiusMM: optimizationSettings.FineSearchRadiusMM,
            stepMM: optimizationSettings.FineStepMM,
            maxMoveForPenaltyMM: optimizationSettings.CoarseMaxMoveMM,
            optimizationSettings: optimizationSettings,
            defectSettings: defectSettings,
            waneSettings: waneSettings,
            scoringSettings: scoringSettings,
            usableRadiusSettings: usableRadiusSettings);

        fine.CoarseCandidates = coarse.TestedCandidates;
        fine.MediumCandidates = medium.TestedCandidates;
        fine.FineCandidates = fine.TestedCandidates;

        fine.TestedCandidates =
            fine.CoarseCandidates +
            fine.MediumCandidates +
            fine.FineCandidates;

        return fine;
    }

    /// <summary>
    /// Backward-compatible overload.
    /// Existing calls still compile, but the main pipeline should pass active/adaptive scoringSettings.
    /// </summary>
    public static ChargerOptimizationResult FindBestSetupCoarseToFine(
 List<BinnedRadiusFieldPoint> binnedRadiusField,
 List<RadiusDefectPatch> patches,
 double baselineFinalScore,
 OptimizationSettings optimizationSettings,
 DefectSettings defectSettings,
 WaneSettings waneSettings)
    {
        return FindBestSetupCoarseToFine(
            binnedRadiusField,
            patches,
            baselineFinalScore,
            optimizationSettings,
            defectSettings,
            waneSettings,
            new ScoringSettings(),
            new UsableRadiusSettings());
    }

    /// <summary>
    /// Backward-compatible overload.
    /// </summary>
    public static ChargerOptimizationResult FindBestSetupCoarseToFine(
        List<BinnedRadiusFieldPoint> binnedRadiusField,
        List<RadiusDefectPatch> patches,
        double baselineFinalScore)
    {
        return FindBestSetupCoarseToFine(
            binnedRadiusField,
            patches,
            baselineFinalScore,
            new OptimizationSettings(),
            new DefectSettings(),
            new WaneSettings(),
            new ScoringSettings(),
            new UsableRadiusSettings());
    }

    /// <summary>
    /// Wide grid search.
    /// Tests center offset and sweep correction.
    /// </summary>
    public static ChargerOptimizationResult FindBestSetup(
        List<BinnedRadiusFieldPoint> binnedRadiusField,
        List<RadiusDefectPatch> patches,
        double baselineFinalScore,
        double baselineWaneScore,
        double baselineDefectPenalty,
        double searchRangeMM,
        double offsetStepMM,
        double maxMoveForPenaltyMM,
        OptimizationSettings optimizationSettings,
        DefectSettings defectSettings,
        WaneSettings waneSettings,
        ScoringSettings scoringSettings,
        UsableRadiusSettings usableRadiusSettings)
    {
        if (binnedRadiusField == null || binnedRadiusField.Count == 0)
            throw new InvalidOperationException("Binned radius field is empty.");

        if (patches == null)
            throw new InvalidOperationException("Defect patches are missing.");

        if (offsetStepMM <= 0)
            throw new InvalidOperationException("Optimizer offset step must be greater than zero.");

        ChargerOptimizationResult? best = null;
        int tested = 0;

        foreach (double offsetX in Range(0.0, searchRangeMM, offsetStepMM))
        {
            foreach (double offsetY in Range(0.0, searchRangeMM, offsetStepMM))
            {
                foreach (double sweepX in Range(0.0, searchRangeMM, offsetStepMM))
                {
                    foreach (double sweepY in Range(0.0, searchRangeMM, offsetStepMM))
                    {
                        var candidate = EvaluateCandidate(
                            binnedRadiusField,
                            patches,
                            offsetX,
                            offsetY,
                            sweepX,
                            sweepY,
                            maxMoveForPenaltyMM,
                            baselineWaneScore,
                            baselineDefectPenalty,
                            optimizationSettings,
                            defectSettings,
                            waneSettings,
                            scoringSettings,
                            usableRadiusSettings);

                        tested++;

                        if (tested % 500 == 0)
                            Debug.WriteLine($"Charger optimizer tested: {tested}");

                        candidate.TestedCandidates = tested;

                        if (best == null || candidate.FinalScore < best.FinalScore)
                            best = candidate;
                    }
                }
            }
        }

        if (best == null)
            throw new InvalidOperationException("No charger optimization result found.");

        ApplyImprovementPercent(best, baselineFinalScore);

        best.TestedCandidates = tested;

        return best;
    }

    /// <summary>
    /// Refines the search around the previous best setup.
    /// </summary>
    private static ChargerOptimizationResult FindBestSetupAround(
        List<BinnedRadiusFieldPoint> binnedRadiusField,
        List<RadiusDefectPatch> patches,
        double baselineFinalScore,
        double baselineWaneScore,
        double baselineDefectPenalty,
        ChargerOptimizationResult center,
        double radiusMM,
        double stepMM,
        double maxMoveForPenaltyMM,
        OptimizationSettings optimizationSettings,
        DefectSettings defectSettings,
        WaneSettings waneSettings,
        ScoringSettings scoringSettings,
        UsableRadiusSettings usableRadiusSettings)
    {
        if (center == null)
            throw new InvalidOperationException("Optimizer center result is missing.");

        if (center.SpindleMovement == null)
            throw new InvalidOperationException("Optimizer center movement is missing.");

        if (stepMM <= 0)
            throw new InvalidOperationException("Optimizer refine step must be greater than zero.");

        ChargerOptimizationResult? best = null;
        int tested = 0;

        double centerSweepX = center.SpindleMovement.SweepCorrectionXMM;
        double centerSweepY = center.SpindleMovement.SweepCorrectionYMM;

        foreach (double offsetX in Range(center.OffsetXMM, radiusMM, stepMM))
        {
            foreach (double offsetY in Range(center.OffsetYMM, radiusMM, stepMM))
            {
                foreach (double sweepX in Range(centerSweepX, radiusMM, stepMM))
                {
                    foreach (double sweepY in Range(centerSweepY, radiusMM, stepMM))
                    {
                        var candidate = EvaluateCandidate(
                            binnedRadiusField,
                            patches,
                            offsetX,
                            offsetY,
                            sweepX,
                            sweepY,
                            maxMoveForPenaltyMM,
                            baselineWaneScore,
                            baselineDefectPenalty,
                            optimizationSettings,
                            defectSettings,
                            waneSettings,
                            scoringSettings,
                            usableRadiusSettings);

                        tested++;
                        candidate.TestedCandidates = tested;

                        if (best == null || candidate.FinalScore < best.FinalScore)
                            best = candidate;
                    }
                }
            }
        }

        if (best == null)
            return center;

        ApplyImprovementPercent(best, baselineFinalScore);

        best.TestedCandidates = tested;

        return best;
    }

    private static ChargerOptimizationResult EvaluateCandidate(
List<BinnedRadiusFieldPoint> binnedRadiusField,
List<RadiusDefectPatch> patches,
double offsetXMM,
double offsetYMM,
double sweepXMM,
double sweepYMM,
double maxMoveForPenaltyMM,
double baselineWaneScore,
double baselineDefectPenalty,
OptimizationSettings optimizationSettings,
DefectSettings defectSettings,
WaneSettings waneSettings,
ScoringSettings scoringSettings,
UsableRadiusSettings usableRadiusSettings)
    {
        var movement = SpindleMovementBuilder.BuildFromSweep(
            offsetXMM,
            offsetYMM,
            sweepXMM,
            sweepYMM);

        double waneScore =
            SpindleAxisWaneOptimizer.ComputeAxisAdjustedWaneScore(
                binnedRadiusField,
                movement.TailMoveXMM,
                movement.TailMoveYMM,
                movement.HeadMoveXMM,
                movement.HeadMoveYMM,
                minAngleBinsPerSlice: waneSettings.MinAngleBinsPerWaneSlice);

        double defectPenalty =
            PatchAwareWanePenalty.ComputeSpindleAxisAdjusted(
                patches,
                movement.TailMoveXMM,
                movement.TailMoveYMM,
                movement.HeadMoveXMM,
                movement.HeadMoveYMM,
                depthWeight: defectSettings.PatchDepthWeight,
                cellWeight: defectSettings.PatchCellWeight,
                severityWeight: defectSettings.PatchSeverityWeight,
                spindleReliefWeight: defectSettings.SpindleReliefWeight);

        double maxSpindleMovementMM = MaxSpindleMovementMM(movement);

        double finalScore;

        if (optimizationSettings.UseCandidateAwareSearchScore)
        {
            finalScore =
                ComputeCandidateAwareSearchScore(
                    candidateWaneScore: waneScore,
                    baselineWaneScore: baselineWaneScore,
                    candidateDefectPenalty: defectPenalty,
                    baselineDefectPenalty: baselineDefectPenalty,
                    maxSpindleMovementMM: maxSpindleMovementMM,
                    maxMoveForPenaltyMM: maxMoveForPenaltyMM,
                    optimizationSettings: optimizationSettings,
                    scoringSettings: scoringSettings);
        }
        else
        {
            double movementPenalty = ComputeMovementPenalty(
                offsetXMM,
                offsetYMM,
                maxMoveForPenaltyMM,
                optimizationSettings.MovementPenaltyWeight);

            double sweepPenalty = ComputeMovementPenalty(
                sweepXMM,
                sweepYMM,
                maxMoveForPenaltyMM,
                optimizationSettings.MovementPenaltyWeight);

            finalScore =
                waneScore +
                defectPenalty +
                movementPenalty +
                sweepPenalty;
        }

        return new ChargerOptimizationResult
        {
            OffsetXMM = offsetXMM,
            OffsetYMM = offsetYMM,

            WaneScore = waneScore,
            DefectPenalty = defectPenalty,
            MovementPenalty = ComputeCandidateMovementContribution(
                maxSpindleMovementMM,
                maxMoveForPenaltyMM,
                scoringSettings),
            FinalScore = finalScore,

            SpindleMovement = movement
        };
    }

    private static double ComputeCandidateAwareSearchScore(
        double candidateWaneScore,
        double baselineWaneScore,
        double candidateDefectPenalty,
        double baselineDefectPenalty,
        double maxSpindleMovementMM,
        double maxMoveForPenaltyMM,
        OptimizationSettings optimizationSettings,
        ScoringSettings scoringSettings)
    {
        // Candidate wane risk is allowed above 100 because a candidate can make balance worse.
        double waneRiskPercent = baselineWaneScore > 0.000001
            ? Math.Max(0.0, candidateWaneScore / baselineWaneScore * 100.0)
            : 0.0;

        // Low-wood risk is remaining risk compared with current setup.
        // 100 = no relief, 50 = half remaining, 0 = fully relieved.
        double lowWoodRiskPercent = baselineDefectPenalty > 0.000001
            ? Math.Clamp(candidateDefectPenalty / baselineDefectPenalty * 100.0, 0.0, 200.0)
            : 0.0;

        double movementRiskPercent = maxMoveForPenaltyMM > 0.000001
            ? Math.Clamp(maxSpindleMovementMM / maxMoveForPenaltyMM * 100.0, 0.0, 100.0)
            : 0.0;

        double score =
            waneRiskPercent * (scoringSettings.WaneBalanceWeightPercent / 100.0) +
            lowWoodRiskPercent * (scoringSettings.LowWoodRiskWeightPercent / 100.0) +
            movementRiskPercent * (scoringSettings.SpindleMovementWeightPercent / 100.0);

        // Guardrail: even on a bad low-wood log, do not let the optimizer destroy
        // wane balance unless the adaptive profile explicitly allows more sacrifice.
        double allowedWaneScore =
            baselineWaneScore *
            (1.0 + optimizationSettings.MaximumAllowedWaneIncreasePercent / 100.0);

        if (baselineWaneScore > 0.000001 && candidateWaneScore > allowedWaneScore)
        {
            double excessWanePercent =
                ((candidateWaneScore - allowedWaneScore) / baselineWaneScore) * 100.0;

            score += excessWanePercent * optimizationSettings.WaneGuardPenaltyWeight;
        }

        return score;
    }

    private static double ComputeCandidateMovementContribution(
        double maxSpindleMovementMM,
        double maxMoveForPenaltyMM,
        ScoringSettings scoringSettings)
    {
        double movementRiskPercent = maxMoveForPenaltyMM > 0.000001
            ? Math.Clamp(maxSpindleMovementMM / maxMoveForPenaltyMM * 100.0, 0.0, 100.0)
            : 0.0;

        return movementRiskPercent * (scoringSettings.SpindleMovementWeightPercent / 100.0);
    }

    private static double MaxSpindleMovementMM(OptimizedSpindleMovement movement)
    {
        double tail = Math.Sqrt(
            movement.TailMoveXMM * movement.TailMoveXMM +
            movement.TailMoveYMM * movement.TailMoveYMM);

        double head = Math.Sqrt(
            movement.HeadMoveXMM * movement.HeadMoveXMM +
            movement.HeadMoveYMM * movement.HeadMoveYMM);

        return Math.Max(tail, head);
    }

    private static void ApplyImprovementPercent(
        ChargerOptimizationResult result,
        double baselineFinalScore)
    {
        if (baselineFinalScore <= 0.001)
        {
            result.ImprovementPercent = 0.0;
            result.IsRecommended = false;
            return;
        }

        result.ImprovementPercent =
            ((baselineFinalScore - result.FinalScore) / baselineFinalScore) * 100.0;

        // Final recommendation threshold is handled in LogProcessingPipeline
        // because the pipeline uses the operator-facing uniform risk score.
        result.IsRecommended = result.ImprovementPercent > 0.0;
    }

    private static double ComputeMovementPenalty(
        double offsetXMM,
        double offsetYMM,
        double maxOffsetMM,
        double weight)
    {
        double movement =
            Math.Sqrt(offsetXMM * offsetXMM + offsetYMM * offsetYMM);

        if (maxOffsetMM <= 0.001)
            return 0.0;

        return (movement / maxOffsetMM) * weight;
    }

    private static IEnumerable<double> Range(
        double center,
        double radius,
        double step)
    {
        if (step <= 0)
            throw new InvalidOperationException("Range step must be greater than zero.");

        for (double v = center - radius; v <= center + radius + 0.0001; v += step)
            yield return Math.Round(v, 4);
    }

    private static void ValidateInputs(
        List<BinnedRadiusFieldPoint> binnedRadiusField,
        List<RadiusDefectPatch> patches,
        OptimizationSettings optimizationSettings,
        DefectSettings defectSettings,
        ScoringSettings scoringSettings)
    {
        if (binnedRadiusField == null || binnedRadiusField.Count == 0)
            throw new InvalidOperationException("Binned radius field is empty.");

        if (patches == null)
            throw new InvalidOperationException("Defect patches are missing.");

        if (optimizationSettings == null)
            throw new InvalidOperationException("Optimization settings are missing.");

        if (defectSettings == null)
            throw new InvalidOperationException("Defect settings are missing.");

        if (scoringSettings == null)
            throw new InvalidOperationException("Scoring settings are missing.");

        if (optimizationSettings.CoarseMaxMoveMM <= 0)
            throw new InvalidOperationException("CoarseMaxMoveMM must be greater than zero.");

        if (optimizationSettings.CoarseStepMM <= 0)
            throw new InvalidOperationException("CoarseStepMM must be greater than zero.");

        if (optimizationSettings.MediumSearchRadiusMM <= 0)
            throw new InvalidOperationException("MediumSearchRadiusMM must be greater than zero.");

        if (optimizationSettings.MediumStepMM <= 0)
            throw new InvalidOperationException("MediumStepMM must be greater than zero.");

        if (optimizationSettings.FineSearchRadiusMM <= 0)
            throw new InvalidOperationException("FineSearchRadiusMM must be greater than zero.");

        if (optimizationSettings.FineStepMM <= 0)
            throw new InvalidOperationException("FineStepMM must be greater than zero.");

        if (optimizationSettings.MovementPenaltyWeight < 0)
            throw new InvalidOperationException("MovementPenaltyWeight cannot be negative.");

        if (optimizationSettings.MaximumAllowedWaneIncreasePercent < 0)
            throw new InvalidOperationException("MaximumAllowedWaneIncreasePercent cannot be negative.");

        if (optimizationSettings.WaneGuardPenaltyWeight < 0)
            throw new InvalidOperationException("WaneGuardPenaltyWeight cannot be negative.");

        if (defectSettings.PatchDepthWeight < 0 ||
            defectSettings.PatchCellWeight < 0 ||
            defectSettings.PatchSeverityWeight < 0 ||
            defectSettings.SpindleReliefWeight < 0)
        {
            throw new InvalidOperationException("Defect scoring weights cannot be negative.");
        }
    }
}
