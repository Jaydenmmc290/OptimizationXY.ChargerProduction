using OptimizationXY.Charger.Models.Acquisition;
using OptimizationXY.Charger.Models.Optimization;



namespace OptimizationXY.Charger.Models.Settings;

public sealed class AppSettings
{
    public int SettingsVersion { get; set; } = 10; // Version of the settings schema. Increment this value when making changes to the settings
                                                  // structure to ensure compatibility with saved settings files.

    public CameraAcquisitionSettings Acquisition { get; set; } = new();

    public OperatorViewSettings OperatorView { get; set; } = new();

    public UnitSettings Units { get; set; } = new(); //  Unit settings for the application, including internal math units and display preferences.
    public RotatingScanSettings RotatingScan { get; set; } = new(); // Settings related to the rotating scan process, including minimum and preferred valid frames, expected angle step, and tolerances.

    public PeelViewSettings PeelView { get; set; } = new();  // Settings for the peel view display, including maximum linear gap and angular neighbor frames for display fill.

    public GeometrySettings Geometry { get; set; } = new();  // Settings related to geometry analysis, including local centerline slice thickness, minimum points per slice, radius field bin sizes, and minimum points per radius bin.
    public SweepSettings Sweep { get; set; } = new(); // Settings for sweep analysis, including end trim fraction and thresholds for good, warning, and bad sweep measurements.

    public WaneSettings Wane { get; set; } = new(); // Settings for wane analysis, including thresholds for good, warning, and bad wane measurements.
    public UsableRadiusSettings UsableRadius { get; set; } = new(); // Settings for usable radius analysis, including thresholds for good, warning, and bad usable radius measurements.

    public DefectSettings Defects { get; set; } = new(); // Settings for defect analysis, including thresholds for good, warning, and bad defect measurements.
    public OptimizationSettings Optimization { get; set; } = new(); // Settings for optimization analysis, including parameters for optimization algorithms and criteria.
    public SpindleOptimizationMode OptimizationMode { get; set; } = SpindleOptimizationMode.RecoveryOptimized;
    public InscribedGeometrySettings InscribedGeometry { get; set; } = new();
    public ScoringSettings Scoring { get; set; } = new(); // Settings for scoring analysis, including weights and thresholds for different scoring metrics.

    public AdaptiveScoringSettings AdaptiveScoring { get; set; } = new(); // Settings for adaptive scoring, including parameters for adaptive scoring algorithms and criteria.
    public MachineLimitSettings MachineLimits { get; set; } = new(); // Settings for machine limits, including maximum and minimum operational parameters.
    public ProductionSettings Production { get; set; } = new();
    public PlcIntegrationSettings Plc { get; set; } = new();
    public SafetySettings Safety { get; set; } = new();
    public RecoveryOptimizationSettings RecoveryOptimization { get; set; } = new();
    public DiagnosticsSettings Diagnostics { get; set; } = new(); // Settings for diagnostics, including thresholds and parameters for diagnostic checks.

    public ScanFolderInfo Scan { get; set; } = new();

    public DatabaseSettings Database { get; set; } = new();
    public ProductionSetupSettings Setup { get; set; } = new();
    public ScanStorageSettings Storage { get; set; } = new();
}

public sealed class UnitSettings
{
    // Keep math in mm. Only final/operator output should be inches-first.
    public bool InternalMathUsesMM { get; set; } = true;  // Indicates whether internal calculations use millimeters (true) or inches (false).
    public bool DisplayInchesFirst { get; set; } = true;  // Indicates whether to display measurements in inches first (true) or millimeters first (false).
    public int InchesDecimals { get; set; } = 3;  // Number of decimal places to display for inch measurements.
    public int MillimetersDecimals { get; set; } = 2;  // Number of decimal places to display for millimeter measurements.
}

public sealed class RotatingScanSettings
{
    public int MinimumValidFrames { get; set; } = 12;  // Minimum number of valid frames required for a successful rotating scan.
    public int PreferredValidFrames { get; set; } = 36;  // Preferred number of valid frames for optimal rotating scan results.

