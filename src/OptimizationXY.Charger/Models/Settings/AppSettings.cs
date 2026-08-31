using OptimizationXY.Charger.Models.Acquisition;
using OptimizationXY.Charger.Models.Optimization;


/// <summary>
/// Represents the application settings for the OptimizationXY Charger application.
/// </summary>
namespace OptimizationXY.Charger.Models.Settings;

public sealed class AppSettings
{
    /// <summary>
    /// The version of the settings schema. This is used to determine if the settings file is
    /// compatible with the current application version.
    /// </summary>
    public int SettingsVersion { get; set; } = 10;


    /// <summary>
    /// Settings related to camera acquisition, including exposure, gain, and other camera parameters.
    /// </summary>
    public CameraAcquisitionSettings Acquisition { get; set; } = new();


    /// <summary>
    /// Settings related to the operator view, including display preferences and user interface settings.
    /// </summary>
    public OperatorViewSettings OperatorView { get; set; } = new();

    /// <summary>
    /// Unit settings for the application, including internal math units and display preferences.
    /// </summary>
    public UnitSettings Units { get; set; } = new(); 
    /// <summary>
    /// Settings related to the rotating scan process, including minimum and preferred valid frames, expected angle step, and tolerances.
    /// </summary>
    public RotatingScanSettings RotatingScan { get; set; } = new(); 

    /// <summary>
    /// Settings for the peel view display, including maximum linear gap and angular neighbor frames for display fill.
    /// </summary>
    public PeelViewSettings PeelView { get; set; } = new();  

    /// <summary>
    /// Settings related to geometry analysis, including local centerline slice thickness, minimum points per slice, radius field bin sizes, and minimum points per radius bin.
    /// </summary>
    public GeometrySettings Geometry { get; set; } = new(); 
    /// <summary>
    /// Settings for sweep analysis, including end trim fraction and thresholds for good, warning, and bad sweep measurements.
    /// </summary>
    public SweepSettings Sweep { get; set; } = new(); 

    /// <summary>
    /// Settings for wane analysis, including thresholds for good, warning, and bad wane measurements.
    /// </summary>
    public WaneSettings Wane { get; set; } = new(); 
    /// <summary>
    /// Settings for usable radius analysis, including thresholds for good, warning, and bad usable radius measurements.
    /// </summary>
    public UsableRadiusSettings UsableRadius { get; set; } = new(); 

    /// <summary>
    /// Settings for defect analysis, including thresholds for good, warning, and bad defect measurements.
    /// </summary>
    public DefectSettings Defects { get; set; } = new(); 
    /// <summary>
    /// Settings for optimization analysis, including parameters for optimization algorithms and criteria.
    /// </summary>
    public OptimizationSettings Optimization { get; set; } = new(); 
    /// <summary>
    /// The mode for spindle optimization.
    /// </summary>
    public SpindleOptimizationMode OptimizationMode { get; set; } = SpindleOptimizationMode.CompareInscribedGeometry;
    /// <summary>
    /// Settings for inscribed geometry analysis.
    /// </summary>
    public InscribedGeometrySettings InscribedGeometry { get; set; } = new();
    /// <summary>
    /// Settings for scoring analysis, including weights and thresholds for different scoring metrics.
    /// </summary>
    public ScoringSettings Scoring { get; set; } = new(); 

    /// <summary>
    /// Settings for adaptive scoring, including parameters for adaptive scoring algorithms and criteria.
    /// </summary>
    public AdaptiveScoringSettings AdaptiveScoring { get; set; } = new(); 
    /// <summary>
    /// Settings for machine limits, including maximum and minimum operational parameters.
    /// </summary>
    public MachineLimitSettings MachineLimits { get; set; } = new(); 
    /// <summary>
    /// Settings for production, including parameters for production processes and criteria.
    /// </summary>
    public ProductionSettings Production { get; set; } = new(); 
    /// <summary>
    /// Settings for PLC integration, including parameters for PLC communication and control.
    /// </summary>
    public PlcIntegrationSettings Plc { get; set; } = new(); 
    /// <summary>
    /// Settings for safety, including parameters for safety checks and criteria.
    /// </summary>
    public SafetySettings Safety { get; set; } = new(); 
    /// <summary>
    /// Settings for recovery optimization, including parameters for recovery optimization algorithms and criteria.
    /// </summary>
    public RecoveryOptimizationSettings RecoveryOptimization { get; set; } = new(); 
    /// <summary>
    /// Settings for diagnostics, including thresholds and parameters for diagnostic checks.
    /// </summary>
    public DiagnosticsSettings Diagnostics { get; set; } = new();

    /// <summary>
    /// Settings for scan folder information, including year, month, day, block number, and scan number.
    /// </summary>
    public ScanFolderInfo Scan { get; set; } = new();


    /// <summary>
    /// Settings for database configuration, including connection strings and parameters for database access.
    /// </summary>
    public DatabaseSettings Database { get; set; } = new();

    /// <summary>
    /// Settings for production setup, including parameters for production readiness and commissioning checks.
    /// </summary>
    public ProductionSetupSettings Setup { get; set; } = new();

