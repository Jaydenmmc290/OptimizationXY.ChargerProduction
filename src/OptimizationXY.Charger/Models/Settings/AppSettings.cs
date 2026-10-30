using OptimizationXY.Charger.Models.Acquisition;
using OptimizationXY.Charger.Models.Optimization;


// =============================================================================
// AppSettings.cs - every setting the program reads, in one tree
// -----------------------------------------------------------------------------
// The whole tree is saved as Config\appsettings.json (see AppSettingsStore and
// ApplicationPaths). The values written after "=" below are only FIRST-RUN
// DEFAULTS; the settings file on each computer overrides them.
// AppSettingsValidator checks the loaded values before they are used.
//
// Units: lengths are millimetres unless the name ends in "In"/"Inches"; angles
// are degrees; percentages are 0-100 unless the comment says "fraction".
//
// Machine conventions used in the comments: HEAD = end of the block nearest
// the lathe operator, TAIL = far end. In the scan point cloud Z = 0 is the
// tail end.
// =============================================================================
namespace OptimizationXY.Charger.Models.Settings;

/// <summary>
/// Root of the settings tree. Each property is one section of appsettings.json.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// The version of the settings file layout (currently 10). AppSettingsStore
    /// upgrades older files when it loads them.
    /// </summary>
    public int SettingsVersion { get; set; } = 10;


    /// <summary>
    /// Camera, frame-pairing and image-joining settings (both cameras). Part of the calibrated setup.
    /// </summary>
    public CameraAcquisitionSettings Acquisition { get; set; } = new();


    /// <summary>
    /// Lathe-operator screen settings and the scan-completeness thresholds behind its Good/Warning/Failed verdict.
    /// </summary>
    public OperatorViewSettings OperatorView { get; set; } = new();

    /// <summary>
    /// Unit settings for the application, including internal math units and display preferences.
    /// </summary>
    public UnitSettings Units { get; set; } = new(); 
    /// <summary>
    /// How frames from one revolution are checked and given angles, plus laser-line detection options.
    /// </summary>
    public RotatingScanSettings RotatingScan { get; set; } = new(); 

    /// <summary>
    /// Virtual-peel settings: sheet length, usable-sheet rules and the peel picture layout.
    /// </summary>
    public PeelViewSettings PeelView { get; set; } = new();  

    /// <summary>
    /// Slice thickness and radius-field cell sizes used to turn the point cloud into geometry.
    /// </summary>
    public GeometrySettings Geometry { get; set; } = new(); 
    /// <summary>
    /// How many slices at each end are averaged for the sweep/bow numbers.
    /// </summary>
    public SweepSettings Sweep { get; set; } = new(); 

    /// <summary>
    /// When a cell of the virtual peel counts as wane (missing wood) or severe wane.
    /// </summary>
    public WaneSettings Wane { get; set; } = new(); 
    /// <summary>
    /// Core size, veneer thickness and thin-cleanup warning used by the virtual peel.
    /// </summary>
    public UsableRadiusSettings UsableRadius { get; set; } = new(); 

    /// <summary>
    /// Radius-defect thresholds (mm below/above the slice median) and legacy scoring weights.
    /// </summary>
    public DefectSettings Defects { get; set; } = new(); 
    /// <summary>
    /// Legacy penalty-scored search: search grid sizes and penalty weights.
    /// </summary>
    public OptimizationSettings Optimization { get; set; } = new(); 
    /// <summary>
    /// Which strategy chooses the spindle position. Production: CompareInscribedGeometry.
    /// </summary>
    public SpindleOptimizationMode OptimizationMode { get; set; } = SpindleOptimizationMode.CompareInscribedGeometry;
    /// <summary>
    /// Largest-inscribed cylinder/oval search settings (used by the production mode).
    /// </summary>
    public InscribedGeometrySettings InscribedGeometry { get; set; } = new();
    /// <summary>
    /// Legacy penalty-scored search: good/bad limits and weights of each score part.
    /// </summary>
    public ScoringSettings Scoring { get; set; } = new(); 

    /// <summary>
    /// Legacy penalty-scored search: how the weights change for swept, oval, low-wood or clean logs.
    /// </summary>
    public AdaptiveScoringSettings AdaptiveScoring { get; set; } = new(); 
    /// <summary>
    /// Largest allowed head/tail spindle moves (hard travel limits) and the warning level.
    /// </summary>
    public MachineLimitSettings MachineLimits { get; set; } = new(); 
    /// <summary>
    /// Operating mode, which readiness checks are enforced, and the commissioning sign-off.
    /// </summary>
    public ProductionSettings Production { get; set; } = new(); 
    /// <summary>
    /// PLC handshake, write order, readback and timeout settings.
    /// </summary>
    public PlcIntegrationSettings Plc { get; set; } = new(); 
    /// <summary>
    /// Knife clearance / carriage retract rules and other safety switches.
    /// </summary>
    public SafetySettings Safety { get; set; } = new(); 
    /// <summary>
    /// RecoveryOptimized mode: which strategies to compare and the direct-search size.
    /// </summary>
    public RecoveryOptimizationSettings RecoveryOptimization { get; set; } = new(); 
    /// <summary>
    /// What extra sections go into the saved diagnostics report.
    /// </summary>
    public DiagnosticsSettings Diagnostics { get; set; } = new();

    /// <summary>
    /// Scan folder details (date, block and scan numbers). Not read anywhere at
    /// run time - kept so existing settings files still load. The SQL save parses
    /// the saved folder name into its own ScanFolderInfo instead.
    /// </summary>
    public ScanFolderInfo Scan { get; set; } = new();


    /// <summary>
    /// Optional SQL Server (Express) saving of scan results.
    /// </summary>
    public DatabaseSettings Database { get; set; } = new();

    /// <summary>
    /// Progress flags written by the production setup wizard (database, cameras, laser, length and scanner calibration).
    /// </summary>
    public ProductionSetupSettings Setup { get; set; } = new();

    /// <summary>
    /// How long saved scan folders are kept.
    /// </summary>
    public ScanStorageSettings Storage { get; set; } = new();

    /// <summary>
    /// Kinematic position-selection prompt (off by default).
    /// </summary>
    public KinematicSettings Kinematic { get; set; } = new();
}