    public double ExpectedAngleStepDeg { get; set; } = 10.0;  // Expected angle step in degrees between consecutive frames during the rotating scan.

    public int MinimumProfilePointsPerFrame { get; set; } = 50; //  Minimum number of profile points required per frame for a valid rotating scan.

    public int DisplayFillMaxLinearGapPixels { get; set; } = 20;
    public int DisplayFillMaxAngularNeighborFrames { get; set; } = 2;

    public double MinValidEndDiameterFraction { get; set; } = 0.18;
    public bool FillMissingPoints { get; internal set; }
}

public sealed class GeometrySettings
{
    public double LocalCenterlineSliceThicknessMM { get; set; } = 10.0;  // Thickness of each slice used for local centerline analysis in millimeters.
    public int MinPointsPerLocalCenterlineSlice { get; set; } = 8;  //  Minimum number of points required per local centerline slice for valid analysis.

    public double RadiusFieldZBinSizeMM { get; set; } = 10.0;  // Size of the Z-axis bins used for radius field analysis in millimeters.
    public double RadiusFieldAngleBinSizeDeg { get; set; } = 10.0;  // Size of the angle bins used for radius field analysis in degrees.
    public int MinPointsPerRadiusBin { get; set; } = 1;  //     Minimum number of points required per radius bin for valid analysis.

}

public sealed class SweepSettings
{
    public double EndTrimFraction { get; set; } = 0.03; // Fraction of the log length to trim from each end for sweep analysis.

    public int MinimumEndSlices { get; set; } = 3; // Minimum number of slices to consider at each end of the log for sweep analysis.
    public double EndSliceFraction { get; set; } = 0.05; // Fraction of the log length to consider for end slices in sweep analysis.

}

public sealed class WaneSettings
{
    public double WaneToleranceMM { get; set; } = 1.0;  // Tolerance for wane measurements in millimeters. Wane measurements within this tolerance are considered acceptable.
    public double SevereWaneMM { get; set; } = 3.0;  // Threshold for severe wane measurements in millimeters. Wane measurements exceeding this threshold are considered severe.

    public int MinAngleBinsPerWaneSlice { get; set; } = 12; // Minimum number of angle bins required per wane slice for valid analysis.

}

public sealed class UsableRadiusSettings
{
    public double CoreDiameterInches { get; set; } = 4.0; // Diameter of the core in inches. This value is used to determine the usable radius of the log.
    public double VeneerThicknessMM { get; set; } = 3.2;  // Thickness of the veneer in millimeters. This value is used to determine the usable radius of the log.

    public double ThinCleanupWarningMM { get; set; } = 1.0;  // Threshold for thin cleanup warning in millimeters. If the usable radius is below this threshold, a warning will be issued.
}

public sealed class ScoringSettings
{
    public double WaneBalanceGoodIn { get; set; } = 0.050; // Threshold for good wane balance in inches. Wane balance measurements below this threshold are considered good.
    public double WaneBalanceBadIn { get; set; } = 0.250;  // Threshold for bad wane balance in inches. Wane balance measurements above this threshold are considered bad.
    public double WaneBalanceWeightPercent { get; set; } = 25.0;  // Weight assigned to wane balance in the scoring system as a percentage.

    public double LowWoodRiskGoodPercent { get; set; } = 25.0;  // Threshold for good low wood risk as a percentage. Low wood risk measurements below this threshold are considered good.
    public double LowWoodRiskBadPercent { get; set; } = 100.0; // Threshold for bad low wood risk as a percentage. Low wood risk measurements above this threshold are considered bad.
    public double LowWoodRiskWeightPercent { get; set; } = 35.0;  // Weight assigned to low wood risk in the scoring system as a percentage.

    public double DeepestLowWoodGoodIn { get; set; } = 0.050;   // Threshold for good deepest low wood in inches. Deepest low wood measurements below this threshold are considered good.
    public double DeepestLowWoodBadIn { get; set; } = 0.250;  // Threshold for bad deepest low wood in inches. Deepest low wood measurements above this threshold are considered bad.
    public double DeepestLowWoodWeightPercent { get; set; } = 10.0;  // Weight assigned to deepest low wood in the scoring system as a percentage.

