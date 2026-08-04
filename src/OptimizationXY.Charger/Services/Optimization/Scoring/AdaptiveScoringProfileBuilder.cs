using OptimizationXY.Charger.Models.Optimization.Scoring;
using OptimizationXY.Charger.Models.Settings;
using System.Text;
using System.Globalization;

namespace OptimizationXY.Charger.Services.Optimization.Scoring;

public static class AdaptiveScoringProfileBuilder
{

    /// <summary>
    /// Builds an adaptive scoring profile based on the provided diagnostics and application settings.
    /// </summary>
    /// <param name="diagnostics">The adaptive log diagnostics.</param>
    /// <param name="appSettings">The application settings.</param>
    /// <returns>An adaptive scoring profile.</returns>
    public static AdaptiveScoringProfile Build(
        AdaptiveLogDiagnostics diagnostics,
        AppSettings appSettings)
    {
        ArgumentNullException.ThrowIfNull(diagnostics); // Defensive programming: Ensure diagnostics is not null
        ArgumentNullException.ThrowIfNull(appSettings); // Defensive programming: Ensure appSettings is not null

        var adaptive = appSettings.AdaptiveScoring;  // Defensive programming: Ensure adaptive scoring settings are not null

        var profile = new AdaptiveScoringProfile  // Defensive programming: Ensure profile is initialized properly
        {
            Enabled = adaptive.Enabled,
            LogClass = "Balanced",
            Scoring = CopyScoring(appSettings.Scoring),
            Defects = CopyDefects(appSettings.Defects),
            Optimization = CopyOptimization(appSettings.Optimization),
            Wane = CopyWane(appSettings.Wane)
        };

        if (!adaptive.Enabled)
        {
            profile.LogClass = "Fixed Settings";
            profile.Reasons.Add("Adaptive scoring is disabled, so the normal fixed settings were used.");
            return profile;
        }

        bool highSweep =
            diagnostics.CenterlineSweepIn >= adaptive.HighSweepIn ||  // Defensive programming: Ensure that the sweep and bow thresholds are checked correctly
            diagnostics.MaxBowIn >= adaptive.HighBowIn;

        bool highLowWood =
            diagnostics.LowWoodPercent >= adaptive.HighLowWoodPercent ||
            diagnostics.SevereLowWoodPercent >= adaptive.HighSevereLowWoodPercent ||  // Defensive programming: Ensure that the low-wood thresholds are checked correctly
            diagnostics.DeepestLowWoodIn >= adaptive.DeepLowWoodIn;

        bool highOvality =
            diagnostics.OutOfRoundIn >= adaptive.HighOutOfRoundIn;  // Defensive programming: Ensure that the out-of-round threshold is checked correctly

        bool cleanLog =
            diagnostics.LowWoodPercent <= adaptive.CleanLowWoodMaxPercent &&
            diagnostics.CenterlineSweepIn <= adaptive.CleanSweepMaxIn &&
            diagnostics.OutOfRoundIn <= adaptive.CleanOutOfRoundMaxIn;

        if (highLowWood)
        {
            profile.LogClass = "Low-Wood Risk";
            profile.Scoring.LowWoodRiskWeightPercent *= adaptive.LowWoodLogLowWoodWeightMultiplier;
            profile.Scoring.DeepestLowWoodWeightPercent *= adaptive.LowWoodLogDeepestWeightMultiplier;
            profile.Scoring.SpindleMovementWeightPercent *= adaptive.LowWoodLogMovementWeightMultiplier;

            profile.Defects.LowRadiusThresholdMM *= adaptive.SensitiveLowRadiusMultiplier;
            profile.Defects.SevereLowRadiusThresholdMM *= adaptive.SensitiveLowRadiusMultiplier;

            profile.Optimization.MovementPenaltyWeight *= adaptive.LowWoodLogMovementWeightMultiplier;

            profile.Reasons.Add(
                "Low-wood risk is high, so low-wood and deepest-low-wood penalties were increased and movement penalty was reduced.");
        }

        if (highSweep)
        {
            profile.LogClass = profile.LogClass == "Balanced" ? "Swept" : profile.LogClass + " + Swept";
            profile.Scoring.LowWoodRiskWeightPercent *= adaptive.SweptLogLowWoodWeightMultiplier;
            profile.Scoring.CenterlineSweepWeightPercent *= adaptive.SweptLogSweepWeightMultiplier;
            profile.Scoring.SpindleMovementWeightPercent *= adaptive.SweptLogMovementWeightMultiplier;

            profile.Optimization.MovementPenaltyWeight *= adaptive.SweptLogMovementWeightMultiplier;

            profile.Reasons.Add(
                "Centerline sweep or bow is high, so the optimizer is allowed to use more spindle correction if it reduces wane risk.");
        }

        if (highOvality)
        {
            profile.LogClass = profile.LogClass == "Balanced" ? "Oval" : profile.LogClass + " + Oval";
            profile.Scoring.OutOfRoundWeightPercent *= adaptive.OvalLogOutOfRoundWeightMultiplier;
            profile.Scoring.LowWoodRiskWeightPercent *= adaptive.OvalLogLowWoodWeightMultiplier;

            profile.Reasons.Add(
                "Out-of-round amount is high, so thin-zone protection and roundness risk were weighted more strongly.");
        }

        if (cleanLog && !highLowWood && !highSweep && !highOvality)
        {
            profile.LogClass = "Clean";
            profile.Scoring.SpindleMovementWeightPercent *= adaptive.CleanLogMovementWeightMultiplier;
            profile.Scoring.LowWoodRiskWeightPercent *= adaptive.CleanLogLowWoodWeightMultiplier;

            profile.Defects.LowRadiusThresholdMM *= adaptive.RelaxedLowRadiusMultiplier;
            profile.Defects.SevereLowRadiusThresholdMM *= adaptive.RelaxedLowRadiusMultiplier;

            profile.Optimization.MovementPenaltyWeight *= adaptive.CleanLogMovementWeightMultiplier;

            profile.Reasons.Add(
                "The log appears clean, so unnecessary spindle movement was penalized more strongly.");
        }

        profile.Optimization.MovementPenaltyWeight = Clamp(
            profile.Optimization.MovementPenaltyWeight,
            adaptive.MinimumMovementPenaltyWeight,
            adaptive.MaximumMovementPenaltyWeight);

        // ------------------------------------------------------------
        // Adaptive wane guardrail.
        // This controls how much worse wane balance is allowed to get
        // before the optimizer starts pushing back.
        // ------------------------------------------------------------

        if (cleanLog)
        {
            profile.Optimization.MaximumAllowedWaneIncreasePercent = 15.0;
            profile.Optimization.WaneGuardPenaltyWeight = 1.5;
        }

        if (highLowWood || highSweep)
        {
            profile.Optimization.MaximumAllowedWaneIncreasePercent = 45.0;
            profile.Optimization.WaneGuardPenaltyWeight = 1.0;
        }

        if (diagnostics.LowWoodPercent >= 10.0 ||
            diagnostics.DeepestLowWoodIn >= adaptive.DeepLowWoodIn)
        {
            profile.Optimization.MaximumAllowedWaneIncreasePercent = 40.0;
            profile.Optimization.WaneGuardPenaltyWeight = 1.25;
        }

        profile.Optimization.MovementPenaltyWeight = Clamp(
            profile.Optimization.MovementPenaltyWeight,
            adaptive.MinimumMovementPenaltyWeight,
            adaptive.MaximumMovementPenaltyWeight);

        NormalizeUniformWeights(profile.Scoring);

        if (profile.Reasons.Count == 0)
            profile.Reasons.Add("No major log-specific risk dominated, so balanced adaptive settings were used.");

        return profile;
    }