    /// <summary>
    /// Settings for scan storage, including parameters for scan data storage and management.
    /// </summary>
    public ScanStorageSettings Storage { get; set; } = new();

    /// <summary>
    /// Settings for kinematic rotation, including whether to enable the kinematic position selection UI.
    /// </summary>
    public KinematicSettings Kinematic { get; set; } = new();
}

/// <summary>
/// Represents the unit settings for the application, including internal math units and display preferences.
/// </summary>
public sealed class UnitSettings
{
    /// <summary>
    /// Indicates whether internal calculations use millimeters (true) or inches (false). This setting affects how measurements are processed and displayed throughout the application.
    /// </summary>
    public bool InternalMathUsesMM { get; set; } = true;

    /// <summary>
    /// Indicates whether to display inch measurements before millimeter measurements in the user interface. If true, inch measurements will be shown first; if false, millimeter measurements will be shown first.
    /// </summary>
    public bool DisplayInchesFirst { get; set; } = true; 
    /// <summary>
    /// Number of decimal places to display for inch measurements.
    /// </summary>
    public int InchesDecimals { get; set; } = 3;  
    /// <summary>
    /// Number of decimal places to display for millimeter measurements.
    /// </summary>
    public int MillimetersDecimals { get; set; } = 2;  
}


/// <summary>
/// Represents the settings for the rotating scan process, including minimum and preferred valid frames, expected angle step, and tolerances.
/// </summary>
public sealed class RotatingScanSettings
{
    /// <summary>
    /// Minimum number of valid frames required for a successful rotating scan.
    /// </summary>
    public int MinimumValidFrames { get; set; } = 12;  
    /// <summary>
    /// Preferred number of valid frames for optimal rotating scan results.
    /// </summary>
    public int PreferredValidFrames { get; set; } = 36;  

    /// <summary>
    /// Expected angle step in degrees between consecutive frames during the rotating scan.
    /// </summary>
    public double ExpectedAngleStepDeg { get; set; } = 10.0;  

    /// <summary>
    /// Minimum number of profile points required per frame for a valid rotating scan.
    /// </summary>
    public int MinimumProfilePointsPerFrame { get; set; } = 50;

    /// <summary>
    /// Tolerance in degrees for the angle step between consecutive frames during the rotating scan.
    /// This value defines the acceptable range of variation from the expected angle step.
    /// </summary>
    public int DisplayFillMaxLinearGapPixels { get; set; } = 20;

    /// <summary>
    /// Maximum number of angular neighbor frames to consider for display fill during the rotating scan.
    /// This value defines how many adjacent frames can be used to fill in missing data for display purposes.
    /// </summary>
    public int DisplayFillMaxAngularNeighborFrames { get; set; } = 2;

    /// <summary>
    /// Minimum valid end diameter fraction for the rotating scan. This value defines
    /// the minimum acceptable ratio of the end diameter to the overall diameter of the scanned object.
    /// </summary>

    public double MinValidEndDiameterFraction { get; set; } = 0.18;

    /// <summary>
    /// Indicates whether to fill in missing points during the rotating scan process. If true, the application will attempt to
    /// interpolate or estimate missing data points based on available information; if false, missing points will remain unfilled.
    /// </summary>
    public bool FillMissingPoints { get; internal set; }
}


/// <summary>
/// Represents the settings for geometry analysis, including local centerline slice thickness, minimum points per slice, radius field bin sizes, and minimum points per radius bin.
/// </summary>
public sealed class GeometrySettings
{

    /// <summary>
    /// Thickness of each slice used for local centerline analysis in millimeters. This value defines how thick each slice of the log will be when analyzing the local centerline geometry.
    /// </summary>
    public double LocalCenterlineSliceThicknessMM { get; set; } = 10.0;


    /// <summary>
    /// Minimum number of points required per local centerline slice for valid analysis.
    /// </summary>
    public int MinPointsPerLocalCenterlineSlice { get; set; } = 8;  

    /// <summary>
    /// Size of the Z-axis bins used for radius field analysis in millimeters.
    /// </summary>
    public double RadiusFieldZBinSizeMM { get; set; } = 10.0;  

    /// <summary>
    /// Size of the angle bins used for radius field analysis in degrees.
    /// </summary>
    public double RadiusFieldAngleBinSizeDeg { get; set; } = 10.0;  

    /// <summary>
    /// Minimum number of points required per radius bin for valid analysis.
    /// </summary>
    public int MinPointsPerRadiusBin { get; set; } = 1;  

}

/// <summary>
/// Represents the settings for sweep analysis, including end trim fraction and thresholds for good, warning, and bad sweep measurements.
/// </summary>
public sealed class SweepSettings
{

    /// <summary>
    /// Fraction of the log length to trim from each end for sweep analysis. This value defines how much of the log's ends
    /// will be excluded from the sweep analysis to avoid edge effects and ensure accurate measurements.
    /// </summary>
    public double EndTrimFraction { get; set; } = 0.03;

