using System.Text;
using OptimizationXY.Charger.Models.Settings;

namespace OptimizationXY.Charger.Services.Configuration;


/// <summary>
/// Provides a summary of the application settings for diagnostic purposes.
/// </summary>
public static class AppSettingsDiagnosticsBuilder
{
    /// <summary>
    /// Builds a summary of the provided application settings.
    /// </summary>
    /// <param name="settings">The application settings to summarize.</param>
    /// <returns>A string containing the summary of the application settings.</returns>
    public static string BuildSummary(AppSettings settings)
    {
        var sb = new StringBuilder();

        sb.AppendLine("SETTINGS SUMMARY");
        sb.AppendLine(FormattableString.CurrentCulture($"Settings Version: {settings.SettingsVersion}"));

        sb.AppendLine();
        sb.AppendLine("Dual-Camera Acquisition:");
        sb.AppendLine(FormattableString.CurrentCulture($"Target Concatenated Frames: {settings.Acquisition.TargetFramePairsPerLog}"));
        sb.AppendLine(FormattableString.CurrentCulture($"Head On Left: {settings.Acquisition.Concatenation.HeadOnLeft}"));
        sb.AppendLine(FormattableString.CurrentCulture($"Head Crop L/R: {settings.Acquisition.Concatenation.HeadCropLeftPixels}/{settings.Acquisition.Concatenation.HeadCropRightPixels} px"));
        sb.AppendLine(FormattableString.CurrentCulture($"Tail Crop L/R: {settings.Acquisition.Concatenation.TailCropLeftPixels}/{settings.Acquisition.Concatenation.TailCropRightPixels} px"));
        sb.AppendLine(FormattableString.CurrentCulture($"Tail Vertical Offset: {settings.Acquisition.Concatenation.TailVerticalOffsetPixels} px"));
        sb.AppendLine(FormattableString.CurrentCulture($"Camera Overlap: {settings.Acquisition.Concatenation.OverlapPixels} px"));

        sb.AppendLine();
        sb.AppendLine("Units:");
        sb.AppendLine(FormattableString.CurrentCulture($"Display Inches First: {settings.Units.DisplayInchesFirst}"));
        sb.AppendLine(FormattableString.CurrentCulture($"Internal Math Uses MM: {settings.Units.InternalMathUsesMM}"));

        sb.AppendLine();
        sb.AppendLine("Rotating Scan:");
        sb.AppendLine(FormattableString.CurrentCulture($"Minimum Valid Frames: {settings.RotatingScan.MinimumValidFrames}"));
        sb.AppendLine(FormattableString.CurrentCulture($"Preferred Valid Frames: {settings.RotatingScan.PreferredValidFrames}"));
        sb.AppendLine(FormattableString.CurrentCulture($"Expected Angle Step: {settings.RotatingScan.ExpectedAngleStepDeg:F2} deg"));

        sb.AppendLine();
        sb.AppendLine("Geometry:");
        sb.AppendLine(FormattableString.CurrentCulture($"Local Centerline Slice: {settings.Geometry.LocalCenterlineSliceThicknessMM:F2} mm"));
        sb.AppendLine(FormattableString.CurrentCulture($"Radius Z Bin Size: {settings.Geometry.RadiusFieldZBinSizeMM:F2} mm"));
        sb.AppendLine(FormattableString.CurrentCulture($"Radius Angle Bin Size: {settings.Geometry.RadiusFieldAngleBinSizeDeg:F2} deg"));

        sb.AppendLine();
        sb.AppendLine("Defects:");
        sb.AppendLine(FormattableString.CurrentCulture($"Low Radius Threshold: {settings.Defects.LowRadiusThresholdMM:F2} mm"));
        sb.AppendLine(FormattableString.CurrentCulture($"Severe Low Radius Threshold: {settings.Defects.SevereLowRadiusThresholdMM:F2} mm"));
        sb.AppendLine(FormattableString.CurrentCulture($"High Radius Threshold: {settings.Defects.HighRadiusThresholdMM:F2} mm"));

        sb.AppendLine();
        sb.AppendLine("Optimization:");
        sb.AppendLine(FormattableString.CurrentCulture($"Mode: {settings.OptimizationMode}"));
        sb.AppendLine(FormattableString.CurrentCulture($"Minimum Improvement: {settings.Optimization.MinimumImprovementPercent:F1}%"));
        sb.AppendLine(FormattableString.CurrentCulture($"Coarse Max Move: {settings.Optimization.CoarseMaxMoveMM:F2} mm"));
        sb.AppendLine(FormattableString.CurrentCulture($"Inscribed Geometry Angular Bins: {settings.InscribedGeometry.AngularBins}"));
        sb.AppendLine(FormattableString.CurrentCulture($"Inscribed Geometry Safety Margin: {settings.InscribedGeometry.SafetyMarginMM:F2} mm"));
        sb.AppendLine(FormattableString.CurrentCulture($"Full Sheet Length: {settings.PeelView.VeneerMarkerSpacingInches:F1} in"));
        sb.AppendLine(FormattableString.CurrentCulture($"Minimum Usable Sheet Coverage: {settings.PeelView.MinimumUsableSheetCoveragePercent:F1}%"));
        sb.AppendLine(FormattableString.CurrentCulture($"Direct Recovery Refinement: {settings.RecoveryOptimization.EnableDirectRecoveryRefinement}"));
        sb.AppendLine(FormattableString.CurrentCulture($"Direct Recovery Radius / Step: {settings.RecoveryOptimization.DirectRefinementRadiusMM:F2} / {settings.RecoveryOptimization.DirectRefinementStepMM:F2} mm"));
        sb.AppendLine(FormattableString.CurrentCulture($"Direct Recovery Candidate Cap: {settings.RecoveryOptimization.MaximumDirectRecoveryCandidates}"));

        sb.AppendLine();
        sb.AppendLine("Production Control:");
        sb.AppendLine(FormattableString.CurrentCulture($"Operating Mode: {settings.Production.OperatingMode}"));
        sb.AppendLine(FormattableString.CurrentCulture($"Commissioning Complete: {settings.Production.CommissioningComplete}"));
        sb.AppendLine(FormattableString.CurrentCulture($"PLC Integration Enabled: {settings.Plc.Enabled}"));
        sb.AppendLine(FormattableString.CurrentCulture($"PLC Auto Start: {settings.Plc.AutoStartCaptureOnScanStart}"));
        sb.AppendLine(FormattableString.CurrentCulture($"PLC Auto Send: {settings.Plc.AutoSendResultsAfterSave}"));
        sb.AppendLine(FormattableString.CurrentCulture($"PLC Send Before Save: {settings.Plc.SendResultsBeforeSave}"));
        sb.AppendLine(FormattableString.CurrentCulture($"Require PLC ScanComplete: {settings.Plc.RequireScanCompleteBeforeProcessing}"));
        sb.AppendLine(FormattableString.CurrentCulture($"PLC ScanComplete Timeout: {settings.Plc.ScanCompleteTimeoutMilliseconds} ms"));
        sb.AppendLine(FormattableString.CurrentCulture($"Require Encoder Acquisition: {settings.Production.RequireEncoderAcquisition}"));

        double knifeClearanceMM = settings.Safety.ComputeKnifeClearanceMM(
            settings.UsableRadius.VeneerThicknessMM);

        sb.AppendLine();
        sb.AppendLine("Knife Safety:");
        sb.AppendLine(FormattableString.CurrentCulture($"Peel Thickness: {settings.UsableRadius.VeneerThicknessMM:F3} mm"));
        sb.AppendLine(FormattableString.CurrentCulture($"Clearance Multiplier: {settings.Safety.KnifeClearancePeelThicknessMultiplier:F1}x"));
        sb.AppendLine(FormattableString.CurrentCulture($"Effective Carriage-Retract Clearance: {knifeClearanceMM:F3} mm"));
        sb.AppendLine("Rotation Command From Application: None");

        sb.AppendLine();
        sb.AppendLine("Machine Limits:");
        sb.AppendLine(FormattableString.CurrentCulture($"Max Tail X: {settings.MachineLimits.MaxTailMoveXMM:F2} mm"));
        sb.AppendLine(FormattableString.CurrentCulture($"Max Tail Y: {settings.MachineLimits.MaxTailMoveYMM:F2} mm"));
        sb.AppendLine(FormattableString.CurrentCulture($"Max Head X: {settings.MachineLimits.MaxHeadMoveXMM:F2} mm"));
        sb.AppendLine(FormattableString.CurrentCulture($"Max Head Y: {settings.MachineLimits.MaxHeadMoveYMM:F2} mm"));

        return sb.ToString();
    }
}