    /// <summary>
    /// Builds a human-readable report of the adaptive scoring profile.
    /// </summary>
    /// <param name="profile"></param>
    /// <returns></returns>
    public static string BuildReport(AdaptiveScoringProfile profile)
    {
        if (profile == null)
            return string.Empty;

        var sb = new StringBuilder();

        sb.AppendLine();
        sb.AppendLine("ADAPTIVE PER-LOG SCORING PROFILE");
        sb.AppendLine(FormattableString.CurrentCulture($"Enabled: {profile.Enabled}"));
        sb.AppendLine(FormattableString.CurrentCulture($"Log Class: {profile.LogClass}"));
        sb.AppendLine("Reason:");

        foreach (string reason in profile.Reasons)
            sb.AppendLine(FormattableString.CurrentCulture($"- {reason}"));

        sb.AppendLine();
        sb.AppendLine("Resolved Uniform Score Weights:");
        sb.AppendLine(FormattableString.CurrentCulture($"Wane Balance: {profile.Scoring.WaneBalanceWeightPercent:F1}%"));
        sb.AppendLine(FormattableString.CurrentCulture($"Adjusted Low-Wood Risk: {profile.Scoring.LowWoodRiskWeightPercent:F1}%"));
        sb.AppendLine(FormattableString.CurrentCulture($"Deepest Low-Wood Area: {profile.Scoring.DeepestLowWoodWeightPercent:F1}%"));
        sb.AppendLine(FormattableString.CurrentCulture($"Spindle Movement: {profile.Scoring.SpindleMovementWeightPercent:F1}%"));
        sb.AppendLine(FormattableString.CurrentCulture($"Out-of-Round: {profile.Scoring.OutOfRoundWeightPercent:F1}%"));
        sb.AppendLine(FormattableString.CurrentCulture($"Centerline Sweep: {profile.Scoring.CenterlineSweepWeightPercent:F1}%"));

        sb.AppendLine();
        sb.AppendLine("Resolved Defect Thresholds:");
        sb.AppendLine(FormattableString.CurrentCulture($"Low Radius Threshold: {profile.Defects.LowRadiusThresholdMM:F2} mm"));
        sb.AppendLine(FormattableString.CurrentCulture($"Severe Low Radius Threshold: {profile.Defects.SevereLowRadiusThresholdMM:F2} mm"));
        sb.AppendLine(FormattableString.CurrentCulture($"High Radius Threshold: {profile.Defects.HighRadiusThresholdMM:F2} mm"));

        sb.AppendLine();
        sb.AppendLine("Resolved Optimizer Penalty:");
        sb.AppendLine(FormattableString.CurrentCulture($"Movement Penalty Weight: {profile.Optimization.MovementPenaltyWeight:F4}"));
        sb.AppendLine(FormattableString.CurrentCulture($"Maximum Allowed Wane Increase: {profile.Optimization.MaximumAllowedWaneIncreasePercent:F1}%"));
        sb.AppendLine(FormattableString.CurrentCulture($"Wane Guard Penalty Weight: {profile.Optimization.WaneGuardPenaltyWeight:F2}"));

        return sb.ToString();
    }