/// <summary>
/// Represents the unit settings for the application, including internal math units and display preferences.
/// </summary>
public sealed class UnitSettings
{
    /// <summary>
    /// Records that internal calculations are in millimetres. Leave true - the maths is written in millimetres.
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
/// Settings for one rotating scan: frame validity, revolution detection (which
/// frames make up exactly one turn and what angle each has), calibration
/// extrapolation, and laser-stripe detection options.
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
    /// Nominal angle step in degrees (10 = 36 frames per turn). Used by readiness
    /// checks and the legacy fixed-step mode; with UseMeasuredRevolutionAngles the
    /// real step is measured from each scan.
    /// </summary>
    public double ExpectedAngleStepDeg { get; set; } = 10.0;  

    /// <summary>
    /// Minimum number of laser profile points a frame needs to count as valid.
    /// </summary>
    public int MinimumProfilePointsPerFrame { get; set; } = 50;

    /// <summary>
    /// DISPLAY point cloud only: largest gap along the log (image pixels) that may be
    /// filled by straight-line interpolation. Never used for measurement.
    /// </summary>
    public int DisplayFillMaxLinearGapPixels { get; set; } = 20;

    /// <summary>
    /// DISPLAY point cloud only: how many frames either side may be used to fill a
    /// missing value. Never used for measurement.
    /// </summary>
    public int DisplayFillMaxAngularNeighborFrames { get; set; } = 2;

    /// <summary>
    /// Minimum valid end-diameter fraction (0-1). In each frame's profile, the
    /// columns at either end whose diameter is below this fraction of that
    /// profile's largest diameter are trimmed off (fixture, glare or a collapsed
    /// laser line rather than log body).
    /// </summary>

    public double MinValidEndDiameterFraction { get; set; } = 0.18;

    /// <summary>
    /// When true (default), frame angles are taken from the scan itself: the frames between
    /// rotation start and the return to the starting profile are divided evenly over 360 degrees,
    /// and frames of the resting log are excluded. When false, the legacy fixed step
    /// (frame index x 360 / TargetFramePairsPerLog) is used.
    /// </summary>
    public bool UseMeasuredRevolutionAngles { get; set; } = true;

    /// <summary>
    /// Frames per camera still accepted after the PLC reports ScanComplete. The block finishes
    /// its turn right around that signal, so the frame that closes the revolution can arrive just
    /// after it. Anything beyond this allowance is the stationary block and is not captured.
    /// </summary>
    public int FramesAcceptedAfterScanComplete { get; set; } = 8;