    /// <summary>
    /// Minimum number of slices to consider at each end of the log for sweep analysis.
    /// </summary>
    public int MinimumEndSlices { get; set; } = 3;

    /// <summary>
    /// Fraction of the log length to consider at the end for sweep analysis. This value defines how much of the log's ends
    /// </summary>
    public double EndSliceFraction { get; set; } = 0.05; 

}


/// <summary>
/// Represents the settings for wane analysis, including thresholds for good, warning, and bad wane measurements.
/// </summary>
public sealed class WaneSettings
{
    /// <summary>
    /// Tolerance for wane measurements in millimeters. Wane measurements within this tolerance are considered acceptable.
    /// </summary>
    public double WaneToleranceMM { get; set; } = 1.0;

    /// <summary>
    /// Threshold for severe wane measurements in millimeters. Wane measurements exceeding this threshold are considered severe.
    /// </summary>
    public double SevereWaneMM { get; set; } = 3.0;

    /// <summary>
    /// Minimum number of angle bins required per wane slice for valid analysis.
    /// </summary>
    public int MinAngleBinsPerWaneSlice { get; set; } = 12;

}


/// <summary>
/// Represents the settings for usable radius analysis, including thresholds for good, warning, and bad usable radius measurements.
/// </summary>
public sealed class UsableRadiusSettings
{

    /// <summary>
    /// Core diameter in inches used for usable radius analysis. This value defines the minimum core diameter of the log that is considered usable for processing.
    /// </summary>
    public double CoreDiameterInches { get; set; } = 4.0; 

    /// <summary>
    /// Thickness of the veneer in millimeters. This value is used to determine the usable radius of the log.
    /// </summary>
    public double VeneerThicknessMM { get; set; } = 3.2;  

    /// <summary>
    /// Threshold for thin cleanup warning in millimeters. If the usable radius is below this threshold, a warning will be issued.
    /// </summary>
    public double ThinCleanupWarningMM { get; set; } = 1.0;  
}


/// <summary>
/// Represents the settings for scoring analysis, including weights and thresholds for different scoring metrics.
/// </summary>
public sealed class ScoringSettings
{
    /// <summary>
    /// Threshold for good wane balance in inches. Wane balance measurements below this threshold are considered good.
    /// </summary>
    public double WaneBalanceGoodIn { get; set; } = 0.050; 

    /// <summary>
    /// Threshold for bad wane balance in inches. Wane balance measurements above this threshold are considered bad.
    /// </summary>
    public double WaneBalanceBadIn { get; set; } = 0.250;  

    /// <summary>
    /// Weight assigned to wane balance in the scoring system as a percentage.
    /// </summary>
    public double WaneBalanceWeightPercent { get; set; } = 25.0;  

    /// <summary>
    /// Threshold for good low wood risk as a percentage. Low wood risk measurements below this threshold are considered good.
    /// </summary>
    public double LowWoodRiskGoodPercent { get; set; } = 25.0;  

    /// <summary>
    /// Threshold for bad low wood risk as a percentage. Low wood risk measurements above this threshold are considered bad.
    /// </summary>
    public double LowWoodRiskBadPercent { get; set; } = 100.0; 

    /// <summary>
    /// Weight assigned to low wood risk in the scoring system as a percentage.
    /// </summary>
    public double LowWoodRiskWeightPercent { get; set; } = 35.0;  

    /// <summary>
    /// Threshold for good deepest low wood in inches. Deepest low wood measurements below this threshold are considered good.
    /// </summary>
    public double DeepestLowWoodGoodIn { get; set; } = 0.050;   

    /// <summary>
    /// Threshold for bad deepest low wood in inches. Deepest low wood measurements above this threshold are considered bad.
    /// </summary>
    public double DeepestLowWoodBadIn { get; set; } = 0.250;  

    /// <summary>
    /// Weight assigned to deepest low wood in the scoring system as a percentage.
    /// </summary>
    public double DeepestLowWoodWeightPercent { get; set; } = 10.0;  

    /// <summary>
    /// Threshold for good spindle movement in inches. Spindle movement measurements below this threshold are considered good.
    /// </summary>
    public double SpindleMovementGoodIn { get; set; } = 0.100;  

    /// <summary>
    /// Threshold for bad spindle movement in inches. Spindle movement measurements above this threshold are considered bad.
    /// </summary>
    public double SpindleMovementBadIn { get; set; } = 0.500;  

    /// <summary>
    /// Weight assigned to spindle movement in the scoring system as a percentage.
    /// </summary>
    public double SpindleMovementWeightPercent { get; set; } = 15.0;  

    /// <summary>
    /// Threshold for good out-of-roundness in inches. Out-of-roundness measurements below this threshold are considered good.
    /// </summary>
    public double OutOfRoundGoodIn { get; set; } = 0.100; 

    /// <summary>
    /// Threshold for bad out-of-roundness in inches. Out-of-roundness measurements above this threshold are considered bad.
    /// </summary>
    public double OutOfRoundBadIn { get; set; } = 0.500;  