    public double SpindleMovementGoodIn { get; set; } = 0.100;  //  Threshold for good spindle movement in inches. Spindle movement measurements below this threshold are considered good.
    public double SpindleMovementBadIn { get; set; } = 0.500;  // Threshold for bad spindle movement in inches. Spindle movement measurements above this threshold are considered bad.
    public double SpindleMovementWeightPercent { get; set; } = 15.0;  // Weight assigned to spindle movement in the scoring system as a percentage.

    public double OutOfRoundGoodIn { get; set; } = 0.100; // Threshold for good out-of-roundness in inches. Out-of-roundness measurements below this threshold are considered good.
    public double OutOfRoundBadIn { get; set; } = 0.500;  // Threshold for bad out-of-roundness in inches. Out-of-roundness measurements above this threshold are considered bad.
    public double OutOfRoundWeightPercent { get; set; } = 10.0;  // Weight assigned to out-of-roundness in the scoring system as a percentage.

    public double CenterlineSweepGoodIn { get; set; } = 0.100;  //  Threshold for good centerline sweep in inches. Centerline sweep measurements below this threshold are considered good.
    public double CenterlineSweepBadIn { get; set; } = 0.750; // Threshold for bad centerline sweep in inches. Centerline sweep measurements above this threshold are considered bad.
    public double CenterlineSweepWeightPercent { get; set; } = 5.0;  // Weight assigned to centerline sweep in the scoring system as a percentage.
}

public sealed class AdaptiveScoringSettings
{
    public bool Enabled { get; set; } = true; // When true, adaptive scoring is enabled, allowing the system to adjust scoring thresholds based on the characteristics of the log being analyzed.

    public bool RebuildDefectMapWithAdaptiveThresholds { get; set; } = true; // When true, the defect map is rebuilt using adaptive thresholds based on the initial analysis of the log.

    // Log classification thresholds.
    public double HighSweepIn { get; set; } = 0.400; // Threshold for high sweep in inches. Logs with sweep measurements above this threshold are classified as high sweep logs.
    public double HighBowIn { get; set; } = 0.200; // Threshold for high bow in inches. Logs with bow measurements above this threshold are classified as high bow logs.
    public double HighOutOfRoundIn { get; set; } = 0.400; // Threshold for high out-of-roundness in inches. Logs with out-of-roundness measurements above this threshold are classified as high out-of-round logs.

    public double HighLowWoodPercent { get; set; } = 8.0;  // Threshold for high low wood percentage. Logs with low wood percentages above this threshold are classified as high low wood logs.
    public double HighSevereLowWoodPercent { get; set; } = 1.0;  // Threshold for high severe low wood percentage. Logs with severe low wood percentages above this threshold are classified as high severe low wood logs.
    public double DeepLowWoodIn { get; set; } = 0.150;  // Threshold for deep low wood in inches. Logs with deepest low wood measurements above this threshold are classified as deep low wood logs.

    public double CleanLowWoodMaxPercent { get; set; } = 2.0;  // Threshold for maximum clean low wood percentage. Logs with low wood percentages below this threshold are classified as clean logs.
    public double CleanSweepMaxIn { get; set; } = 0.250; // Threshold for maximum clean sweep in inches. Logs with sweep measurements below this threshold are classified as clean logs.
    public double CleanOutOfRoundMaxIn { get; set; } = 0.250; // Threshold for maximum clean out-of-roundness in inches. Logs with out-of-roundness measurements below this threshold are classified as clean logs.

    // Multipliers applied to copied scoring settings for the current log.
    public double SweptLogLowWoodWeightMultiplier { get; set; } = 1.35; // Multiplier applied to low wood weight for swept logs in the scoring system.
    public double SweptLogSweepWeightMultiplier { get; set; } = 1.35; // Multiplier applied to sweep weight for swept logs in the scoring system.
    public double SweptLogMovementWeightMultiplier { get; set; } = 0.70; // Multiplier applied to movement weight for swept logs in the scoring system.