    /// <summary>
    /// How many laser pixels past the LARGEST measured calibration reference a radius may be
    /// extrapolated, by continuing the straight line through the two nearest references. Lets
    /// blocks slightly larger than the biggest reference be scanned without altering the
    /// calibration package. 0 disables it (frames beyond the calibrated range are rejected).
    /// For larger blocks, add a larger measured reference instead of raising this.
    /// </summary>
    public double MaximumCalibrationExtrapolationPixels { get; set; } = 15.0;

    /// <summary>
    /// Two frames whose radius profiles differ by no more than this RMS value (mm) are treated
    /// as the same resting log position.
    /// </summary>
    public double StationaryProfileChangeMM { get; set; } = 0.8;

    /// <summary>
    /// Maximum RMS difference (mm) between the starting profile and a later profile for that later
    /// frame to be accepted as the completion of one full revolution.
    /// </summary>
    public double RevolutionClosureToleranceMM { get; set; } = 1.5;

    /// <summary>
    /// When no frame returns to the start profile within RevolutionClosureToleranceMM,
    /// the frame that comes closest is still taken as the end of the revolution if
    /// it is within this many mm RMS (and clearly closer than the rest of the turn).
    /// The revolution is then reported as approximate, not verified. Without it,
    /// the frames up to the final rest are spread over 360 degrees, and the rest is
    /// detected while the block is still slowing down (80 frames instead of 86-87
    /// on the saved scans that missed closure by 0.1-0.7 mm).
    /// Default 2.5 (checked on 22 saved scans: the 4 near-miss scans went from
    /// 80-82 to 86-87 frames, Block 8's axis offset from 8.9 to 2.0 mm, +2 usable
    /// sheets, no other scan changed). 0 = off (the behaviour before this setting).
    /// </summary>
    public double ApproximateClosureToleranceMM { get; set; } = 2.5;

    /// <summary>
    /// A revolution is never accepted with fewer frames than this.
    /// </summary>
    public int MinimumFramesPerRevolution { get; set; } = 20;

    /// <summary>
    /// Set true if the log turns the opposite way to the mathematical positive direction of the
    /// machine X/Y frame. Verify with an off-centre reference before changing.
    /// </summary>
    public bool ReverseRotationDirection { get; set; } = false;

    /// <summary>
    /// Machine-frame angle (degrees) at which the laser line strikes the log. 0 means the laser
    /// measures along machine +X. Verify with an off-centre reference before changing.
    /// </summary>
    public double LaserAngleOffsetDeg { get; set; } = 0.0;

    /// <summary>
    /// Allows gap filling in the DISPLAY point cloud. NOTE: the setter is
    /// internal, so this value is not read from appsettings.json and stays false;
    /// in practice the display cloud is never filled. Measurement never uses filling.
    /// </summary>
    public bool FillMissingPoints { get; internal set; }

    /// <summary>
    /// Off (default): only the longest unbroken piece of the laser line in each
    /// frame is used, as before. On: other pieces that line up with it are kept
    /// too, so a knot, check or bark gap that breaks the line no longer throws
    /// away the rest of the line in that frame. Compare with --replay-batch first.
    /// </summary>
    public bool KeepAllStripeSegments { get; set; }

    /// <summary>
    /// Off (default): the laser line centre is the middle of the thresholded
    /// stripe (half-pixel steps), as before. On: the brightness-weighted centre
    /// of the stripe in the grey image is used (about a tenth of a pixel).
    /// Compare with --replay-batch first.
    /// </summary>
    public bool SubPixelStripeCenter { get; set; }

    /// <summary>
    /// Off (default): a revolution is a whole number of frames, from the start
    /// frame to the frame that best matches it again.
    /// On: the 360 degree point is placed between the closing frame and its
    /// neighbour where the profile matches best (for example 84.6 frames), so
    /// the angle step is not rounded to a whole frame. Compare with --replay-batch first.
    /// </summary>
    public bool SubFrameRevolutionClosure { get; set; }
}


/// <summary>
/// Represents the settings for geometry analysis, including local centerline slice thickness, minimum points per slice, radius field bin sizes, and minimum points per radius bin.
/// </summary>
public sealed class GeometrySettings
{

    /// <summary>
    /// Thickness (mm) of each slice when finding local centres along the log.
    /// </summary>
    public double LocalCenterlineSliceThicknessMM { get; set; } = 10.0;