    /// <summary>
    /// Weight assigned to out-of-roundness in the scoring system as a percentage.
    /// </summary>
    public double OutOfRoundWeightPercent { get; set; } = 10.0;  

    /// <summary>
    /// Threshold for good centerline sweep in inches. Centerline sweep measurements below this threshold are considered good.
    /// </summary>
    public double CenterlineSweepGoodIn { get; set; } = 0.100;  

    /// <summary>
    /// Threshold for bad centerline sweep in inches. Centerline sweep measurements above this threshold are considered bad.
    /// </summary>
    public double CenterlineSweepBadIn { get; set; } = 0.750; 

    /// <summary>
    /// Weight assigned to centerline sweep in the scoring system as a percentage.
    /// </summary>
    public double CenterlineSweepWeightPercent { get; set; } = 5.0;  
}


/// <summary>
/// Represents the settings for adaptive scoring, including parameters for adaptive scoring algorithms and criteria.
/// </summary>
public sealed class AdaptiveScoringSettings
{

    /// <summary>
    /// Indicates whether adaptive scoring is enabled. If true, the application will use adaptive scoring algorithms to adjust scoring based on log characteristics; if false, standard scoring will be used.
    /// </summary>
    public bool Enabled { get; set; } = true; 

    /// <summary>
    /// Indicates whether the defect map should be rebuilt using adaptive thresholds based on the initial analysis of the log. If true, the defect map is rebuilt; if false, the existing defect map is used.
    /// </summary>
    public bool RebuildDefectMapWithAdaptiveThresholds { get; set; } = true;

    /// <summary>
    /// Threshold for high sweep in inches. Logs with sweep measurements above this threshold are classified as high sweep logs.
    /// </summary>
    public double HighSweepIn { get; set; } = 0.400; 
    /// <summary>
    /// Threshold for high bow in inches. Logs with bow measurements above this threshold are classified as high bow logs.
    /// </summary>
    public double HighBowIn { get; set; } = 0.200; 
    /// <summary>
    /// Threshold for high out-of-roundness in inches. Logs with out-of-roundness measurements above this threshold are classified as high out-of-round logs.
    /// </summary>
    public double HighOutOfRoundIn { get; set; } = 0.400; 

    /// <summary>
    /// Threshold for high low wood percentage. Logs with low wood percentages above this threshold are classified as high low wood logs.
    /// </summary>
    public double HighLowWoodPercent { get; set; } = 8.0;  
    /// <summary>
    /// Threshold for high severe low wood percentage. Logs with severe low wood percentages above this threshold are classified as high severe low wood logs.
    /// </summary>
    public double HighSevereLowWoodPercent { get; set; } = 1.0;  
    /// <summary>
    /// Threshold for deep low wood in inches. Logs with deepest low wood measurements above this threshold are classified as deep low wood logs.
    /// </summary>
    public double DeepLowWoodIn { get; set; } = 0.150;  

    /// <summary>
    /// Threshold for maximum clean low wood percentage. Logs with low wood percentages below this threshold are classified as clean logs.
    /// </summary>
    public double CleanLowWoodMaxPercent { get; set; } = 2.0;  
    /// <summary>
    /// Threshold for maximum clean sweep in inches. Logs with sweep measurements below this threshold are classified as clean logs.
    /// </summary>
    public double CleanSweepMaxIn { get; set; } = 0.250; 
    /// <summary>
    /// Threshold for maximum clean out-of-roundness in inches. Logs with out-of-roundness measurements below this threshold are classified as clean logs.
    /// </summary>
    public double CleanOutOfRoundMaxIn { get; set; } = 0.250;

    /// <summary>
    /// Multiplier applied to low wood weight for swept logs in the scoring system. This value adjusts the weight of low wood measurements for logs classified as swept logs, affecting their overall score.
    /// </summary>
    public double SweptLogLowWoodWeightMultiplier { get; set; } = 1.35;

    /// <summary>
    /// Multiplier applied to sweep weight for swept logs in the scoring system. This value adjusts the weight of sweep measurements for logs classified as swept logs, affecting their overall score.
    /// </summary>
    public double SweptLogSweepWeightMultiplier { get; set; } = 1.35; 
    /// <summary>
    /// Multiplier applied to movement weight for swept logs in the scoring system. This value adjusts the weight of movement measurements for logs classified as swept logs, affecting their overall score.
    /// </summary>
    public double SweptLogMovementWeightMultiplier { get; set; } = 0.70; 

    /// <summary>
    /// Multiplier applied to low wood weight for low wood logs in the scoring system. This value adjusts the weight of low wood measurements for logs classified as low wood logs, affecting their overall score.
    /// </summary>
    public double LowWoodLogLowWoodWeightMultiplier { get; set; } = 1.60; 
    /// <summary>
    /// Multiplier applied to deepest low wood weight for low wood logs in the scoring system. This value adjusts the weight of deepest low wood measurements for logs classified as low wood logs, affecting their overall score.
    /// </summary>
    public double LowWoodLogDeepestWeightMultiplier { get; set; } = 1.40; 
    /// <summary>
    /// Multiplier applied to movement weight for low wood logs in the scoring system. This value adjusts the weight of movement measurements for logs classified as low wood logs, affecting their overall score.
    /// </summary>
    public double LowWoodLogMovementWeightMultiplier { get; set; } = 0.65;  