    public double LowWoodLogLowWoodWeightMultiplier { get; set; } = 1.60; // Multiplier applied to low wood weight for low wood logs in the scoring system.
    public double LowWoodLogDeepestWeightMultiplier { get; set; } = 1.40; // Multiplier applied to deepest low wood weight for low wood logs in the scoring system.
    public double LowWoodLogMovementWeightMultiplier { get; set; } = 0.65;  //  Multiplier applied to movement weight for low wood logs in the scoring system.

    public double OvalLogOutOfRoundWeightMultiplier { get; set; } = 1.45; // Multiplier applied to out-of-round weight for oval logs in the scoring system.
    public double OvalLogLowWoodWeightMultiplier { get; set; } = 1.20;  //  Multiplier applied to low wood weight for oval logs in the scoring system.

    public double CleanLogMovementWeightMultiplier { get; set; } = 1.60; // Multiplier applied to movement weight for clean logs in the scoring system.
    public double CleanLogLowWoodWeightMultiplier { get; set; } = 0.80; // Multiplier applied to low wood weight for clean logs in the scoring system.

    // Defect-map threshold behavior.
    // Values closer to zero mark more cells as low wood.
    public double SensitiveLowRadiusMultiplier { get; set; } = 0.85;  // -2.0 -> -1.7 mm
    public double RelaxedLowRadiusMultiplier { get; set; } = 1.25;    // -2.0 -> -2.5 mm

    // Internal optimizer movement penalty behavior.
    public double MinimumMovementPenaltyWeight { get; set; } = 0.01; // Minimum weight assigned to movement penalties in the scoring system during optimization.
    public double MaximumMovementPenaltyWeight { get; set; } = 0.20; // Maximum weight assigned to movement penalties in the scoring system during optimization.
}

public sealed class DefectSettings
{
    // Error = measured radius - nominal radius.
    public double LowRadiusThresholdMM { get; set; } = -2.0; // Threshold for low radius defects in millimeters (measured radius below nominal radius).
    public double SevereLowRadiusThresholdMM { get; set; } = -4.0;  // Threshold for severe low radius defects in millimeters (measured radius significantly below nominal radius).
    public double HighRadiusThresholdMM { get; set; } = 2.0; // Threshold for high radius defects in millimeters (measured radius above nominal radius).

    public double LowRadiusWeight { get; set; } = 1.0;  // Weight assigned to low radius defects in the scoring system.
    public double SevereLowRadiusWeight { get; set; } = 3.0;  //    Weight assigned to severe low radius defects in the scoring system.
    public double HighRadiusWeight { get; set; } = 0.35;  // Weight assigned to high radius defects in the scoring system.

    public double PatchDepthWeight { get; set; } = 0.25;  // Weight assigned to the depth of patch defects in the scoring system.
    public double PatchCellWeight { get; set; } = 0.002;  // Weight assigned to the number of cells in patch defects in the scoring system.
    public double PatchSeverityWeight { get; set; } = 0.002;  // Weight assigned to the severity of patch defects in the scoring system.
    public double SpindleReliefWeight { get; set; } = 0.10;  // Weight assigned to spindle relief defects in the scoring system.
}

public sealed class OptimizationSettings
{
    // Final recommendation must beat baseline by this much before app says "use optimized setup".
    public double MinimumImprovementPercent { get; set; } = 2.0;  // Minimum percentage improvement required for the optimized setup to be considered better than the baseline setup.

    // Coarse search.
    public double CoarseMaxMoveMM { get; set; } = 20.0;  // Maximum movement allowed during the coarse search phase in millimeters.
    public double CoarseStepMM { get; set; } = 5.0;  // Step size for the coarse search phase in millimeters.

    // Medium search.
    public double MediumSearchRadiusMM { get; set; } = 5.0;  // Maximum search radius for the medium search phase in millimeters.
    public double MediumStepMM { get; set; } = 2.5;  // Step size for the medium search phase in millimeters.

