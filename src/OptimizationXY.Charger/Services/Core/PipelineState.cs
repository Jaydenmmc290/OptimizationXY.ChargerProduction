using Emgu.CV;
using OptimizationXY.Charger.Models.Analysis;
using OptimizationXY.Charger.Models.Calibration;
using OptimizationXY.Charger.Models.Core;
using OptimizationXY.Charger.Models.Defects;
using OptimizationXY.Charger.Models.Diagnostics;
using OptimizationXY.Charger.Models.Geometry;
using OptimizationXY.Charger.Models.Optimization;
using OptimizationXY.Charger.Models.Optimization.Scoring;
using OptimizationXY.Charger.Models.Optimization.Geometry;
using OptimizationXY.Charger.Models.Optimization.Recovery;
using OptimizationXY.Charger.Models.Optimization.Setup;
using OptimizationXY.Charger.Models.Optimization.Spindle;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Models.Production;
using OptimizationXY.Charger.Models.Plc;
using OptimizationXY.Charger.Models.Operator;

namespace OptimizationXY.Charger.Services.Core
{

    /// <summary>
    /// The ONE shared place where the current scan's data and results live.
    /// </summary>
    /// <remarks>
    /// There is a single instance (<see cref="Instance"/>). The coordinator clears
    /// it at the start of each scan (<see cref="Clear"/>, which keeps the
    /// settings); each pipeline stage then fills in its part in order: frames ->
    /// calibration -> point cloud -> centreline -> radius fields -> defects ->
    /// optimization result -> final recommendation -> PLC payload -> readiness ->
    /// PLC transmission. The save services and the UI read the results from here.
    /// Only one scan is processed at a time, so there is no locking; never start a
    /// new scan while the previous one is still saving (see LogProcessingCoordinator).
    /// </remarks>
    public sealed class PipelineState
    {

        // Singleton instance of the PipelineState class.
        private static readonly Lazy<PipelineState> _instance =
            new(() => new PipelineState());

        // Accessor for the singleton instance of the PipelineState class.
        public static PipelineState Instance => _instance.Value;

        // Private constructor to prevent external instantiation of the PipelineState class.
        private PipelineState()
        {
        }

        public AppSettings Settings { get; set; } = new(); // Application settings for the pipeline, including units, scan settings, geometry settings, defect settings,
                                                           // optimization settings, machine limits, and diagnostics settings.
        public string DiagnosticsReport { get; set; } = string.Empty;  // Diagnostics report generated during the pipeline execution, containing
                                                                       // information about the processing steps, errors, and warnings.

        public string LastSavedRunFolder { get; set; } = string.Empty; // Folder path where the last successful run of the pipeline
                                                                       // was saved, used for reference and reloading previous results.

        /// <summary>
        /// True when this scan is sent to the PLC first and saved straight after.
        /// The readiness checks that look for the saved record then report it as
        /// deferred instead of missing, and are brought up to date after the save.
        /// </summary>
        public bool ResultSaveDeferredUntilAfterPlcWrite { get; set; }

        /// <summary>
        /// True once the save of this scan has been started, so it is never
        /// started a second time.
        /// </summary>
        public bool ResultSaveStarted { get; set; }
        /// <summary>Folder of the scan currently being worked on (replay / folder processing).</summary>
        public string CurrentLogFolder { get; set; } = string.Empty;
        /// <summary>How the frames of this scan were captured (counts, timing, mode).</summary>
        public CaptureAuditRecord? CaptureAudit { get; set; }
        /// <summary>Block number sent to the PLC for this scan (0 until PlcPayloadBuilder assigns it).</summary>
        public int ProductionRunSequence { get; set; }
        /// <summary>Result of the production readiness checks for this scan.</summary>
        public ProductionReadinessReport? ProductionReadiness { get; set; }
        /// <summary>The exact payload (inches) built for the PLC, or null if it could not be built.</summary>
        public PlcCommandPayload? PlcPayload { get; set; }
        /// <summary>What happened when the payload was written to the PLC.</summary>
        public PlcTransmissionRecord? PlcTransmission { get; set; }
        /// <summary>Feedback read back from the PLC after the write.</summary>
        public MachineFeedbackSnapshot? MachineFeedback { get; set; }
        /// <summary>Scan-quality verdict shown to the operator; a Failed verdict blocks the PLC write.</summary>
        public OperatorScanQualityResult? OperatorScanQuality { get; set; }

        /// <summary>
        /// Kinematic visualization state captured during operator position selection.
        /// Contains complete diagnostic information about the rotation logic calculation.
        /// Serialized to saved logs for verification and testing.
        /// </summary>
        public Kinematic.KinematicVisualizationState? KinematicVisualizationState { get; set; }

        // Rotating scan input: one joined (head+tail) frame per pair, in capture order.
        public List<ScanFrame> ScanFrames { get; set; } = new();

