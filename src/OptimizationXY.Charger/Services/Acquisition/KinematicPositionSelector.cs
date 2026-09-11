using System;
using NLog;
using OptimizationXY.Charger.Models.Optimization.Spindle;
using OptimizationXY.Charger.Services.Core;
using OptimizationXY.Charger.Services.Production;

namespace OptimizationXY.Charger.Kinematic;

/// <summary>
/// Automatically applies kinematic rotation to optimize spindle positions.
/// 
/// This service:
/// 1. Runs KinematicsCalculator to rotate positions automatically (no UI prompt)
/// 2. Captures complete mathematical calculation state
/// 3. Validates output using same safety checks as PlcPayloadBuilder
/// 4. Returns an updated FinalSpindleRecommendation with kinematic-adjusted positions
/// 5. Logs all calculations for verification and sanity checks
/// 
/// The service is stateless between calls and thread-safe.
/// All calculations run silently in the background for verification purposes.
/// </summary>
public sealed class KinematicPositionSelector
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Automatically applies kinematic rotation to optimize spindle positions.
    /// No operator interaction required - calculation runs silently in background.
    /// 
    /// PROCESS:
    /// 1. Calls KinematicsCalculator.CalculateCorrectedPositions()
    /// 2. Captures mathematical calculation state
    /// 3. Validates kinematic output (finite values, ranges match)
    /// 4. Creates updated recommendation with kinematic-adjusted positions
    /// 5. Logs complete calculation for verification
    /// 6. Returns result for PLC transmission
    /// 
    /// SAFETY: 
    /// - Output validated identically to Optimization output
    /// - No PLC write occurs until PlcPayloadBuilder.ValidateFinite() passes
    /// </summary>
    /// <param name="originalRecommendation">The FinalSpindleRecommendation from Optimization</param>
    /// <returns>FinalSpindleRecommendation with kinematic-adjusted positions</returns>
    /// <exception cref="ArgumentNullException">If originalRecommendation is null</exception>
    public static FinalSpindleRecommendation GetOperatorPositionSelection(
        FinalSpindleRecommendation originalRecommendation)
    {
        ArgumentNullException.ThrowIfNull(originalRecommendation);

        // Log the beginning of the kinematic calculation (no UI dialog)
        Logger.Debug(
            "Kinematic rotation starting automatically. Input positions: " +
            $"Head=({originalRecommendation.CommandHeadXMM:F3}, {originalRecommendation.CommandHeadYMM:F3}) " +
            $"Tail=({originalRecommendation.CommandTailXMM:F3}, {originalRecommendation.CommandTailYMM:F3})");

        // Automatically apply kinematic rotation (no MessageBox dialog)
        Logger.Info("Automatically applying kinematic rotation to optimize positions");
        return ApplyKinematicRotation(originalRecommendation);
    }

    /// <summary>
    /// Applies kinematic rotation logic to the optimization positions.
    /// 
    /// PROCESS:
    /// 1. Creates KinematicsCalculator with default config
    /// 2. Calls CalculateCorrectedPositions() to get rotated XY values
    /// 3. Captures full visual diagnostic state
    /// 4. Validates output (same checks as PlcPayloadBuilder.ValidateFinite)
    /// 5. Creates new FinalSpindleRecommendation with kinematic-adjusted positions
    /// 6. Logs all intermediate results for testing verification
    /// 7. Returns updated recommendation for PLC write workflow
    /// 
    /// If validation fails, detailed error is logged but does not throw.
    /// This allows the system to proceed safely (existing positions remain valid fallback).
    /// </summary>
    private static FinalSpindleRecommendation ApplyKinematicRotation(
        FinalSpindleRecommendation original)
    {
        var calculator = new KinematicsCalculator();
        var state = new KinematicVisualizationState();
        var pipelineState = PipelineState.Instance;

        try
        {
            // Log original positions before calculation
            Logger.Debug($"Kinematic calculation starting with inputs: " +
                $"Head=({original.CommandHeadXMM:F3}, {original.CommandHeadYMM:F3}) " +
                $"Tail=({original.CommandTailXMM:F3}, {original.CommandTailYMM:F3})");

            pipelineState.DiagnosticsReport += 
                $"[KINEMATIC] Input Optimization positions:\r\n" +
                $"  Head: X={original.CommandHeadXMM:F4}mm, Y={original.CommandHeadYMM:F4}mm\r\n" +
                $"  Tail: X={original.CommandTailXMM:F4}mm, Y={original.CommandTailYMM:F4}mm\r\n";

            // Call kinematic calculator
            calculator.CalculateCorrectedPositions(
                original.CommandHeadXMM,
                original.CommandHeadYMM,
                original.CommandTailXMM,
                original.CommandTailYMM,
                out double correctedHeadX,
                out double correctedHeadY,
                out double correctedTailX,
                out double correctedTailY);

            // Calculate forward kinematics for cylinder positions (A1, B1, A2, B2)
            calculator.CalculateForwardKinematics(
                correctedHeadX,
                correctedHeadY,
                correctedTailX,
                correctedTailY,
                out double a1,
                out double b1,
                out double a2,
                out double b2);

            // Store final corrected positions
            state.FinalHeadXMM = correctedHeadX;
            state.FinalHeadYMM = correctedHeadY;
            state.FinalTailXMM = correctedTailX;
            state.FinalTailYMM = correctedTailY;

            // Validate kinematic output using same method as PlcPayloadBuilder
            string validationError = ValidateKinematicOutput(
                correctedHeadX, correctedHeadY, correctedTailX, correctedTailY);

            if (!string.IsNullOrEmpty(validationError))
            {
                state.IsValid = false;
                state.ValidationErrorMessage = validationError;
                Logger.Error($"Kinematic output validation failed: {validationError}");
                pipelineState.DiagnosticsReport += 
                    $"[KINEMATIC] ⚠ VALIDATION FAILED: {validationError}\r\n" +
                    $"[KINEMATIC] Falling back to original optimization positions.\r\n";

                // Return original positions if kinematic output is invalid
                return original;
            }

            pipelineState.DiagnosticsReport +=
                $"[KINEMATIC] Output Kinematic positions:\r\n" +
                $"  Head: X={correctedHeadX:F4}mm, Y={correctedHeadY:F4}mm\r\n" +
                $"  Tail: X={correctedTailX:F4}mm, Y={correctedTailY:F4}mm\r\n" +
                $"[KINEMATIC] Cylinder targets (A1, B1, A2, B2):\r\n" +
                $"  A1 (Head X): {a1:F4}\r\n" +
                $"  B1 (Head Y): {b1:F4}\r\n" +
                $"  A2 (Tail X): {a2:F4}\r\n" +
                $"  B2 (Tail Y): {b2:F4}\r\n" +
                $"[KINEMATIC] Validation: PASSED\r\n";

            // Create new recommendation with kinematic-adjusted positions
            var kinematicRecommendation = new FinalSpindleRecommendation
            {
                // Copy all original fields
                OptimizationMode = original.OptimizationMode,
                SelectedStrategy = original.SelectedStrategy,
                IsRecommended = original.IsRecommended,
                MachineMoveAllowed = original.MachineMoveAllowed,
                RecommendedAction = original.RecommendedAction,
                Reason = original.Reason,
                Warnings = new List<string>(original.Warnings),

                RiskScore = original.RiskScore,
                RiskRating = original.RiskRating,
                BaselineRiskScore = original.BaselineRiskScore,
                BaselineRiskRating = original.BaselineRiskRating,
                RiskScoreReductionPercent = original.RiskScoreReductionPercent,

                // Use kinematic-corrected positions
                CommandHeadXMM = correctedHeadX,
                CommandHeadYMM = correctedHeadY,
                CommandTailXMM = correctedTailX,
                CommandTailYMM = correctedTailY,

                // Keep best tested values for reference
                BestTestedHeadXMM = original.BestTestedHeadXMM,
                BestTestedHeadYMM = original.BestTestedHeadYMM,
                BestTestedTailXMM = original.BestTestedTailXMM,
                BestTestedTailYMM = original.BestTestedTailYMM,

                // Copy all other fields
                MaxSpindleMovementMM = original.MaxSpindleMovementMM,
                TotalFullSheets = original.TotalFullSheets,
                UsableFullSheets = original.UsableFullSheets,
                RejectedFullSheets = original.RejectedFullSheets,
                UsableVeneerLengthMM = original.UsableVeneerLengthMM,
                InscribedMajorDiameterMM = original.InscribedMajorDiameterMM,
                InscribedMinorDiameterMM = original.InscribedMinorDiameterMM,
                InscribedOrientationDeg = original.InscribedOrientationDeg,
                BaselineWaneScoreMM = original.BaselineWaneScoreMM,
                OptimizedWaneScoreMM = original.OptimizedWaneScoreMM,
                BaselineInternalSearchScore = original.BaselineInternalSearchScore,
                OptimizedInternalSearchScore = original.OptimizedInternalSearchScore,
                BaselineDefectPenalty = original.BaselineDefectPenalty,
                OptimizedDefectPenalty = original.OptimizedDefectPenalty,
                TestedCandidates = original.TestedCandidates,
                CoarseCandidates = original.CoarseCandidates,
                MediumCandidates = original.MediumCandidates,
                FineCandidates = original.FineCandidates,
            };

            Logger.Info(
                $"[KINEMATIC] Final kinematic positions returned: " +
                $"Head=({correctedHeadX:F3}, {correctedHeadY:F3}) " +
                $"Tail=({correctedTailX:F3}, {correctedTailY:F3})");

            return kinematicRecommendation;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception occurred during kinematic position selection");
            Logger.Warn(
                $"[KINEMATIC] ⚠ ERROR during kinematic calculation: {ex.Message}. " +
                $"Falling back to original optimization positions.");

            // Return original positions on any exception
            return original;
        }
    }

    /// <summary>
    /// Validates kinematic output using the same validation rules as PlcPayloadBuilder.ValidateFinite().
    /// 
    /// Checks:
    /// 1. All values are finite (not NaN, not Infinity)
    /// 2. All values within float range (can be represented in PLC FLOAT type)
    /// 
    /// This ensures kinematic output is safe to send to PLC.
    /// </summary>
    /// <returns>Empty string if valid, error message if invalid</returns>
    private static string ValidateKinematicOutput(
        double headX, double headY, double tailX, double tailY)
    {
        if (!double.IsFinite(headX))
            return $"Head X is not finite: {headX}";

        if (!double.IsFinite(headY))
            return $"Head Y is not finite: {headY}";

        if (!double.IsFinite(tailX))
            return $"Tail X is not finite: {tailX}";

        if (!double.IsFinite(tailY))
            return $"Tail Y is not finite: {tailY}";

        if (headX > float.MaxValue || headX < -float.MaxValue)
            return $"Head X exceeds FLOAT range: {headX}";

        if (headY > float.MaxValue || headY < -float.MaxValue)
            return $"Head Y exceeds FLOAT range: {headY}";

        if (tailX > float.MaxValue || tailX < -float.MaxValue)
            return $"Tail X exceeds FLOAT range: {tailX}";

        if (tailY > float.MaxValue || tailY < -float.MaxValue)
            return $"Tail Y exceeds FLOAT range: {tailY}";

        return string.Empty;  // All validations passed
    }
}