    /// <summary>
    /// Multiplier applied to out-of-round weight for oval logs in the scoring system. This value adjusts the weight of out-of-round measurements for logs classified as oval logs, affecting their overall score.
    /// </summary>
    public double OvalLogOutOfRoundWeightMultiplier { get; set; } = 1.45; 
    /// <summary>
    /// Multiplier applied to low wood weight for oval logs in the scoring system. This value adjusts the weight of low wood measurements for logs classified as oval logs, affecting their overall score.
    /// </summary>
    public double OvalLogLowWoodWeightMultiplier { get; set; } = 1.20;  

    /// <summary>
    /// Multiplier applied to movement weight for clean logs in the scoring system. This value adjusts the weight of movement measurements for logs classified as clean logs, affecting their overall score.
    /// </summary>
    public double CleanLogMovementWeightMultiplier { get; set; } = 1.60; 
    /// <summary>
    /// Multiplier applied to low wood weight for clean logs in the scoring system. This value adjusts the weight of low wood measurements for logs classified as clean logs, affecting their overall score.
    /// </summary>
    public double CleanLogLowWoodWeightMultiplier { get; set; } = 0.80;

    /// <summary>
    /// Multiplier applied to the low radius threshold for sensitive scoring in the scoring system. This value adjusts the threshold for
    /// low radius measurements when sensitive scoring is enabled, affecting how low radius defects are evaluated.
    /// </summary>
    public double SensitiveLowRadiusMultiplier { get; set; } = 0.85;

    /// <summary>
    /// Multiplier applied to the low radius threshold for relaxed scoring in the scoring system. This value adjusts the threshold for
    /// </summary>
    public double RelaxedLowRadiusMultiplier { get; set; } = 1.25;

    /// <summary>
    /// Minimum weight assigned to movement penalties in the scoring system during optimization. This value defines the lower
    /// bound for how much movement penalties can influence the overall score during the optimization process.
    /// </summary>
    public double MinimumMovementPenaltyWeight { get; set; } = 0.01; 
    /// <summary>
    /// Maximum weight assigned to movement penalties in the scoring system during optimization. This value defines the upper
    /// bound for how much movement penalties can influence the overall score during the optimization process.
    /// </summary>
    public double MaximumMovementPenaltyWeight { get; set; } = 0.20; 
}

/// <summary>
/// Represents the settings for defect analysis, including thresholds for good, warning, and bad defect measurements.
/// </summary>
public sealed class DefectSettings
{
    /// <summary>
    /// Threshold for low radius defects in millimeters. Defects with measured radius below this threshold are considered low radius defects.
    /// </summary>
    public double LowRadiusThresholdMM { get; set; } = -2.0; 
    /// <summary>
    /// Threshold for severe low radius defects in millimeters. Defects with measured radius significantly below the nominal radius are considered severe low radius defects.
    /// </summary>
    public double SevereLowRadiusThresholdMM { get; set; } = -4.0;  
    /// <summary>
    /// Threshold for high radius defects in millimeters. Defects with measured radius above the nominal radius are considered high radius defects.
    /// </summary>
    public double HighRadiusThresholdMM { get; set; } = 2.0; 

    /// <summary>
    /// Weight assigned to low radius defects in the scoring system.
    /// </summary>
    public double LowRadiusWeight { get; set; } = 1.0;  
    /// <summary>
    /// Weight assigned to severe low radius defects in the scoring system.
    /// </summary>
    public double SevereLowRadiusWeight { get; set; } = 3.0;  
    /// <summary>
    /// Weight assigned to high radius defects in the scoring system.
    /// </summary>
    public double HighRadiusWeight { get; set; } = 0.35; 

    /// <summary>
    /// Weight assigned to the depth of patch defects in the scoring system.
    /// </summary>
    public double PatchDepthWeight { get; set; } = 0.25;  
    /// <summary>
    /// Weight assigned to the number of cells in patch defects in the scoring system.
    /// </summary>
    public double PatchCellWeight { get; set; } = 0.002;  
    /// <summary>
    /// Weight assigned to the severity of patch defects in the scoring system.
    /// </summary>
    public double PatchSeverityWeight { get; set; } = 0.002;  
    /// <summary>
    /// Weight assigned to spindle relief defects in the scoring system.
    /// </summary>
    public double SpindleReliefWeight { get; set; } = 0.10;  
}


/// <summary>
/// Represents the settings for optimization analysis, including parameters for optimization algorithms and criteria.
/// </summary>
public sealed class OptimizationSettings
{
   
    /// <summary>
    /// Minimum percentage improvement required for the optimized setup to be considered better than the baseline setup.
    /// </summary>
    public double MinimumImprovementPercent { get; set; } = 2.0;