    /// <summary>
    /// Minimum points a slice needs before its centre is fitted.
    /// </summary>
    public int MinPointsPerLocalCenterlineSlice { get; set; } = 8;  

    /// <summary>
    /// Length (mm, along the log) of one radius-field cell.
    /// </summary>
    public double RadiusFieldZBinSizeMM { get; set; } = 10.0;  

    /// <summary>
    /// Width (degrees, around the log) of one radius-field cell.
    /// </summary>
    public double RadiusFieldAngleBinSizeDeg { get; set; } = 10.0;  

    /// <summary>
    /// Minimum readings a cell needs; cells with fewer are left empty.
    /// </summary>
    public int MinPointsPerRadiusBin { get; set; } = 1;  


    /// <summary>
    /// 0 (default): the spindle search treats each 10 mm x 10 deg cell as being
    /// as small as its single lowest reading, as before. 1 or 2: ignore that many
    /// of the lowest readings in a cell (only when the cell has at least
    /// 2 x N + 3 readings), so one noisy low reading cannot decide the position.
    /// Affects only the spindle search field. Compare with --replay-batch first.
    /// </summary>
    public int OptimizationCellIgnoreLowestReadings { get; set; }
}

/// <summary>
/// Represents the settings for sweep analysis, including end trim fraction and thresholds for good, warning, and bad sweep measurements.
/// </summary>
public sealed class SweepSettings
{

    /// <summary>
    /// Fraction (0-1) of the log length trimmed from each end before the sweep/
    /// centreline calculation in the pipeline, so ragged block ends do not skew it.
    /// </summary>
    public double EndTrimFraction { get; set; } = 0.03;

    /// <summary>
    /// Minimum number of slices averaged at each end for the end-centre positions.
    /// </summary>
    public int MinimumEndSlices { get; set; } = 3;

    /// <summary>
    /// Fraction (0-1) of all SLICES averaged at each end for the end-centre
    /// positions (at least MinimumEndSlices).
    /// </summary>
    public double EndSliceFraction { get; set; } = 0.05; 

}


/// <summary>
/// Represents the settings for wane analysis, including thresholds for good, warning, and bad wane measurements.
/// </summary>
public sealed class WaneSettings
{
    /// <summary>
    /// A virtual-peel cell counts as wane when the wood is missing by more than this many mm.
    /// </summary>
    public double WaneToleranceMM { get; set; } = 1.0;

    /// <summary>
    /// A virtual-peel cell counts as SEVERE wane when the wood is missing by more than this many mm.
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
    /// Diameter (inches) of the core left on the lathe; the virtual peel stops here.
    /// </summary>
    public double CoreDiameterInches { get; set; } = 4.0; 

    /// <summary>
    /// Veneer thickness (mm). Also sets the knife clearance (Safety.KnifeClearancePeelThicknessMultiplier x this).
    /// </summary>
    public double VeneerThicknessMM { get; set; } = 3.2;  

    /// <summary>
    /// Peel-picture colouring: cells within this many mm of missing wood are drawn as "thin cleanup".
    /// </summary>
    public double ThinCleanupWarningMM { get; set; } = 1.0;  
}


/// <summary>
/// Legacy PenaltyScored search only. Each score part is scaled from 0 at its
/// "Good" limit to full penalty at its "Bad" limit, then weighted by its
/// WeightPercent. Not used to choose the position in the inscribed modes.
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
/// Legacy PenaltyScored search only: thresholds that classify the log (swept,
/// low-wood, oval or clean) and the weight multipliers used for each class
/// (see AdaptiveScoringProfileBuilder).
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
    /// Multiplier applied to the low radius threshold for relaxed scoring (makes the low-radius test less sensitive).
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
/// Radius-defect classification thresholds and legacy scoring weights.
/// Thresholds are millimetres relative to the MEDIAN radius of the same slice
/// (negative = below it) and are combined with fixed percentage limits in
/// RadiusDefectMapBuilder.
/// </summary>
public sealed class DefectSettings
{
    /// <summary>
    /// A cell at least this far below its slice median (e.g. -2 mm) can be LowRadius.
    /// </summary>
    public double LowRadiusThresholdMM { get; set; } = -2.0; 
    /// <summary>
    /// A cell at least this far below its slice median (e.g. -4 mm) can be SevereLowRadius.
    /// </summary>
    public double SevereLowRadiusThresholdMM { get; set; } = -4.0;  
    /// <summary>
    /// A cell at least this far above its slice median (e.g. +2 mm) can be HighRadius.
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
/// Legacy PenaltyScored search grid and penalty settings (ChargerOptimizer).
/// The search tries offsets on a coarse grid, then a medium grid around the
/// best coarse point, then a fine grid around the best medium point.
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
    /// <summary>Grid step (mm) of the medium search.</summary>
    public double MediumStepMM { get; set; } = 2.5; 