    /// <summary>
    ///     Creates a deep copy of the provided ScoringSettings object.
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    private static ScoringSettings CopyScoring(ScoringSettings s)
    {
        return new ScoringSettings
        {
            WaneBalanceGoodIn = s.WaneBalanceGoodIn,
            WaneBalanceBadIn = s.WaneBalanceBadIn,
            WaneBalanceWeightPercent = s.WaneBalanceWeightPercent,

            LowWoodRiskGoodPercent = s.LowWoodRiskGoodPercent,
            LowWoodRiskBadPercent = s.LowWoodRiskBadPercent,
            LowWoodRiskWeightPercent = s.LowWoodRiskWeightPercent,

            DeepestLowWoodGoodIn = s.DeepestLowWoodGoodIn,
            DeepestLowWoodBadIn = s.DeepestLowWoodBadIn,
            DeepestLowWoodWeightPercent = s.DeepestLowWoodWeightPercent,

            SpindleMovementGoodIn = s.SpindleMovementGoodIn,
            SpindleMovementBadIn = s.SpindleMovementBadIn,
            SpindleMovementWeightPercent = s.SpindleMovementWeightPercent,

            OutOfRoundGoodIn = s.OutOfRoundGoodIn,
            OutOfRoundBadIn = s.OutOfRoundBadIn,
            OutOfRoundWeightPercent = s.OutOfRoundWeightPercent,

            CenterlineSweepGoodIn = s.CenterlineSweepGoodIn,
            CenterlineSweepBadIn = s.CenterlineSweepBadIn,
            CenterlineSweepWeightPercent = s.CenterlineSweepWeightPercent
        };
    }