    /// <summary>
    /// Maximum movement in millimeters allowed during the coarse search phase of the optimization process. This value defines the upper limit
    /// for how much the spindle can move during the initial coarse search for optimal settings.
    /// </summary>
    public double CoarseMaxMoveMM { get; set; } = 20.0;

    /// <summary>
    /// Step size in millimeters for the coarse search phase of the optimization process. This value defines the increment by which the spindle moves during the coarse search for optimal settings.
    /// </summary>
    public double CoarseStepMM { get; set; } = 5.0;


    /// <summary>
    /// Maximum search radius in millimeters for the medium search phase of the optimization process. This value defines the upper limit for how far the
    /// optimizer will search for optimal settings during the medium search phase.
    /// </summary>
    public double MediumSearchRadiusMM { get; set; } = 5.0; 
    public double MediumStepMM { get; set; } = 2.5; 

    public double FineSearchRadiusMM { get; set; } = 2.5;  
    public double FineStepMM { get; set; } = 1.0;  



    /// <summary>
    /// Weight assigned to movement penalties in the scoring system.
    /// </summary>
    public double MovementPenaltyWeight { get; set; } = 0.05;  

    /// <summary>
    /// When true, the optimizer will use a candidate-aware search score that considers both the baseline and candidate scores during the search for optimal settings.
    /// </summary>
    public bool UseCandidateAwareSearchScore { get; set; } = true; 

    /// <summary>
    /// How much worse the candidate wane balance is allowed to get before
    /// the optimizer adds a guardrail penalty.
    /// </summary>
    public double MaximumAllowedWaneIncreasePercent { get; set; } = 15.0;

    /// <summary>
    /// Weight applied to the wane guardrail penalty in the scoring system. This value defines how much
    /// the optimizer penalizes candidates that exceed the maximum allowed wane increase percentage.
    /// </summary>
    public double WaneGuardPenaltyWeight { get; set; } = 1.0;

}


/// <summary>
/// Settings related to the inscribed geometry used in the optimization process.
/// </summary>  
public sealed class InscribedGeometrySettings
{
    /// <summary>
    /// Circumferential envelope resolution used by the cylinder/oval search.
    /// With 36 rotation frames, 36 bins preserves the measured 10-degree spacing.
    /// </summary>
    public int AngularBins { get; set; } = 36;

    /// <summary>
    /// Reject candidate axes that do not have enough measured angular coverage.
    /// </summary>
    public int MinimumValidAngularBins { get; set; } = 30;

    /// <summary>
    /// Subtracted from the measured envelope so the reported geometry does not
    /// sit directly on the measured surface.
    /// </summary>
    public double SafetyMarginMM { get; set; } = 0.5;

    /// <summary>
    /// Smallest minor/major semi-axis ratio considered by the oval search.
    /// </summary>
    public double MinimumAxisRatio { get; set; } = 0.55;

    public double AxisRatioStep { get; set; } = 0.05;
    public double OrientationStepDeg { get; set; } = 10.0;

    /// <summary>
    /// When two candidates have the same geometry within this tolerance,
    /// prefer the one requiring less spindle movement.
    /// </summary>
    public double GeometryTieToleranceMM { get; set; } = 0.01;
}

/// <summary>
/// Defines the operating modes for the charger, indicating whether it is in engineering, commissioning, or production mode.
/// </summary>
public enum ChargerOperatingMode
{
    Engineering = 0,
    Commissioning = 1,
    Production = 2
}


/// <summary>
/// Represents the settings for production, including parameters for production readiness and commissioning checks.
/// </summary>
public sealed class ProductionSettings
{
    public ChargerOperatingMode OperatingMode { get; set; } = ChargerOperatingMode.Commissioning;

    /// <summary>
    /// Production PLC writes remain blocked until every commissioning item below
    /// is explicitly completed on the real machine.
    /// </summary>
    public bool CameraSeamCalibrationVerified { get; set; }
    public bool ScannerCalibrationVerified { get; set; }
    public bool MachineTravelLimitsVerified { get; set; }
    public bool PlcSignConventionVerified { get; set; }
    public bool KnifeClearanceVerified { get; set; }
    public bool EmergencyStopAndInterlocksVerified { get; set; }
    public bool PlcRuntimeLicenseVerified { get; set; }

    public string CommissioningApprovedBy { get; set; } = string.Empty;
    public DateTime? CommissioningApprovedUtc { get; set; }

    public bool RequireExactFrameCount { get; set; } = true;
    public bool RequireEncoderAcquisition { get; set; } = true;
    public int MaximumDroppedSourceFrames { get; set; }
    public int MinimumPointCloudPoints { get; set; } = 5000;
    public int MinimumLocalCenterlineSlices { get; set; } = 20;
    public int MinimumBinnedRadiusCells { get; set; } = 500;
    public bool RequireResultSaveBeforePlcWrite { get; set; } = true;