    /// <summary>Half-width (mm) of the fine search around the best medium point.</summary>
    public double FineSearchRadiusMM { get; set; } = 2.5;  
    /// <summary>Grid step (mm) of the fine search.</summary>
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

    /// <summary>Step between tested minor/major axis ratios in the oval search (e.g. 0.55, 0.60, ...).</summary>
    public double AxisRatioStep { get; set; } = 0.05;
    /// <summary>Step (degrees) between tested oval orientations.</summary>
    public double OrientationStepDeg { get; set; } = 10.0;

    /// <summary>
    /// When two candidates have the same geometry within this tolerance,
    /// prefer the one requiring less spindle movement.
    /// </summary>
    public double GeometryTieToleranceMM { get; set; } = 0.01;

    /// <summary>
    /// Repeatability control. The search is centred on the fitted centreline of the block, and a
    /// candidate axis must gain this many millimetres of inscribed radius for every millimetre it
    /// sits away from that centreline. 0 restores the unanchored search (largest cylinder at any
    /// position, search centred on the current spindle axis). Higher values give more repeatable
    /// positions and slightly less diameter; 1.0 or more effectively selects the centreline itself.
    /// </summary>
    public double CenterlinePullWeight { get; set; } = 0.5;

    /// <summary>
    /// Off (default): coarse-medium-fine grid search, as before. On: after the
    /// grid search, the largest-inscribed-cylinder search is finished with a
    /// branch-and-bound search over the whole machine travel that proves how far
    /// from the best possible score the answer can be (reported as the
    /// "certified gap"). Compare with --replay-batch first.
    /// </summary>
    public bool UseCertifiedCylinderSearch { get; set; }

    /// <summary>The certified search stops once the answer is proven within this many mm of the best possible score.</summary>
    public double CertifiedSearchToleranceMM { get; set; } = 0.25;

    /// <summary>Upper limit on positions the certified search may test (bounds its time).</summary>
    public int CertifiedSearchMaxEvaluations { get; set; } = 150000;

    /// <summary>
    /// When the cylinder and oval give the same usable sheets, usable veneer
    /// length and rejected sheets, the oval is only chosen if it cuts severe or
    /// total wane by at least this many percent; otherwise the cylinder is kept.
    /// Stops 0.1-0.5% wane differences from flipping the choice (and moving the
    /// axis) between repeat scans of the same block. It never costs a sheet: it
    /// only acts when the sheets are equal. Default 1 (checked on 22 saved scans:
    /// identical sheets, oval/cylinder choice stable). 0 = the oval wins on any
    /// wane difference, however small (the behaviour before this setting).
    /// </summary>
    public double OvalMinimumWaneAdvantagePercent { get; set; } = 1.0;
}

/// <summary>
/// The program's operating mode. Only <see cref="Production"/> allows PLC
/// machine commands ("Operating mode" is an essential readiness check).
/// </summary>
public enum ChargerOperatingMode
{
    /// <summary>Development/bench use. No PLC commands.</summary>
    Engineering = 0,
    /// <summary>Setting up the machine. No PLC commands. (First-run default.)</summary>
    Commissioning = 1,
    /// <summary>Normal production: PLC commands allowed when the essential checks pass.</summary>
    Production = 2
}


/// <summary>
/// Production readiness settings: operating mode, which checks are enforced,
/// commissioning sign-off and data-quality minimums (see ProductionReadinessEvaluator).
/// </summary>
public sealed class ProductionSettings
{
    /// <summary>Engineering, Commissioning or Production. Only Production lets the PLC write happen.</summary>
    public ChargerOperatingMode OperatingMode { get; set; } = ChargerOperatingMode.Commissioning;

    /// <summary>
    /// When true (default), only the essential checks can stop a PLC write: Production mode,
    /// PLC connected, the operator scan-quality gate, a valid payload, a final recommendation,
    /// finite values, hard travel limits,
    /// reserved fields zero, knife clearance and write order. Every other readiness check still
    /// runs and is written to the report and application log, but a failure is reported as
    /// "NOT ENFORCED" instead of blocking. Set false to make every check blocking again.
    /// </summary>
    public bool EnforceOnlyEssentialReadinessChecks { get; set; } = true;