        // How rotation angles were assigned to the frames of the current scan.
        public OptimizationXY.Charger.Services.Acquisition.RevolutionAngleResult? RevolutionAngles { get; set; }

        // Axial scale (pixels per mm along the log) used for this scan.
        public CalibrationResult? Calibration { get; set; }

        /// <summary>
        /// Immutable per-run copy of the exact lookup values used by the pipeline.
        /// This is serialized with the saved scan so a later file replacement
        /// cannot change the audit record for the completed calculation.
        /// </summary>
        public ScannerLookupTable? ScannerCalibrationSnapshot { get; set; }

        /// <summary>
        /// Exact camera configuration bytes captured when the live scan began.
        /// These are written to the run folder rather than re-reading mutable
        /// files from disk after processing completes.
        /// </summary>
        public byte[]? HeadCameraConfigurationSnapshot { get; set; }
        public byte[]? TailCameraConfigurationSnapshot { get; set; }
        // (Head first, tail second; both are copied into the saved scan folder.)

        // Geometry. PointCloud = MEASURED points only (X/Y across the log, Z along it,
        // mm, Z = 0 at the tail). This is the only cloud used for measurement,
        // optimization and the PLC command.
        public List<Point3D> PointCloud { get; set; } = new();


        /// <summary>How long each pipeline step took (saved in the timing report).</summary>
        public List<PipelineTimingEntry> TimingEntries { get; set; } = new();

        /// <summary>
        /// Real elapsed time of the last processing run (frames in hand to
        /// position chosen), in seconds. 0 until a run finishes. TimingEntries
        /// cannot be added up for this: frames are processed in parallel.
        /// </summary>
        public double ProcessingElapsedSeconds { get; set; }
        /// <summary>
        /// DISPLAY ONLY. May contain estimated / averaged fill points. Never use this
        /// for measurements or spindle optimization - use <see cref="PointCloud"/>.
        /// </summary>
        public List<Point3D> DisplayPointCloud { get; set; } = new();
        /// <summary>Fitted straight centreline of the log (PCA refined through slice centres).</summary>
        public CenterlineResult? Centerline { get; set; }
        /// <summary>Centre and mean radius of each thin slice along the log.</summary>
        public List<LocalCenterlineSlice> LocalCenterlines { get; set; } = new();
        /// <summary>Sweep/bow numbers (diagnostic).</summary>
        public CenterlineSweepResult? CenterlineSweep { get; set; }

        // Radius field
        // PCA-centerline-relative field used for shape diagnostics such as
        // ovality and surface-defect analysis.
        public List<RadiusFieldPoint> RadiusField { get; set; } = new();
        public List<BinnedRadiusFieldPoint> BinnedRadiusField { get; set; } = new();

        // Current-spindle-relative field used for every calculation that
        // evaluates or selects a machine spindle position. In this frame the
        // existing spindle axis is (0,0), matching PointCloud and peel math.
        public List<RadiusFieldPoint> OptimizationRadiusField { get; set; } = new();
        public List<BinnedRadiusFieldPoint> OptimizationBinnedRadiusField { get; set; } = new();

        // Defects (diagnostic in the inscribed-geometry modes; scored in PenaltyScored mode).
        public RadiusDefectMap? RadiusDefectMap { get; set; }
        public List<RadiusDefectPatch> RadiusDefectPatches { get; set; } = new();
        public DefectPatchAnalysis? DefectPatchAnalysis { get; set; }

        // Optimization results. Only the ones for the active OptimizationMode are filled.

        public AdaptiveScoringProfile? AdaptiveScoringProfile { get; set; }



        // Inscribed-geometry search results (filled in the inscribed modes and in
        // RecoveryOptimized): best round cylinder, best oval, and the peel comparison
        // that picks between them (CompareInscribedGeometry = production mode).
        public InscribedGeometryResult? LargestInscribedCylinder { get; set; }
        public InscribedGeometryResult? LargestInscribedOval { get; set; }
        public GeometryPeelComparisonResult? GeometryPeelComparison { get; set; }
        // RecoveryOptimized mode only: the strategy comparison and the direct search.
        public RecoveryOptimizationResult? RecoveryOptimization { get; set; }
        public DirectRecoverySearchResult? DirectRecoverySearch { get; set; }

        // Final model: log size summary and THE recommendation that becomes the PLC command.
        public LogGeometryModel Model { get; set; } = new();
        public FinalSpindleRecommendation? FinalSpindleRecommendation { get; set; } // the final spindle position recommendation: becomes the PLC command and is saved to the scan folder / database

        // Visuals (pictures saved with the scan and shown on screen). All are OpenCV
        // images that must be disposed - Clear() does that.