    // Fine search.
    public double FineSearchRadiusMM { get; set; } = 2.5;  // Maximum search radius for the fine search phase in millimeters.
    public double FineStepMM { get; set; } = 1.0;  // Step size for the fine search phase in millimeters.

    // Peel recovery is evaluated by the virtual-peel stage. The spindle
    // search settings below contain only values actually used by the
    // penalty-scored search.

    public double MovementPenaltyWeight { get; set; } = 0.05;  // Weight assigned to movement penalties in the scoring system.

    public bool UseCandidateAwareSearchScore { get; set; } = true; // When true, the optimizer will use a candidate-aware search score that considers both the baseline and candidate scores during the search for optimal settings.

    // How much worse the candidate wane balance is allowed to get before
    // the optimizer adds a guardrail penalty.
    // Example: 35 means candidate wane may become 35% worse than baseline
    // before the guardrail starts pushing back.

    public double MaximumAllowedWaneIncreasePercent { get; set; } = 15.0;
    // Larger value = stronger pushback when wane gets worse than allowed.
    // Start with 1.0. If it still over-sacrifices wane, try 1.5 or 2.0.
    public double WaneGuardPenaltyWeight { get; set; } = 1.0;

}


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

public enum ChargerOperatingMode
{
    Engineering = 0,
    Commissioning = 1,
    Production = 2
}

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

    public double ComputeKnifeClearanceMM(double veneerThicknessMM)
    {
        // Defense in depth: the effective multiplier can never be lower
        // than the required ten peel thicknesses, even if settings are edited
        // outside the normal validated settings path.
        double effectiveMultiplier =
            Math.Max(10.0, KnifeClearancePeelThicknessMultiplier);

        double multipleClearance =
            Math.Max(0.0, veneerThicknessMM) *
            effectiveMultiplier;

        return Math.Max(MinimumKnifeClearanceMM, multipleClearance) +
               AdditionalCarriageRetractBufferMM;
    }
}

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

public sealed class MachineLimitSettings
{
    // These values must be measured and approved during commissioning.
    // Production PLC writes are blocked until MachineTravelLimitsVerified is true.
    public double MaxTailMoveXMM { get; set; } = 50.8; // Maximum movement allowed for the tail in the X direction in millimeters.
    public double MaxTailMoveYMM { get; set; } = 50.8; // Maximum movement allowed for the tail in the Y direction in millimeters.
    public double MaxHeadMoveXMM { get; set; } = 50.8;  // Maximum movement allowed for the head in the X direction in millimeters.
    public double MaxHeadMoveYMM { get; set; } = 50.8; // Maximum movement allowed for the head in the Y direction in millimeters.

    public double WarningMoveMM { get; set; } = 50.8;  // Warning threshold for movement in millimeters. If the movement exceeds this value, a warning will be issued.
}

public sealed class DiagnosticsSettings
{
    public bool IncludeEngineeringDetails { get; set; } = true;  // Indicates whether to include engineering details in the diagnostics report.

    public bool IncludeTimingReport { get; set; } = true;  // Indicates whether to include timing report in the diagnostics report.
    public bool IncludeSettingsSummary { get; set; } = true;   //   Indicates whether to include settings summary in the diagnostics report.
}

public sealed class SheetQualityStats
{
    public int TotalCells { get; set; } // Total number of cells analyzed for sheet quality. This value is used to calculate the recoverable veneer score.
    public int UsableCells { get; set; }  // Number of usable cells analyzed for sheet quality. This value is used to calculate the recoverable veneer score.
    public int WaneCells { get; set; } // Number of cells affected by wane analyzed for sheet quality. This value is used to calculate the recoverable veneer score.
    public int SevereWaneCells { get; set; }  // Number of cells affected by severe wane analyzed for sheet quality. This value is used to calculate the recoverable veneer score.
}

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

public sealed class DatabaseSettings
{
    public bool EnableDatabaseSaving { get; set; }

    public string ConnectionString { get; set; } = string.Empty;
}


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