    /// <summary>
    /// Commissioning sign-off items (and who approved them, when). They feed the
    /// "commissioning" readiness check. That check is not in the essential list,
    /// so while EnforceOnlyEssentialReadinessChecks is true (default) an
    /// incomplete sign-off is reported as NOT ENFORCED instead of blocking. With
    /// it false, PLC writes stay blocked until every item is completed.
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

    /// <summary>Readiness check: the scan must have the expected frame count (non-essential).</summary>
    public bool RequireExactFrameCount { get; set; } = true;
    /// <summary>Readiness check: both cameras must be in triggered (Encoder) mode (non-essential).</summary>
    public bool RequireEncoderAcquisition { get; set; } = true;
    /// <summary>Readiness check: most source frames that may be dropped (non-essential).</summary>
    public int MaximumDroppedSourceFrames { get; set; }
    /// <summary>Readiness check: minimum measured point-cloud size (non-essential).</summary>
    public int MinimumPointCloudPoints { get; set; } = 5000;
    /// <summary>Readiness check: minimum number of centreline slices (non-essential).</summary>
    public int MinimumLocalCenterlineSlices { get; set; } = 20;
    /// <summary>Readiness check: minimum number of radius-field cells (non-essential).</summary>
    public int MinimumBinnedRadiusCells { get; set; } = 500;
    /// <summary>
    /// Readiness check: the saved-record checks (scan folder/SQL record) must pass
    /// before the PLC write. When results are sent before saving
    /// (Plc.SendResultsBeforeSave) these checks are deferred and re-checked after
    /// the save.
    /// </summary>
    public bool RequireResultSaveBeforePlcWrite { get; set; } = true;

    /// <summary>True when every commissioning item is ticked and an approver and date are recorded.</summary>
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
/// PLC handshake and write settings (see XyRestrictedPlcService and
/// docs/plc/PLC_INTEGRATION.md).
/// </summary>
public sealed class PlcIntegrationSettings
{
    /// <summary>Connect to the PLC at all. False on the development computer.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Start capturing frames automatically when the PLC raises ScanStart.</summary>
    public bool AutoStartCaptureOnScanStart { get; set; } = true;
    /// <summary>Send the result to the PLC automatically (no operator button press).</summary>
    public bool AutoSendResultsAfterSave { get; set; } = true;

    /// <summary>
    /// When true, the result is written to the PLC as soon as it has been
    /// calculated and checked, and the scan files and SQL record are saved
    /// straight afterwards. The PLC write then no longer waits for the save.
    /// The save still always happens, and the next scan is not processed until
    /// it has finished. Set false to save first and send second.
    ///
    /// Ignored (save first) when Production.EnforceOnlyEssentialReadinessChecks
    /// is false, because the full checks require the saved record before the
    /// PLC write and a failed save must then stop it.
    /// </summary>
    public bool SendResultsBeforeSave { get; set; } = true;
    /// <summary>After writing, read the tags back and require them to match what was written.</summary>
    public bool RequireReadbackVerification { get; set; } = true;
    /// <summary>Require the operator scan-quality gate to pass (not Failed) before sending.</summary>
    public bool RequireGoodQuality { get; set; } = true;
    /// <summary>How long to wait for the readback to match, in milliseconds.</summary>
    public int ReadbackTimeoutMilliseconds { get; set; } = 5000;
    /// <summary>Largest difference allowed between a written and a read-back FLOAT value.</summary>
    public double FloatComparisonTolerance { get; set; } = 0.01;
    /// <summary>Write the carriage-retract array before the head/tail command (required write order).</summary>
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
    /// assert ScanComplete; every head/tail pair up to that signal is processed
    /// (typically 85-100 pairs).
    /// </summary>
    public bool RequireScanCompleteBeforeProcessing { get; set; } = true;

    /// <summary>How long to wait for ScanComplete before giving up on the scan, in milliseconds.</summary>
    public int ScanCompleteTimeoutMilliseconds { get; set; } = 30000;
}

/// <summary>
/// Knife clearance and carriage-retract rules, plus safety switches used by
/// the readiness checks.
/// </summary>
public sealed class SafetySettings
{
    /// <summary>
    /// Clearance from the measured largest log radius used for the carriage
    /// retract command, as a multiple of the veneer thickness. The requested
    /// production rule is ten peel thicknesses; the code never uses less than 10
    /// even if a smaller number is saved (see <see cref="ComputeKnifeClearanceMM"/>).
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