    public bool CommissioningComplete =>
        CameraSeamCalibrationVerified &&
        ScannerCalibrationVerified &&
        MachineTravelLimitsVerified &&
        PlcSignConventionVerified &&
        KnifeClearanceVerified &&
        EmergencyStopAndInterlocksVerified &&
        PlcRuntimeLicenseVerified &&
        !string.IsNullOrWhiteSpace(CommissioningApprovedBy) &&
        CommissioningApprovedUtc.HasValue;
}

/// <summary>
/// Represents the settings for PLC integration, including parameters for enabling the integration,
/// automatic capture, result sending, and verification requirements.
/// </summary>
public sealed class PlcIntegrationSettings
{
    public bool Enabled { get; set; } = true;
    public bool AutoStartCaptureOnScanStart { get; set; } = true;
    public bool AutoSendResultsAfterSave { get; set; } = true;
    public bool RequireReadbackVerification { get; set; } = true;
    public bool RequireGoodQuality { get; set; } = true;
    public int ReadbackTimeoutMilliseconds { get; set; } = 5000;
    public double FloatComparisonTolerance { get; set; } = 0.01;
    public bool WriteCarriageRetractFirst { get; set; } = true;

    /// <summary>
    /// Maximum amount of time to wait for the required PLC network
    /// before creating the application's XY connection.
    /// </summary>
    public int StartupNetworkWaitMilliseconds { get; set; } = 10000;

    /// <summary>
    /// Interval between PLC network availability checks during startup.
    /// </summary>
    public int StartupNetworkCheckIntervalMilliseconds { get; set; } = 500;

    /// <summary>
    /// In production, processing waits for the PLC-controlled rotation cycle to
    /// assert ScanComplete after all 36 synchronized frame pairs are present.
    /// </summary>
    public bool RequireScanCompleteBeforeProcessing { get; set; } = true;

    public int ScanCompleteTimeoutMilliseconds { get; set; } = 30000;
}

public sealed class SafetySettings
{
    /// <summary>
    /// Clearance from the measured largest log radius used for the carriage
    /// retract command. The requested production rule is ten peel thicknesses.
    /// </summary>
    public double KnifeClearancePeelThicknessMultiplier { get; set; } = 10.0;

    /// <summary>
    /// Optional absolute floor. The effective clearance is the larger of this
    /// value and the peel-thickness multiple.
    /// </summary>
    public double MinimumKnifeClearanceMM { get; set; }

    /// <summary>
    /// Optional machine-specific clearance added after the ten-thickness rule.
    /// Keep at zero until measured during commissioning.
    /// </summary>
    public double AdditionalCarriageRetractBufferMM { get; set; }

    public bool BlockOnMovementWarning { get; set; } = true;
    public bool RequireFiniteCommands { get; set; } = true;

    /// <summary>
    /// When true, skips the local knife clearance validation check during production readiness
    /// and trusts that the PLC has been properly configured with knife clearance values.
    /// Set this to true in production to allow PLC-based carriage retract control without
    /// blocking on the application-side safety check.
    /// </summary>
    public bool ReadKnifeClearanceFromPlc { get; set; } = false;

    /// <summary>
    /// Computes the knife clearance in millimeters based on the given veneer thickness and the configured safety settings.
    /// The effective clearance is calculated as the maximum of the minimum knife clearance and the product
    /// of the veneer thickness and the knife clearance multiplier, plus any additional carriage retract buffer.
    /// </summary>
    /// <param name="veneerThicknessMM">The thickness of the veneer in millimeters.</param>
    /// <returns>The computed knife clearance in millimeters.</returns>
    public double ComputeKnifeClearanceMM(double veneerThicknessMM)
    {
        
        double effectiveMultiplier =
            Math.Max(10.0, KnifeClearancePeelThicknessMultiplier);

        double multipleClearance =
            Math.Max(0.0, veneerThicknessMM) *
            effectiveMultiplier;

        return Math.Max(MinimumKnifeClearanceMM, multipleClearance) +
               AdditionalCarriageRetractBufferMM;
    }
}

/// <summary>
/// Represents the settings for recovery optimization, including parameters for
/// candidate inclusion, direct refinement, and search coverage checks.
/// </summary>
public sealed class RecoveryOptimizationSettings
{
    /// <summary>
    /// Recovery mode compares baseline, penalty-search, cylinder, oval, and a
    /// bounded direct veneer-recovery refinement using the physical circular peel.
    /// </summary>
    public bool IncludeBaselineCandidate { get; set; } = true;
    public bool IncludePenaltyCandidate { get; set; } = true;
    public bool IncludeCylinderCandidate { get; set; } = true;
    public bool IncludeOvalCandidate { get; set; } = true;
    public bool EnableDirectRecoveryRefinement { get; set; } = true;
    public bool PreferLowerMovementOnExactTie { get; set; } = true;

    /// <summary>
    /// Local XY search around each strategy seed. A 2 mm radius with a 2 mm
    /// step produces at most 81 combinations per seed before deduplication.
    /// </summary>
    public double DirectRefinementRadiusMM { get; set; } = 2.0;
    public double DirectRefinementStepMM { get; set; } = 2.0;
    public int MaximumDirectRecoveryCandidates { get; set; } = 500;

