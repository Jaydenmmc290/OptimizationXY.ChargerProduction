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
        public string CurrentLogFolder { get; set; } = string.Empty;
        public CaptureAuditRecord? CaptureAudit { get; set; }
        public int ProductionRunSequence { get; set; }
        public ProductionReadinessReport? ProductionReadiness { get; set; }
        public PlcCommandPayload? PlcPayload { get; set; }
        public PlcTransmissionRecord? PlcTransmission { get; set; }
        public MachineFeedbackSnapshot? MachineFeedback { get; set; }
        public OperatorScanQualityResult? OperatorScanQuality { get; set; }

        // Rotating scan input
        public List<ScanFrame> ScanFrames { get; set; } = new();

        // Calibration
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

        // Geometry
        public List<Point3D> PointCloud { get; set; } = new();

        // Display only.
        // This can contain estimated / averaged fill points.
        // Do not use this for final measurements or spindle optimization.

        public List<PipelineTimingEntry> TimingEntries { get; set; } = new();
        public List<Point3D> DisplayPointCloud { get; set; } = new();
        public CenterlineResult? Centerline { get; set; }
        public List<LocalCenterlineSlice> LocalCenterlines { get; set; } = new();
        public CenterlineSweepResult? CenterlineSweep { get; set; }

        // Radius field
        public List<RadiusFieldPoint> RadiusField { get; set; } = new();
        public List<BinnedRadiusFieldPoint> BinnedRadiusField { get; set; } = new();

        // Defects
        public RadiusDefectMap? RadiusDefectMap { get; set; }
        public List<RadiusDefectPatch> RadiusDefectPatches { get; set; } = new();
        public DefectPatchAnalysis? DefectPatchAnalysis { get; set; }

        // Optimization
        public OptimizationWeights OptimizationWeights { get; set; } = new();
        public AdaptiveScoringProfile? AdaptiveScoringProfile { get; set; }

        public OptimizedChargerSetup? OptimizedChargerSetup { get; set; }

        public InscribedGeometryResult? LargestInscribedCylinder { get; set; }
        public InscribedGeometryResult? LargestInscribedOval { get; set; }
        public GeometryPeelComparisonResult? GeometryPeelComparison { get; set; }
        public RecoveryOptimizationResult? RecoveryOptimization { get; set; }
        public DirectRecoverySearchResult? DirectRecoverySearch { get; set; }

        // Final model
        public LogGeometryModel Model { get; set; } = new();
        public FinalSpindleRecommendation? FinalSpindleRecommendation { get; set; } // the final recommendation for the spindle position, based on the optimization results what I need to give to the database

        // Visuals

        public Mat? UnwrappedPeelView { get; set; }
        public Mat? CylinderPeelView { get; set; }
        public Mat? OvalPeelView { get; set; }
        public Mat? BaselinePeelView { get; set; }
        public Mat? PenaltyPeelView { get; set; }
        public Mat? DirectRecoveryPeelView { get; set; }
        public Mat? PointCloudOverlay { get; set; }
        public Mat? RadiusDefectHeatmap { get; set; }
        public Mat? RotatingScanTopDownOverlay { get; set; }
        public Mat? CenterlineOffsetOverlay { get; set; }
        public Mat? SpindleAxisOverlay { get; set; }

        public void Clear()
        {

            var keepSettings = Settings;

            DiagnosticsReport = string.Empty;
            CurrentLogFolder = string.Empty;
            LastSavedRunFolder = string.Empty;
            CaptureAudit = null;
            ProductionRunSequence = 0;
            ProductionReadiness = null;
            PlcPayload = null;
            PlcTransmission = null;
            MachineFeedback = null;
            OperatorScanQuality = null;

            PointCloudOverlay?.Dispose();
            RadiusDefectHeatmap?.Dispose();
            RotatingScanTopDownOverlay?.Dispose();
            CenterlineOffsetOverlay?.Dispose();
            SpindleAxisOverlay?.Dispose();
            UnwrappedPeelView?.Dispose();
            CylinderPeelView?.Dispose();
            OvalPeelView?.Dispose();
            BaselinePeelView?.Dispose();
            PenaltyPeelView?.Dispose();
            DirectRecoveryPeelView?.Dispose();

            PointCloudOverlay = null;
            RadiusDefectHeatmap = null;
            RotatingScanTopDownOverlay = null;
            CenterlineOffsetOverlay = null;
            SpindleAxisOverlay = null;
            UnwrappedPeelView = null;
            CylinderPeelView = null;
            OvalPeelView = null;
            BaselinePeelView = null;
            PenaltyPeelView = null;
            DirectRecoveryPeelView = null;
            Calibration = null;
            ScannerCalibrationSnapshot = null;
            HeadCameraConfigurationSnapshot = null;
            TailCameraConfigurationSnapshot = null;
            Centerline = null;
            CenterlineSweep = null;
            RadiusDefectMap = null;
            DefectPatchAnalysis = null;
            AdaptiveScoringProfile = null;
            OptimizedChargerSetup = null;
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
            ScanFrames.Clear();
            PointCloud.Clear();

            LocalCenterlines.Clear();
            DisplayPointCloud.Clear();
            RadiusField.Clear();
            BinnedRadiusField.Clear();
            RadiusDefectPatches.Clear();

            Model = new LogGeometryModel();

            Settings = keepSettings;
        }
    }
}