    /// <summary>Turn a large-move warning (MachineLimits.WarningMoveMM) into a blocking readiness check.</summary>
    public bool BlockOnMovementWarning { get; set; } = true;
    /// <summary>Every command value must be a real finite number.</summary>
    public bool RequireFiniteCommands { get; set; } = true;

    /// <summary>
    /// When true, skips the local knife clearance validation check during production readiness
    /// and trusts that the PLC has been properly configured with knife clearance values.
    /// Set this to true in production to allow PLC-based carriage retract control without
    /// blocking on the application-side safety check.
    /// </summary>
    public bool ReadKnifeClearanceFromPlc { get; set; }

    /// <summary>
    /// Computes the knife clearance in millimeters based on the given veneer thickness and the configured safety settings.
    /// Clearance = max(MinimumKnifeClearanceMM, veneer thickness x max(10, multiplier))
    /// + AdditionalCarriageRetractBufferMM. Example: 3.2 mm veneer x 10 = 32 mm.
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
    /// <summary>Which strategies take part in the comparison (each is virtually peeled).</summary>
    public bool IncludePenaltyCandidate { get; set; } = true;
    public bool IncludeCylinderCandidate { get; set; } = true;
    public bool IncludeOvalCandidate { get; set; } = true;
    /// <summary>Also search a small grid around each strategy's position for a better peel.</summary>
    public bool EnableDirectRecoveryRefinement { get; set; } = true;
    /// <summary>On an exact tie, keep the candidate that moves the spindles less.</summary>
    public bool PreferLowerMovementOnExactTie { get; set; } = true;

    /// <summary>
    /// Local XY search around each strategy seed. A 2 mm radius with a 2 mm
    /// step produces at most 81 combinations per seed before deduplication.
    /// </summary>
    public double DirectRefinementRadiusMM { get; set; } = 2.0;
    /// <summary>Grid step (mm) of the direct refinement search.</summary>
    public double DirectRefinementStepMM { get; set; } = 2.0;
    /// <summary>Upper limit on positions the direct search may peel (bounds its time).</summary>
    public int MaximumDirectRecoveryCandidates { get; set; } = 500;

    /// <summary>
    /// Sparse scans are rejected before direct recovery search. These are search
    /// coverage checks in addition to the global production-readiness checks.
    /// </summary>
    public int MinimumRecoverySearchZBins { get; set; } = 20;
    /// <summary>Minimum angle bins with data for the direct search to run.</summary>
    public int MinimumRecoverySearchAngularBins { get; set; } = 30;
}

/// <summary>
/// Represents the settings for machine travel limits, including maximum allowed movements for the tail and head in both X and Y directions.
/// </summary>
public sealed class MachineLimitSettings
{
    /// <summary>
    /// HARD travel limits: the largest move (mm) allowed for each spindle end in
    /// each direction, measured from the current axis. A command outside these is
    /// blocked ("Hard machine travel limits" is an essential readiness check).
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
/// Cell counts for ONE virtual-peel sheet (used internally by the peel
/// simulation to decide whether the sheet is usable).
/// </summary>
public sealed class SheetQualityStats
{
    public int TotalCells { get; set; }
    public int UsableCells { get; set; } 
    public int WaneCells { get; set; } 
    public int SevereWaneCells { get; set; }  
}

/// <summary>
/// Date, block and scan numbers of a saved scan folder. SqlScanSaveService
/// fills one by parsing the Scan_#### folder path; AppSettings.Scan is unused.
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
/// database saving and specifying the connection string. When saving is on,
/// each scan is also written to SQL (SqlScanSaveService) after the folder save.
/// </summary>
public sealed class DatabaseSettings
{
    public bool EnableDatabaseSaving { get; set; }

    public string ConnectionString { get; set; } = string.Empty;
}

/// <summary>
/// Represents the settings for production setup, including parameters for database configuration,
/// camera verification, laser detection, length calibration, and scanner calibration.
/// These flags are set by the production setup wizard; the main form opens the
/// wizard automatically while <see cref="MinimumSetupComplete"/> is false.
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

    /// <summary>True when every required setup step has been completed and a calibration is active.</summary>
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
    public bool EnableKinematicPositionSelection { get; set; }
}