    /// <summary>
    ///     Creates a deep copy of the provided DefectSettings object.
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    private static DefectSettings CopyDefects(DefectSettings s)
    {
        return new DefectSettings
        {
            LowRadiusThresholdMM = s.LowRadiusThresholdMM,
            SevereLowRadiusThresholdMM = s.SevereLowRadiusThresholdMM,
            HighRadiusThresholdMM = s.HighRadiusThresholdMM,

            LowRadiusWeight = s.LowRadiusWeight,
            SevereLowRadiusWeight = s.SevereLowRadiusWeight,
            HighRadiusWeight = s.HighRadiusWeight,

            PatchDepthWeight = s.PatchDepthWeight,
            PatchCellWeight = s.PatchCellWeight,
            PatchSeverityWeight = s.PatchSeverityWeight,
            SpindleReliefWeight = s.SpindleReliefWeight
        };
    }

    /// <summary>
    ///    Creates a deep copy of the provided OptimizationSettings object.
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    private static OptimizationSettings CopyOptimization(OptimizationSettings s)
    {
        return new OptimizationSettings
        {
            MinimumImprovementPercent = s.MinimumImprovementPercent,

            CoarseMaxMoveMM = s.CoarseMaxMoveMM,
            CoarseStepMM = s.CoarseStepMM,

            MediumSearchRadiusMM = s.MediumSearchRadiusMM,
            MediumStepMM = s.MediumStepMM,

            FineSearchRadiusMM = s.FineSearchRadiusMM,
            FineStepMM = s.FineStepMM,

            MovementPenaltyWeight = s.MovementPenaltyWeight,

            UseCandidateAwareSearchScore = s.UseCandidateAwareSearchScore,
            MaximumAllowedWaneIncreasePercent = s.MaximumAllowedWaneIncreasePercent,
            WaneGuardPenaltyWeight = s.WaneGuardPenaltyWeight
        };
    }

    /// <summary>
    ///   Creates a deep copy of the provided WaneSettings object.
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    private static WaneSettings CopyWane(WaneSettings s)
    {
        return new WaneSettings
        {
            WaneToleranceMM = s.WaneToleranceMM,
            SevereWaneMM = s.SevereWaneMM,
            MinAngleBinsPerWaneSlice = s.MinAngleBinsPerWaneSlice
        };
    }

    /// <summary>
    ///    Normalizes the scoring weights in the provided ScoringSettings object so that they sum to 100%.
    /// </summary>
    /// <param name="scoring"></param>
    private static void NormalizeUniformWeights(ScoringSettings scoring)
    {
        double total =
            scoring.WaneBalanceWeightPercent +
            scoring.LowWoodRiskWeightPercent +
            scoring.DeepestLowWoodWeightPercent +
            scoring.SpindleMovementWeightPercent +
            scoring.OutOfRoundWeightPercent +
            scoring.CenterlineSweepWeightPercent;

        if (total <= 0.000001)
            return;

        double scale = 100.0 / total;

        scoring.WaneBalanceWeightPercent *= scale;
        scoring.LowWoodRiskWeightPercent *= scale;
        scoring.DeepestLowWoodWeightPercent *= scale;
        scoring.SpindleMovementWeightPercent *= scale;
        scoring.OutOfRoundWeightPercent *= scale;
        scoring.CenterlineSweepWeightPercent *= scale;
    }

    /// <summary>
    ///   Clamps the provided value between the specified minimum and maximum values.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    private static double Clamp(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return min;

        return Math.Max(min, Math.Min(max, value));
    }
}