    /// <summary>
    /// Sparse scans are rejected before direct recovery search. These are search
    /// coverage checks in addition to the global production-readiness checks.
    /// </summary>
    public int MinimumRecoverySearchZBins { get; set; } = 20;
    public int MinimumRecoverySearchAngularBins { get; set; } = 30;
}

/// <summary>
/// Represents the settings for machine travel limits, including maximum allowed movements for the tail and head in both X and Y directions.
/// </summary>
public sealed class MachineLimitSettings
{
    /// <summary>
    /// Maximum movement allowed for the tail in the X and Y direction in millimeters.
    /// This value defines the upper limit for how far the tail can move in the X and Y direction during operation.
    /// </summary>
    public double MaxTailMoveXMM { get; set; } = 100; 
    public double MaxTailMoveYMM { get; set; } = 100; 
    public double MaxHeadMoveXMM { get; set; } = 100;  
    public double MaxHeadMoveYMM { get; set; } = 100; 

    public double WarningMoveMM { get; set; } = 80;  // Warning threshold for movement in millimeters. If the movement exceeds this value, a warning will be issued.
}


/// <summary>
/// Represents the settings for diagnostics, including options to include engineering details, timing reports, and settings summaries in the diagnostics report.
/// </summary>
public sealed class DiagnosticsSettings
{

    /// <summary>
    /// Indicates whether to include engineering details in the diagnostics report. If true, the report will
    /// contain detailed engineering information; if false, this information will be omitted.
    /// </summary>
    public bool IncludeEngineeringDetails { get; set; } = true; 

    /// <summary>
    /// Indicates whether to include timing report in the diagnostics report. If true, the report will
    /// contain timing information; if false, this information will be omitted.
    /// </summary>
    public bool IncludeTimingReport { get; set; } = true;  

    /// <summary>
    /// Indicates whether to include settings summary in the diagnostics report. If true, the report will
    /// contain a summary of the settings; if false, this information will be omitted.
    /// </summary>
    public bool IncludeSettingsSummary { get; set; } = true;   
}


/// <summary>
/// Represents the statistics for sheet quality analysis, including total cells analyzed,
/// usable cells, wane-affected cells, and severe wane-affected cells.
/// </summary>
public sealed class SheetQualityStats
{
    public int TotalCells { get; set; }
    public int UsableCells { get; set; } 
    public int WaneCells { get; set; } 
    public int SevereWaneCells { get; set; }  
}

/// <summary>
/// Represents information about a scan folder, including the date, block number, and scan number.
/// </summary>  
public class ScanFolderInfo
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public int BlockNumber { get; set; }
    public int ScanNumber { get; set; }

    public string BlockName { get; set; } = string.Empty;
    public string BlockFolderPath { get; set; } = string.Empty;
}


/// <summary>
/// Represents the settings for database integration, including parameters for enabling
/// database saving and specifying the connection string.
/// </summary>
public sealed class DatabaseSettings
{
    public bool EnableDatabaseSaving { get; set; }

    public string ConnectionString { get; set; } = string.Empty;
}

/// <summary>
/// Represents the settings for production setup, including parameters for database configuration,
/// camera verification, laser detection, length calibration, and scanner calibration.
/// </summary>
public sealed class ProductionSetupSettings
{
    public bool DatabaseConfigured { get; set; }
    public bool CameraConfigurationVerified { get; set; }
    public bool CameraPhysicalPositionsConfirmed { get; set; }
    public bool LaserDetectionVerified { get; set; }
    public bool LengthCalibrationConfigured { get; set; }
    public double PendingLengthPixelsPerMM { get; set; }
    public DateTime? LengthCalibrationMeasuredUtc { get; set; }
    public bool ScannerCalibrationCreated { get; set; }
    public bool ScannerCalibrationRepeatabilityVerified { get; set; }
    public string ActiveCalibrationId { get; set; } = string.Empty;
    public DateTime? LastSetupCompletedUtc { get; set; }

    public bool MinimumSetupComplete =>
        DatabaseConfigured &&
        CameraConfigurationVerified &&
        CameraPhysicalPositionsConfirmed &&
        LaserDetectionVerified &&
        LengthCalibrationConfigured &&
        double.IsFinite(PendingLengthPixelsPerMM) &&
        PendingLengthPixelsPerMM > 0 &&
        ScannerCalibrationCreated &&
        ScannerCalibrationRepeatabilityVerified &&
        !string.IsNullOrWhiteSpace(ActiveCalibrationId);
}

/// <summary>
/// Represents the settings for kinematic rotation, including whether to enable the kinematic position selection UI.
/// </summary>
public sealed class KinematicSettings
{
    /// <summary>
    /// Enables or disables the kinematic position selection message box that appears during production processing.
    /// When false (default), the message box will not appear and production will proceed without kinematic selection.
    /// When true, operators will be prompted to choose between kinematic rotation or current XY positions.
    /// </summary>
    public bool EnableKinematicPositionSelection { get; set; } = false;
}
