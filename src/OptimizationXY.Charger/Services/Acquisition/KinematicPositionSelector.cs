using System;
using System.Windows.Forms;
using NLog;
using OptimizationXY.Charger.Models.Optimization.Spindle;
using OptimizationXY.Charger.Services.Core;
using OptimizationXY.Charger.Services.Production;

namespace OptimizationXY.Charger.Kinematic;

/// <summary>
/// Orchestrates the operator selection between Kinematic Rotation and Current XY positions.
/// 
/// This service:
/// 1. Presents a clear UI choice to the operator (MessageBox with two options)
/// 2. If Kinematic selected: runs KinematicsCalculator to rotate positions
/// 3. Captures complete visual diagnostic state during calculation
/// 4. Validates output using same safety checks as PlcPayloadBuilder
/// 5. Returns an updated FinalSpindleRecommendation with selected positions
/// 6. Logs all decisions and calculations for testing/verification
/// 
/// The service is stateless between calls and thread-safe for non-UI operations.
/// UI interactions are synchronous (blocking) to ensure operator engagement.
/// </summary>
public sealed class KinematicPositionSelector
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Presents operator with choice between Kinematic Rotation and Current XY positions,
    /// executes selected path, and returns updated FinalSpindleRecommendation.
    /// 
    /// This is the main entry point for the kinematic selection feature.
    /// 
    /// PROCESS:
    /// 1. Shows MessageBox asking operator to choose between two options
    /// 2. If operator cancels, returns original recommendation unchanged
    /// 3. If "Use Kinematic Rotation" selected:
    ///    - Calls KinematicsCalculator.CalculateCorrectedPositions()
    ///    - Captures full visual diagnostic state
    ///    - Validates kinematic output (finite values, ranges match)
    ///    - Creates updated recommendation with kinematic-adjusted positions
    ///    - Logs complete calculation and decision
    /// 4. If "Continue With Current XY" selected:
    ///    - Returns original recommendation unchanged
    ///    - Logs operator decision
    /// 5. In both cases, adds diagnostics to state for audit trail
    /// 
    /// SAFETY: 
    /// - Both paths produce FinalSpindleRecommendation objects that pass through
    ///   PlcPayloadBuilder.ValidateFinite() before PLC write
    /// - Kinematic output validated identically to Optimization output
    /// - No PLC write occurs until both validation layers pass
    /// </summary>
    /// <param name="originalRecommendation">The FinalSpindleRecommendation from Optimization</param>
    /// <returns>FinalSpindleRecommendation with either original or kinematic-adjusted positions</returns>
    /// <exception cref="ArgumentNullException">If originalRecommendation is null</exception>
    public static FinalSpindleRecommendation GetOperatorPositionSelection(
        FinalSpindleRecommendation originalRecommendation)
    {
        ArgumentNullException.ThrowIfNull(originalRecommendation);

        // Log the beginning of the operator selection process
        Logger.Debug(
            "Kinematic selection process starting. Original positions: " +
            $"Head=({originalRecommendation.CommandHeadXMM:F3}, {originalRecommendation.CommandHeadYMM:F3}) " +
            $"Tail=({originalRecommendation.CommandTailXMM:F3}, {originalRecommendation.CommandTailYMM:F3})");

        // Present operator with choice using MessageBox
        DialogResult choice = MessageBox.Show(
            "Choose position calculation method:\r\n\r\n" +
            "• Use Kinematic Rotation: Apply rotation logic to avoid collision zones\r\n" +
            "• Continue With Current XY: Use existing optimization positions\r\n\r\n" +
            "Click the appropriate button to proceed.",
            "Position Selection - Kinematic Mode Available",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        // If operator cancels or clicks "No", use current positions
        if (choice == DialogResult.No)
        {
            Logger.Info("Operator selected: Continue With Current XY (no kinematic rotation applied)");
            return originalRecommendation;
        }

        // Operator clicked "Yes" - use Kinematic Rotation
        Logger.Info("Operator selected: Use Kinematic Rotation (applying rotation logic to positions)");

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
                $"  Head X: {original.CommandHeadXMM:F4} mm\r\n" +
                $"  Head Y: {original.CommandHeadYMM:F4} mm\r\n" +
                $"  Tail X: {original.CommandTailXMM:F4} mm\r\n" +
                $"  Tail Y: {original.CommandTailYMM:F4} mm\r\n";

            // Store input values in visualization state
            state.InputHeadXMM = original.CommandHeadXMM;
            state.InputHeadYMM = original.CommandHeadYMM;
            state.InputTailXMM = original.CommandTailXMM;
            state.InputTailYMM = original.CommandTailYMM;

            state.RotationZoneLeftLimitDeg = calculator.Config.RotationZoneLeftLimit;
            state.RotationZoneRightLimitDeg = calculator.Config.RotationZoneRightLimit;
            state.CorrectionOffsetsApplied = calculator.Config.EnableXYCorrectionOffset;

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

            // Store final corrected positions
            state.FinalHeadXMM = correctedHeadX;
            state.FinalHeadYMM = correctedHeadY;
            state.FinalTailXMM = correctedTailX;
            state.FinalTailYMM = correctedTailY;

            state.CalculatedAtUtc = DateTime.UtcNow;

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

            state.IsValid = true;
            state.HeadPositionChangeMM = state.CalculateHeadPositionChange();
            state.TailPositionChangeMM = state.CalculateTailPositionChange();

            Logger.Info(
                $"Kinematic calculation successful. Head moved {state.HeadPositionChangeMM:F3} mm, " +
                $"Tail moved {state.TailPositionChangeMM:F3} mm");

            pipelineState.DiagnosticsReport +=
                $"[KINEMATIC] Calculated corrected positions:\r\n" +
                $"  Head X: {correctedHeadX:F4} mm (change: {correctedHeadX - original.CommandHeadXMM:+0.0000;-0.0000} mm)\r\n" +
                $"  Head Y: {correctedHeadY:F4} mm (change: {correctedHeadY - original.CommandHeadYMM:+0.0000;-0.0000} mm)\r\n" +
                $"  Tail X: {correctedTailX:F4} mm (change: {correctedTailX - original.CommandTailXMM:+0.0000;-0.0000} mm)\r\n" +
                $"  Tail Y: {correctedTailY:F4} mm (change: {correctedTailY - original.CommandTailYMM:+0.0000;-0.0000} mm)\r\n" +
                $"[KINEMATIC] Position changes: Head {state.HeadPositionChangeMM:F3} mm, Tail {state.TailPositionChangeMM:F3} mm\r\n" +
                $"[KINEMATIC] Rotation zone: {state.RotationZoneLeftLimitDeg:F1}° to {state.RotationZoneRightLimitDeg:F1}°\r\n" +
                $"[KINEMATIC] Validation: PASSED - kinematic positions are valid\r\n";

            // Create new recommendation with kinematic-adjusted positions
            var kinematicRecommendation = new FinalSpindleRecommendation
            {
                // Copy all original fields
                OptimizationMode = original.OptimizationMode,
                SelectedStrategy = original.SelectedStrategy + " (with Kinematic Rotation)",
                IsRecommended = original.IsRecommended,
                MachineMoveAllowed = original.MachineMoveAllowed,
                RecommendedAction = $"{original.RecommendedAction} [Rotated by Kinematics]",
                Reason = $"{original.Reason} [Applied kinematic rotation to avoid collision zone]",
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

            // Add kinematic-specific warning if positions changed significantly
            if (state.HeadPositionChangeMM > 5.0 || state.TailPositionChangeMM > 5.0)
            {
                kinematicRecommendation.Warnings.Add(
                    $"Kinematic rotation applied: Head moved {state.HeadPositionChangeMM:F2} mm, " +
                    $"Tail moved {state.TailPositionChangeMM:F2} mm to avoid collision zone");
            }

            Logger.Info(
                $"[KINEMATIC] Final recommendation created with kinematic positions. " +
                $"Final positions: Head=({correctedHeadX:F3}, {correctedHeadY:F3}) " +
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