        // Peel views (the "unrolled veneer" pictures), drawn after the PLC write:
        //   UnwrappedPeelView       the selected position (always)
        //   CylinderPeelView        best inscribed cylinder (inscribed compare / recovery)
        //   OvalPeelView            best inscribed oval (inscribed compare / recovery)
        //   BaselinePeelView        current axis, no move (recovery mode)
        //   PenaltyPeelView         penalty-scored position (recovery mode)
        //   DirectRecoveryPeelView  direct search position (recovery mode)
        public Mat? UnwrappedPeelView { get; set; }
        public Mat? CylinderPeelView { get; set; }
        public Mat? OvalPeelView { get; set; }
        public Mat? BaselinePeelView { get; set; }
        public Mat? PenaltyPeelView { get; set; }
        public Mat? DirectRecoveryPeelView { get; set; }

        /// <summary>
        /// Peel images still to be drawn. The pipeline chooses the position from
        /// numbers only and queues the images here; they are drawn after the PLC
        /// write, just before the scan is saved (DeferredPeelViewRenderer).
        /// </summary>
        public List<DeferredPeelView> DeferredPeelViews { get; } = new();

        // Despite its name: the tail / middle / head cross-section picture
        // (RotatingRadiusSectionOverlayBuilder).
        public Mat? PointCloudOverlay { get; set; }

        /// <summary>Single view from the head end of the block, shown in the main form overlay box.</summary>
        public Mat? LogHeadEndViewOverlay { get; set; }
        // Colour map of the radius defect map (dents/bumps along and around the log).
        // CenterlineOffsetOverlay and SpindleAxisOverlay are saved when present, but
        // nothing in the current pipeline fills them, so they stay null.
        public Mat? RadiusDefectHeatmap { get; set; }
        public Mat? CenterlineOffsetOverlay { get; set; }
        public Mat? SpindleAxisOverlay { get; set; }

        /// <summary>
        /// Forgets everything about the previous scan (disposing all images) so a new
        /// scan starts clean. The settings object is kept.
        /// </summary>
        public void Clear()
        {

            var keepSettings = Settings;

            DiagnosticsReport = string.Empty;
            CurrentLogFolder = string.Empty;
            LastSavedRunFolder = string.Empty;
            ResultSaveDeferredUntilAfterPlcWrite = false;
            ResultSaveStarted = false;
            CaptureAudit = null;
            ProductionRunSequence = 0;
            ProductionReadiness = null;
            PlcPayload = null;
            PlcTransmission = null;
            MachineFeedback = null;
            OperatorScanQuality = null;
            KinematicVisualizationState = null;

            PointCloudOverlay?.Dispose();
            LogHeadEndViewOverlay?.Dispose();
            RadiusDefectHeatmap?.Dispose();
            CenterlineOffsetOverlay?.Dispose();
            SpindleAxisOverlay?.Dispose();
            UnwrappedPeelView?.Dispose();
            CylinderPeelView?.Dispose();
            OvalPeelView?.Dispose();
            BaselinePeelView?.Dispose();
            PenaltyPeelView?.Dispose();
            DirectRecoveryPeelView?.Dispose();

            PointCloudOverlay = null;
            LogHeadEndViewOverlay = null;
            RadiusDefectHeatmap = null;
            CenterlineOffsetOverlay = null;
            SpindleAxisOverlay = null;
            UnwrappedPeelView = null;
            CylinderPeelView = null;
            OvalPeelView = null;
            BaselinePeelView = null;
            PenaltyPeelView = null;
            DirectRecoveryPeelView = null;
            DeferredPeelViews.Clear();
            Calibration = null;
            ScannerCalibrationSnapshot = null;
            HeadCameraConfigurationSnapshot = null;
            TailCameraConfigurationSnapshot = null;
            Centerline = null;
            CenterlineSweep = null;
            RadiusDefectMap = null;
            DefectPatchAnalysis = null;
            AdaptiveScoringProfile = null;

            LargestInscribedCylinder = null;
            LargestInscribedOval = null;
            GeometryPeelComparison = null;
            RecoveryOptimization = null;
            DirectRecoverySearch = null;
            FinalSpindleRecommendation = null;

            foreach (var frame in ScanFrames)
            {
                frame.GrayImage?.Dispose();
                frame.BinaryImage?.Dispose();
                frame.OverlayImage?.Dispose();
            }

            TimingEntries.Clear();
            ProcessingElapsedSeconds = 0.0;
            ScanFrames.Clear();
            RevolutionAngles = null;
            PointCloud.Clear();

            LocalCenterlines.Clear();
            DisplayPointCloud.Clear();
            RadiusField.Clear();
            BinnedRadiusField.Clear();
            OptimizationRadiusField.Clear();
            OptimizationBinnedRadiusField.Clear();
            RadiusDefectPatches.Clear();

            Model = new LogGeometryModel();

            Settings = keepSettings;
        }
    }
}
