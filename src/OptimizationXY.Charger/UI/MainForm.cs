using System.Diagnostics;
using System.Security.Cryptography;
using Emgu.CV;
using NLog;
using OptimizationXY.Charger.Infrastructure;
using OptimizationXY.Charger.Models.Acquisition;
using OptimizationXY.Charger.Models.Calibration;
using OptimizationXY.Charger.Models.Operator;
using OptimizationXY.Charger.Models.Optimization;
using OptimizationXY.Charger.Models.Production;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Acquisition;
using OptimizationXY.Charger.Services.Calibration;
using OptimizationXY.Charger.Services.Camera;
using OptimizationXY.Charger.Services.Configuration;
using OptimizationXY.Charger.Services.Core;
using OptimizationXY.Charger.Services.Operator;
using OptimizationXY.Charger.Services.Plc;
using OptimizationXY.Charger.UI.Forms;

namespace OptimizationXY.Charger.UI;
/// <summary>
/// The main form of the application that manages camera services, PLC integration, and user interface for the Optimization XY Charger.
/// </summary>

public partial class MainForm : Form
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private HeadCameraForm? _headCameraForm;
    private TailCameraForm? _tailCameraForm;
    private OperatorScanQualityForm? _operatorScanQualityForm;
    private readonly OperatorScanQualityAnalyzer _operatorScanQualityAnalyzer = new();
    private bool _plcConnected;
    private bool _plcScanStart;
    private bool _plcScanComplete;

    // ================================================================
    // CAMERA SERVICES
    // ================================================================
    private GenieNanoCameraService? _headCameraService;
    private GenieNanoCameraService? _tailCameraService;

    // ================================================================
    // CAPTURE AND PROCESSING
    // ================================================================
    private RotatingLogCaptureManager? _captureManager;
    private LogProcessingCoordinator? _processingCoordinator;
    private XyRestrictedPlcService? _plcService;
    private CalibrationFrameCaptureSession? _calibrationCaptureSession;

    // ================================================================
    // ACQUISITION DIAGNOSTICS
    // ================================================================
    private DiagnosticsCollector? _diagnostics;
    private System.Windows.Forms.Timer? _diagnosticsTimer;

    // Counts every managed camera frame, including idle live-preview frames.
    private long _headLiveFramesReceived;
    private long _tailLiveFramesReceived;
    private long _lastHeadFrameNumber;
    private long _lastTailFrameNumber;
    private long _headLiveFrameGaps;
    private long _tailLiveFrameGaps;

    // ================================================================
    // SETTINGS
    // ================================================================
    private readonly CameraAcquisitionSettings _cameraSettings;

    // ================================================================
    // APPLICATION STATE
    // ================================================================
    private ApplicationState _currentState = ApplicationState.Idle;
    private CancellationTokenSource? _processingCancellation;

    // ================================================================
    // RESULTS AND DISPOSAL
    // ================================================================
    // Spindle result reference for visualization
    private Models.Optimization.Spindle.FinalSpindleRecommendation? _latestResult;

    // Disposal flag
    private bool _isDisposing;
    private int _captureFinishRequested;
    private bool _plcStartInProgress;
    private bool _setupWizardOpen;

    // ================================================================
    // LIVE PREVIEW STATE
    // ================================================================
    // Latest managed frames are used only for live display.
    // Access is protected because camera callbacks can arrive on different threads.
    private readonly Lock _livePreviewLock = new();
    private Mat? _latestHeadPreview;
    private Mat? _latestTailPreview;
    private int _headPreviewPostPending;
    private int _tailPreviewPostPending;
    private bool _combinedPreviewUpdatePosted;

    // Limit UI preview work without limiting production capture.
    // Camera/capture frames can still arrive at full encoder or frame rate.
    private static readonly long PreviewIntervalTicks =
        Stopwatch.Frequency / 10; // 10 preview updates per second

    private long _lastHeadPreviewTimestamp;
    private long _lastTailPreviewTimestamp;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainForm"/> class.
    /// </summary>
    public MainForm()
    {
        InitializeComponent();
        _cameraSettings = PipelineState.Instance.Settings.Acquisition;
        InitializeOptimizationModeSelector();

    }


    /// <summary>
    /// Initializes the optimization mode selector combo box with available modes and sets up event handling for selection changes.
    /// </summary>
    private void InitializeOptimizationModeSelector()
    {
        cmbOptimizationMode.DataSource =
            Enum.GetValues<SpindleOptimizationMode>();

        cmbOptimizationMode.Format += (_, args) =>
        {
            if (args.ListItem is SpindleOptimizationMode mode)
                args.Value = GetOptimizationModeDisplayName(mode);
        };

        cmbOptimizationMode.SelectedItem =
            PipelineState.Instance.Settings.OptimizationMode;

        cmbOptimizationMode.SelectedIndexChanged +=
            OptimizationMode_SelectedIndexChanged;
    }


    /// <summary>
    /// Returns a user-friendly display name for the given optimization mode.
    /// </summary>
    /// <param name="mode">The optimization mode.</param>
    /// <returns>A user-friendly display name for the optimization mode.</returns>
    private static string GetOptimizationModeDisplayName(
        SpindleOptimizationMode mode)
    {
        return mode switch
        {
            SpindleOptimizationMode.PenaltyScored =>
                "Penalty-Scored Optimizer",
            SpindleOptimizationMode.LargestInscribedCylinder =>
                "Largest Inscribed Cylinder",
            SpindleOptimizationMode.LargestInscribedOval =>
                "Largest Inscribed Oval",
            SpindleOptimizationMode.CompareInscribedGeometry =>
                "Compare Cylinder vs Oval",
            SpindleOptimizationMode.RecoveryOptimized =>
                "Recovery Optimized (All Strategies)",
            _ => mode.ToString()
        };
    }

    /// <summary>
    /// Handles the event when the selected optimization mode in the combo box changes. Updates the application settings and saves them to persistent storage.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data.</param>
    private void OptimizationMode_SelectedIndexChanged(
        object? sender,
        EventArgs e)
    {
        if (cmbOptimizationMode.SelectedItem is not
            SpindleOptimizationMode selectedMode)
        {
            return;
        }

        AppSettings settings =
            PipelineState.Instance.Settings;

        settings.OptimizationMode = selectedMode;
        AppSettingsStore.Save(settings);

        AppendDiagnostics(
            $"Optimization mode selected: {selectedMode}");
    }

    /// <summary>
    /// Determines whether the preview for the specified camera position should be updated based on the elapsed time since the last update.
    /// This throttles the preview updates to avoid overwhelming the UI with too many updates per second.
    /// </summary>
    /// <param name="position">The camera position for which to determine if the preview should be updated.</param>
    /// <returns>True if the preview should be updated; otherwise, false.</returns>
    private bool ShouldUpdatePreview(CameraPosition position)
    {
        long now = Stopwatch.GetTimestamp();

        ref long previous =
            ref position == CameraPosition.Head
                ? ref _lastHeadPreviewTimestamp
                : ref _lastTailPreviewTimestamp;

        while (true)
        {
            long last = Volatile.Read(ref previous);

            if (now - last < PreviewIntervalTicks)
                return false;

            if (Interlocked.CompareExchange(
                    ref previous,
                    now,
                    last) == last)
            {
                return true;
            }
        }
    }
    // ================================================================
    // FORM LIFECYCLE
    // ================================================================

    /// <summary>
    /// Initializes the operator view form based on the application settings. If the operator view is enabled, it
    /// creates and configures the OperatorScanQualityForm,
    /// sets its position and size according to the specified display index, and shows it if auto-show is enabled.
    /// </summary>
    private void InitializeOperatorView()
    {
        OperatorViewSettings settings = PipelineState.Instance.Settings.OperatorView;
        if (!settings.Enabled)
            return;

        _operatorScanQualityForm?.CloseForApplicationExit();
        _operatorScanQualityForm?.Dispose();
        _operatorScanQualityForm = new OperatorScanQualityForm
        {
            TopMost = settings.TopMost
        };

        Screen[] screens = Screen.AllScreens;
        int displayIndex = settings.DisplayIndex >= 0 && settings.DisplayIndex < screens.Length
            ? settings.DisplayIndex
            : Array.FindIndex(screens, screen => screen.Primary);
        if (displayIndex < 0)
            displayIndex = 0;

        Rectangle bounds = screens[displayIndex].WorkingArea;
        _operatorScanQualityForm.StartPosition = FormStartPosition.Manual;
        _operatorScanQualityForm.Bounds = bounds;
        if (settings.StartMaximized)
            _operatorScanQualityForm.WindowState = FormWindowState.Maximized;

        _operatorScanQualityForm.ShowWaiting(GetOperatorRuntimeStatus());
        if (settings.AutoShow)
            _operatorScanQualityForm.Show();
    }

    /// <summary>
    /// Gets the current operator runtime status, including PLC connection status, scan start and complete flags, captured frame pairs, and expected frame pairs.
    /// </summary>
    /// <returns>The current operator runtime status.</returns>
    private OperatorRuntimeStatus GetOperatorRuntimeStatus()
    {
        return new OperatorRuntimeStatus
        {
            PlcEnabled = PipelineState.Instance.Settings.Plc.Enabled,
            PlcConnected = _plcConnected || _plcService?.IsConnected == true,
            ScanStart = _plcScanStart,
            ScanComplete = _plcScanComplete,
            CapturedPairs = _captureManager?.CapturedFrameCount ?? 0,
            ExpectedPairs = PipelineState.Instance.Settings.Acquisition.TargetFramePairsPerLog
        };
    }

    /// <summary>
    /// Updates the operator runtime status on the OperatorScanQualityForm.
    /// </summary>
    private void UpdateOperatorRuntime()
    {
        _operatorScanQualityForm?.UpdateRuntime(GetOperatorRuntimeStatus());
    }

    /// <summary>
    /// Handles the Load event of the MainForm. Initializes camera services, operator view, PLC integration, and starts the diagnostics timer. If any initialization error occurs, it logs the error and
    /// sets the application state to Faulted. If the production setup wizard should be opened, it invokes the method to open it.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private async void MainForm_Load(object sender, EventArgs e)
    {
        try
        {
            SetUIState(ApplicationState.Idle);

            AppendDiagnostics("Optimization XY Charger operator form loaded.");
            AppendDiagnostics($"Runtime data root: {ApplicationPaths.RootDirectory}");
            AppendDiagnostics("Initializing camera services...");

            //InitializeCameraPreviewForms();
            InitializeOperatorView();
            InitializeCameraServices();
            await InitializePlcIntegrationAsync();
            StartDiagnosticsTimer();
        }
        catch (Exception ex)
        {
            AppendDiagnostics($"Initialization error: {ex.Message}");
            AppendDiagnostics("Production Setup remains available so camera, SQL, and calibration settings can be corrected.");
            SetUIState(ApplicationState.Faulted);
        }
        finally
        {
            if (ShouldOpenSetupWizard())
            {
                BeginInvoke(new Action(OpenProductionSetupWizard));
            }
        }
    }

    /// <summary>
    /// Handles the FormClosing event of the MainForm. Cancels any ongoing processing, disposes of camera services,
    /// PLC integration, and other resources, and ensures that the base class's OnFormClosing method is called.
    /// </summary>
    /// <param name="e">The <see cref="FormClosingEventArgs"/> instance containing the event data.</param>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_isDisposing)
        {
            base.OnFormClosing(e);
            return;
        }

        _isDisposing = true;

        try
        {
            _processingCancellation?.Cancel();
            _processingCancellation?.Dispose();
            _processingCancellation = null;

            _calibrationCaptureSession?.Cancel(
                "Application is closing.");
            _calibrationCaptureSession?.Dispose();
            _calibrationCaptureSession = null;

            if (_captureManager?.IsCapturingActive == true)
                _captureManager.CancelCapture();

            StopDiagnosticsTimer();
            DisposeCameraServices();
            DisposePlcIntegration();
            _captureManager?.DiagnosticsMessage -=
                    CaptureManager_DiagnosticsMessage;
            _captureManager = null;

            _processingCoordinator?.DiagnosticsMessage -=
                    ProcessingCoordinator_DiagnosticsMessage;
            _processingCoordinator = null;

            DisposePreviewForm(ref _headCameraForm);
            DisposePreviewForm(ref _tailCameraForm);
            _operatorScanQualityForm?.CloseForApplicationExit();
            _operatorScanQualityForm?.Dispose();
            _operatorScanQualityForm = null;

            lock (_livePreviewLock)
            {
                _latestHeadPreview?.Dispose();
                _latestTailPreview?.Dispose();
                _latestHeadPreview = null;
                _latestTailPreview = null;
                _combinedPreviewUpdatePosted = false;
            }

            Image? oldCombinedImage = pictureBoxConcatenated.Image;
            pictureBoxConcatenated.Image = null;
            oldCombinedImage?.Dispose();
        }
        finally
        {
            base.OnFormClosing(e);
        }
    }

    // ================================================================
    // CAMERA INITIALIZATION
    // ================================================================

    /// <summary>
    /// Initializes the camera services for both head and tail cameras based on the configured settings. Validates the camera settings,
    /// creates instances of GenieNanoCameraService for each camera, connects to the cameras, starts live preview and managed frame capture,
    /// and sets the application state to CameraReady. If any error occurs during initialization, it logs the error, disposes of the camera
    /// services, shows a failure message on the operator scan quality form, and sets the application state to Faulted.
    /// </summary>
    private void InitializeCameraServices()
    {
        DisposeCameraServices();

        _cameraSettings.Validate();

        // One shared collector is used by every production acquisition
        // component so the UI reads one consistent set of counters.
        _diagnostics = new DiagnosticsCollector();

        _captureManager =
            new RotatingLogCaptureManager(_cameraSettings, _diagnostics);

        _captureManager.DiagnosticsMessage +=
            CaptureManager_DiagnosticsMessage;

        _processingCoordinator =
            new LogProcessingCoordinator();

        _processingCoordinator.DiagnosticsMessage +=
            ProcessingCoordinator_DiagnosticsMessage;

        try
        {
            CameraDeviceSettings headSettings =
                _cameraSettings.HeadCamera;

            CameraDeviceSettings tailSettings =
                _cameraSettings.TailCamera;

            AppendDiagnostics($"Head camera: {headSettings.GetInitializationDescription()}");
            AppendDiagnostics($"Tail camera: {tailSettings.GetInitializationDescription()}");

            ResolveAssignedCameras();

            _headCameraService = new GenieNanoCameraService();
            _tailCameraService = new GenieNanoCameraService();

            _headCameraService.DiagnosticsMessage += HeadCameraService_DiagnosticsMessage;
            _tailCameraService.DiagnosticsMessage += TailCameraService_DiagnosticsMessage;
            _headCameraService.FrameAcquired += HeadCameraService_FrameAcquired;
            _tailCameraService.FrameAcquired += TailCameraService_FrameAcquired;

            if (_headCameraForm == null || _headCameraForm.IsDisposed)
            {
                _headCameraForm = new HeadCameraForm();
                _headCameraForm.EnsureCameraViewReady();
            }

            if (_tailCameraForm == null || _tailCameraForm.IsDisposed)
            {
                _tailCameraForm = new TailCameraForm();
                _tailCameraForm.EnsureCameraViewReady();
            }
            _headCameraForm.EnsureCameraViewReady();
            _tailCameraForm.EnsureCameraViewReady();

            _headCameraService.Connect(
                headSettings,
                _headCameraForm.CameraViewHost,
                new ArgumentOutOfRangeException(
                    nameof(headSettings.Mode),
                    headSettings.Mode,
                    "Unsupported camera acquisition mode."),
                new ArgumentOutOfRangeException(
                    nameof(headSettings.Mode),
                    headSettings.Mode,
                    "Unsupported camera acquisition mode."));

            AppendDiagnostics($"Head camera connected: {headSettings.ServerName} [{headSettings.DeviceIndex}]");

            _tailCameraService.Connect(tailSettings, _tailCameraForm.CameraViewHost, new ArgumentOutOfRangeException(
                    nameof(tailSettings.Mode),
                    tailSettings.Mode,
                    "Unsupported camera acquisition mode."),
                new ArgumentOutOfRangeException(
                    nameof(tailSettings.Mode),
                    tailSettings.Mode,
                    "Unsupported camera acquisition mode."));


            AppendDiagnostics($"Tail camera connected: {tailSettings.ServerName} [{tailSettings.DeviceIndex}]");

            


            _headCameraService.StartLive();
            _tailCameraService.StartLive();

            // Managed callbacks remain active for live preview and diagnostics.
            // The preview path is throttled separately so full camera rate does
            // not overload the UI or create excessive temporary images.
            _headCameraService.StartManagedFrameCapture();
            _tailCameraService.StartManagedFrameCapture();

            AppendDiagnostics("Both cameras started successfully.");

            SetUIState(ApplicationState.CameraReady);
        }
        catch (Exception ex)
        {
            AppendDiagnostics(
                $"Dual-camera initialization failed: {ex.Message}");

            DisposeCameraServices();
            _operatorScanQualityForm?.ShowFailure(
                "Camera initialization failed. Production scanning is unavailable until both cameras reconnect.",
                GetOperatorRuntimeStatus());
            SetUIState(ApplicationState.Faulted);
        }
    }

    /// <summary>
    /// Initializes the PLC integration by checking the settings, creating an instance of XyRestrictedPlcService, subscribing
    /// to its events, and starting the service.
    /// If PLC integration is disabled in the settings, it logs a message and does not initialize the service.
    /// </summary>
    private async Task InitializePlcIntegrationAsync()
    {
        DisposePlcIntegration();

        var plcSettings = PipelineState.Instance.Settings.Plc;

        if (!plcSettings.Enabled)
        {
            AppendDiagnostics("[PLC] Integration disabled in settings.");
            return;
        }

        _plcService = new XyRestrictedPlcService(plcSettings);
        _plcService.DiagnosticsMessage += PlcService_DiagnosticsMessage;
        _plcService.ScanStartRequested += PlcService_ScanStartRequested;
        _plcService.ScanStartChanged += PlcService_ScanStartChanged;
        _plcService.ConnectionStateChanged += PlcService_ConnectionStateChanged;
        _plcService.ScanCompleteChanged += PlcService_ScanCompleteChanged;
        await _plcService.StartAsync();
    }

    /// <summary>
    /// Handles diagnostics messages from the PLC service and appends them to the diagnostics log in a thread-safe manner.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="message">The diagnostics message.</param>
    private void PlcService_DiagnosticsMessage(object? sender, string message)
    {
        AppendDiagnosticsThreadSafe($"[PLC] {message}");
    }

    /// <summary>
    /// Handles the connection state change event from the PLC service. Updates the internal connection state,
    /// appends a diagnostics message indicating the new connection status, and updates the operator runtime status accordingly.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="connected">The new connection state.</param>
    private void PlcService_ConnectionStateChanged(object? sender, bool connected)
    {
        _plcConnected = connected;
        AppendDiagnosticsThreadSafe(
            connected
                ? "[PLC] Connected and available for verified writes."
                : "[PLC] Disconnected. Production writes are blocked.");
        UpdateOperatorRuntime();
    }

    /// <summary>
    /// Handles the ScanStartChanged event from the PLC service. Updates the internal scan start state,
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="active">The new scan start state.</param>
    private void PlcService_ScanStartChanged(object? sender, bool active)
    {
        _plcScanStart = active;
        AppendDiagnosticsThreadSafe($"[PLC] ScanStart = {active}.");
        UpdateOperatorRuntime();
    }

    /// <summary>
    /// Handles the ScanCompleteChanged event from the PLC service. Updates the internal scan complete state,
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="complete">The new scan complete state.</param>
    private void PlcService_ScanCompleteChanged(object? sender, bool complete)
    {
        _plcScanComplete = complete;
        AppendDiagnosticsThreadSafe($"[PLC] ScanComplete = {complete}.");
        UpdateOperatorRuntime();
    }


    /// <summary>
    /// Handles the ScanStartRequested event from the PLC service. If the application is in a
    /// state that allows starting a capture, it initiates the capture process.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data.</param>
    private void PlcService_ScanStartRequested(object? sender, EventArgs e)
    {
        if (_setupWizardOpen)
        {
            AppendDiagnosticsThreadSafe(
                "[PLC] ScanStart ignored while Production Setup / Calibration mode is open.");
            return;
        }

        if (!PipelineState.Instance.Settings.Plc.AutoStartCaptureOnScanStart)
        {
            AppendDiagnosticsThreadSafe(
                "[PLC] ScanStart received, but automatic capture start is disabled.");
            return;
        }

        void StartFromPlc()
        {
            if (_currentState is not (
                ApplicationState.CameraReady or
                ApplicationState.Completed or
                ApplicationState.Cancelled))
            {
                AppendDiagnostics(
                    $"[PLC] ScanStart ignored because application state is {_currentState}.");
                return;
            }

            AppendDiagnostics("[PLC] Starting 36-pair capture from ScanStart rising edge.");
            _plcStartInProgress = true;
            try
            {
                BtnStartCapture_Click(this, EventArgs.Empty);
            }
            finally
            {
                _plcStartInProgress = false;
            }
        }

        if (InvokeRequired)
            BeginInvoke((Action)StartFromPlc);
        else
            StartFromPlc();
    }

    /// <summary>
    /// Appends a diagnostics message to the diagnostics log in a thread-safe manner. If the form is disposing or disposed, it does nothing.
    /// </summary>
    /// <param name="message">The diagnostics message to append.</param>
    private void AppendDiagnosticsThreadSafe(string message)
    {
        if (_isDisposing || IsDisposed)
            return;

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action<string>(AppendDiagnosticsThreadSafe), message);
            }
            catch (InvalidOperationException)
            {
                // Form is closing.
            }

            return;
        }

        AppendDiagnostics(message);
    }


    /// <summary>
    /// Disposes of the PLC integration by unsubscribing from events, disposing of the PLC service, and setting the reference to null.
    /// </summary>
    private void DisposePlcIntegration()
    {
        if (_plcService is null)
            return;

        _plcService.DiagnosticsMessage -= PlcService_DiagnosticsMessage;
        _plcService.ScanStartRequested -= PlcService_ScanStartRequested;
        _plcService.ScanStartChanged -= PlcService_ScanStartChanged;
        _plcService.ConnectionStateChanged -= PlcService_ConnectionStateChanged;
        _plcService.ScanCompleteChanged -= PlcService_ScanCompleteChanged;
        _plcService.Dispose();
        _plcService = null;
    }

    // ================================================================
    // CAMERA EVENT HANDLERS
    // ================================================================



    /// <summary>
    /// Records every managed head-camera frame and forwards ownership
    /// to the common camera-frame handler.
    /// </summary>
    private void HeadCameraService_FrameAcquired(
        object? sender,
        CameraFrameEventArgs e)
    {
        RecordLiveFrame(CameraPosition.Head, e.FrameNumber);
        HandleCameraFrame(CameraPosition.Head, e);
    }

    /// <summary>
    /// Records every managed tail-camera frame and forwards ownership
    /// to the common camera-frame handler.
    /// </summary>
    private void TailCameraService_FrameAcquired(
        object? sender,
        CameraFrameEventArgs e)
    {
        RecordLiveFrame(CameraPosition.Tail, e.FrameNumber);
        HandleCameraFrame(CameraPosition.Tail, e);
    }

    /// <summary>
    /// Handles diagnostics messages from the head camera service.
    /// </summary>
    private void HeadCameraService_DiagnosticsMessage(
        object? sender,
        string message)
    {
        AppendDiagnostics($"[Head Camera] {message}");
    }

    /// <summary>
    /// Handles diagnostics messages from the tail camera service.
    /// </summary>
    private void TailCameraService_DiagnosticsMessage(
        object? sender,
        string message)
    {
        AppendDiagnostics($"[Tail Camera] {message}");
    }

    /// <summary>
    /// Tracks total managed frames and missing frame-number gaps.
    /// These counters include live-preview frames received while idle.
    /// </summary>
    private void RecordLiveFrame(
        CameraPosition position,
        long frameNumber)
    {
        if (position == CameraPosition.Head)
        {
            Interlocked.Increment(ref _headLiveFramesReceived);

            long previous = Interlocked.Exchange(
                ref _lastHeadFrameNumber,
                frameNumber);

            if (previous > 0 && frameNumber > previous + 1)
            {
                Interlocked.Add(
                    ref _headLiveFrameGaps,
                    frameNumber - previous - 1);
            }

            return;
        }

        Interlocked.Increment(ref _tailLiveFramesReceived);

        long previousTail = Interlocked.Exchange(
            ref _lastTailFrameNumber,
            frameNumber);

        if (previousTail > 0 && frameNumber > previousTail + 1)
        {
            Interlocked.Add(
                ref _tailLiveFrameGaps,
                frameNumber - previousTail - 1);
        }
    }

    /// <summary>
    /// Updates the camera preview for the specified camera position using the provided frame event arguments.
    /// It creates independent copies of the frame for display, normalizes the display copy, and updates
    /// the corresponding preview form if it is visible. The method also queues a combined preview
    /// update to ensure that the operator sees a consistent view of both cameras.
    /// If any errors occur during the update process, they are logged in the diagnostics.
    /// </summary>
    /// <param name="position">The position of the camera (Head or Tail).</param>
    /// <param name="e">The camera frame event arguments.</param>
    private void UpdateCameraPreview(
        CameraPosition position,
        CameraFrameEventArgs e)
    {
        if (_isDisposing ||
             e.Frame == null ||
             e.Frame.IsEmpty)
        {
            AppendDiagnostics(
       $"Preview rejected: {position}, " +
       $"Null={e.Frame == null}, " +
       $"Empty={e.Frame?.IsEmpty}");
            return;
        }

        // Create independent preview copies because e.Frame is owned by
        // the capture path and may be disposed after HandleCameraFrame returns.
        Mat? combinedCopy = e.Frame.Clone();
        Bitmap? childBitmap = null;

        try
        {

            //Wrap it so the bitmap is only created when the child form is visible:
            Form? targetForm =
                position == CameraPosition.Head
                ? _headCameraForm
                : _tailCameraForm;

            if (targetForm?.Visible == true)
            {
                // Normalize and clone the display copy. The clone ensures the Bitmap
                // has its own copy of pixel data and won't reference a disposed Mat.
                using Mat normalizedChildPreview =
                    CreateNormalizedDisplayFrame(e.Frame);

                using Mat bitmapData =
                    normalizedChildPreview.Clone();

                childBitmap = bitmapData.ToBitmap();
            }
           
           

            lock (_livePreviewLock)
            {
                if (position == CameraPosition.Head)
                {
                    Mat? oldHead = _latestHeadPreview;
                    _latestHeadPreview = combinedCopy;
                    oldHead?.Dispose();

                    combinedCopy = null;
                }
                else if (position == CameraPosition.Tail)
                {
                    Mat? oldTail = _latestTailPreview;
                    _latestTailPreview = combinedCopy;
                    oldTail?.Dispose();

                    combinedCopy = null;

                }
                else
                {
                    return;
                }
            }
            // Queue the combined view FIRST.
            // This ensures a child preview problem cannot prevent it.

            QueueCombinedPreviewUpdate();

            // Update the separate preview forms safely. Braces are explicit so
            // the tail branch cannot bind to the inner childBitmap condition.
            if (position == CameraPosition.Head && childBitmap is not null)
            {
                PostBitmapToPreviewForm(CameraPosition.Head,
                    _headCameraForm,
                    childBitmap);

                childBitmap = null;
            }
            else if (position == CameraPosition.Tail && childBitmap is not null)
            {
                PostBitmapToPreviewForm(CameraPosition.Tail,
                    _tailCameraForm,
                    childBitmap);

                childBitmap = null;
            }

        }
        catch (Exception ex)
        {
            AppendDiagnostics(
                $"Overview update failed for {position} camera: {ex.Message}");

        }
        finally
        {
            combinedCopy?.Dispose();
            childBitmap?.Dispose();

        }
    }

    /// <summary>
    /// Posts the provided bitmap to the specified preview form for the given camera position.
    /// If the preview form is not available or a post is already pending, the bitmap is disposed.
    /// </summary>
    private void PostBitmapToPreviewForm(
      CameraPosition position,
      Form? previewForm,
      Bitmap? bitmap)
    {
        if (bitmap is null)
            return;

        if (previewForm is null ||
            previewForm.IsDisposed ||
            previewForm.Disposing ||
            !previewForm.IsHandleCreated)
        {
            bitmap.Dispose();
            return;
        }

        bool postAccepted;

        if (position == CameraPosition.Head)
        {
            postAccepted =
                Interlocked.Exchange(ref _headPreviewPostPending, 1) == 0;
        }
        else
        {
            postAccepted =
                Interlocked.Exchange(ref _tailPreviewPostPending, 1) == 0;
        }

        // A preview update is already waiting on the UI thread.
        // Dispose this newer Bitmap rather than building an unlimited queue.
        if (!postAccepted)
        {
            bitmap.Dispose();
            return;
        }

        try
        {
            previewForm.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (previewForm.IsDisposed ||
                        previewForm.Disposing)
                    {
                        bitmap.Dispose();
                        return;
                    }

                    if (previewForm is HeadCameraForm headForm)
                    {
                        headForm.UpdatePreview(bitmap);
                        bitmap = null;
                        return;
                    }

                    if (previewForm is TailCameraForm tailForm)
                    {
                        tailForm.UpdatePreview(bitmap);
                        bitmap = null;
                        return;
                    }

                    bitmap.Dispose();
                    bitmap = null;
                }
                finally
                {
                    bitmap?.Dispose();

                    if (position == CameraPosition.Head)
                    {
                        Interlocked.Exchange(
                            ref _headPreviewPostPending,
                            0);
                    }
                    else
                    {
                        Interlocked.Exchange(
                            ref _tailPreviewPostPending,
                            0);
                    }
                }
            }));
        }
        catch
        {
            if (position == CameraPosition.Head)
            {
                Interlocked.Exchange(
                    ref _headPreviewPostPending,
                    0);
            }
            else
            {
                Interlocked.Exchange(
                    ref _tailPreviewPostPending,
                    0);
            }

            bitmap!.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Queues an update for the combined preview image by posting a request to the UI thread.
    /// </summary>
    private void QueueCombinedPreviewUpdate()
    {

        if (_isDisposing)
            return;

        if (IsDisposed)
        {
            AppendDiagnostics(
                "Combined preview skipped: main form is disposed.");
            return;
        }

        if (!IsHandleCreated)
        {
            AppendDiagnostics(
                "Combined preview skipped: main form handle not created.");
            return;
        }

        bool shouldPost;

        lock (_livePreviewLock)
        {
            shouldPost = !_combinedPreviewUpdatePosted;

            if (shouldPost)
                _combinedPreviewUpdatePosted = true;
        }

        if (!shouldPost)
            return;

        try
        {
            BeginInvoke(new Action(UpdateLiveCombinedPreview));
        }
        catch (InvalidOperationException)
        {
            lock (_livePreviewLock)
                _combinedPreviewUpdatePosted = false;
        }
    }

    /// <summary>
    /// Updates the combined preview image by combining the latest head and tail camera frames, normalizing them for
    /// display, and updating the picture box on the main form.
    /// If any errors occur during the update process, they are logged in the diagnostics.
    /// </summary>
    private void UpdateLiveCombinedPreview()
    {
        if (_isDisposing)
            return;

        if (IsDisposed)
        {
            AppendDiagnostics(
                "Combined preview update skipped: main form is disposed.");
            return;
        }

        if (pictureBoxConcatenated.IsDisposed)
        {
            AppendDiagnostics(
                "Combined preview update skipped: pictureBoxConcatenated is disposed.");
            return;
        }

        Mat? head;
        Mat? tail;

        lock (_livePreviewLock)
        {
            _combinedPreviewUpdatePosted = false;

            if (_latestHeadPreview == null ||
                _latestTailPreview == null ||
                _latestHeadPreview.IsEmpty ||
                _latestTailPreview.IsEmpty)
            {
                return;
            }

            head = _latestHeadPreview.Clone();
            tail = _latestTailPreview.Clone();
        }

        try
        {
            if (head == null || tail == null)
            {
                AppendDiagnostics(
                    "Combined preview update skipped: a cloned preview is null.");
                return;
            }

            // Normalize each camera independently for display so one
            // camera's brightness range does not control the other.
            using Mat normalizedHead =
                CreateNormalizedDisplayFrame(head);

            using Mat normalizedTail =
                CreateNormalizedDisplayFrame(tail);

            // Use the exact same crop, orientation, overlap, and seam settings
            // as the measurement pipeline so the operator preview matches the
            // 36 concatenated images that will actually be processed.
            using Mat combined =
                DualCameraFrameConcatenator.Concatenate(
                    normalizedHead,
                    normalizedTail,
                    _cameraSettings.Concatenation);

            if (combined.IsEmpty)
            {
                AppendDiagnostics(
                    "Combined preview is empty, skipping update.");
                return;
            }

            using Mat bitmapData = combined.Clone();
            Bitmap newBitmap = bitmapData.ToBitmap();

            pictureBoxConcatenated.SuspendLayout();

            try
            {
                Image? oldImage = pictureBoxConcatenated.Image;

                pictureBoxConcatenated.Image = newBitmap;
                pictureBoxConcatenated.SizeMode = PictureBoxSizeMode.Zoom;
                pictureBoxConcatenated.Visible = true;
                pictureBoxConcatenated.BringToFront();
                pictureBoxConcatenated.Invalidate();

                oldImage?.Dispose();
            }
            finally
            {
                pictureBoxConcatenated.ResumeLayout();
            }
        }
        catch (Exception ex)
        {
            AppendDiagnostics($"Error updating combined preview: {ex.Message}");
        }

        finally
        {
            head?.Dispose();
            tail?.Dispose();
        }
    }

    /// <summary>
    /// Creates a display-only normalized image.
    /// Never pass this Mat into the processing pipeline.
    /// </summary>
    private static Mat CreateNormalizedDisplayFrame(Mat source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.IsEmpty)
            return source.Clone();

        Mat normalized = new();

        CvInvoke.Normalize(
            source,
            normalized,
            0,
            255,
            Emgu.CV.CvEnum.NormType.MinMax,
            Emgu.CV.CvEnum.DepthType.Cv8U);

        return normalized;
    }


    private void ResolveAssignedCameras()
    {
        IReadOnlyList<DiscoveredCamera> cameras =
            SaperaCameraDiscoveryService.Discover();

        if (cameras.Count == 0)
        {
            throw new InvalidOperationException(
                "No Sapera acquisition cameras were discovered. " +
                "Verify the cameras are powered, connected, and visible in Sapera.");
        }

        CameraDeviceSettings head =
            _cameraSettings.HeadCamera;

        CameraDeviceSettings tail =
            _cameraSettings.TailCamera;

        DiscoveredCamera? discoveredHead =
            SaperaCameraDiscoveryService.FindAssignedCamera(
                cameras,
                head.AssignedSerialNumber);

        DiscoveredCamera? discoveredTail =
            SaperaCameraDiscoveryService.FindAssignedCamera(
                cameras,
                tail.AssignedSerialNumber);

        if (discoveredHead is null)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(head.AssignedSerialNumber)
                    ? "No Head/Dog camera has been assigned. Open Production Setup and assign one of the discovered cameras."
                    : $"The assigned Head/Dog camera serial '{head.AssignedSerialNumber}' was not found.");
        }

        if (discoveredTail is null)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(tail.AssignedSerialNumber)
                    ? "No Tail camera has been assigned. Open Production Setup and assign one of the discovered cameras."
                    : $"The assigned Tail camera serial '{tail.AssignedSerialNumber}' was not found.");
        }

        if (string.Equals(
            discoveredHead.SerialNumber,
            discoveredTail.SerialNumber,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The same physical camera cannot be assigned to both Head and Tail.");
        }

        head.ServerName = discoveredHead.ServerName;
        head.DeviceIndex = discoveredHead.DeviceIndex;

        tail.ServerName = discoveredTail.ServerName;
        tail.DeviceIndex = discoveredTail.DeviceIndex;

        AppendDiagnostics(
            $"Resolved Head camera serial {head.AssignedSerialNumber} -> " +
            $"{head.ServerName} [{head.DeviceIndex}]");

        AppendDiagnostics(
            $"Resolved Tail camera serial {tail.AssignedSerialNumber} -> " +
            $"{tail.ServerName} [{tail.DeviceIndex}]");
    }
    /// <summary>
    /// Handles a camera frame event for the specified camera position. It checks if the application is disposing
    /// or if the frame is null or empty, and disposes of the frame if necessary. If the preview should be updated,
    /// it calls UpdateCameraPreview. If a calibration capture session is active, it tries to add the frame to the
    /// session and updates the block label. If a capture manager is active, it adds the frame to the capture manager and updates the
    /// block label accordingly. If the capture is complete, it initiates the finish capture and process sequence.
    /// </summary>
    /// <param name="position"> The position of the camera. </param>
    /// <param name="e"> The camera frame event arguments. </param>
    private void HandleCameraFrame(
        CameraPosition position,
        CameraFrameEventArgs e)
    {
        if (_isDisposing ||
            e.Frame == null ||
            e.Frame.IsEmpty)
        {
            e.Frame?.Dispose();
            return;
        }

        // Preview is deliberately throttled. Production and calibration
        // capture still receive every incoming frame..
        // Display only a limited number of frames per second.
        // Do not throttle the production/calibration capture path below.
        if (ShouldUpdatePreview(position))
        {
            UpdateCameraPreview(position, e);
        }

        CalibrationFrameCaptureSession? calibrationSession =
            _calibrationCaptureSession;

        if (calibrationSession?.IsActive == true)
        {
            calibrationSession.TryAddFrame(position, e);
            UpdateBlockLabel(
                $"Calibration capture |" +
                $" Head {calibrationSession.HeadFrameCount} |" +
                $" Tail {calibrationSession.TailFrameCount}");
            return;
        }

        if (_captureManager == null ||
            !_captureManager.IsCapturingActive)
        {
            e.Frame.Dispose();
            return;
        }

        bool accepted = _captureManager.AddFrame(position, e);

        if (accepted)
        {
            int headCount = _captureManager.HeadFrameCount;
            int tailCount = _captureManager.TailFrameCount;

            UpdateBlockLabel(
                $"Block: Capturing |" +
                $" Head {headCount} |" +
                $" Tail {tailCount}");

            _operatorScanQualityForm?.ShowCapturing(GetOperatorRuntimeStatus());
        }

        if (!accepted ||
            !_captureManager.IsCaptureComplete())
        {
            return;
        }

        if (Interlocked.Exchange(
                ref _captureFinishRequested,
                1) != 0)
        {
            return;
        }
        _ = FinishCaptureAndProcessAsync();
    }


    /// <summary>
    /// Updates the block label on the main form with the provided text. If the form or label is disposed, it does nothing.
    /// </summary>
    /// <param name="text"> The text to display on the block label. </param>
    private void UpdateBlockLabel(string text)
    {
        if (_isDisposing ||
            IsDisposed ||
            lblBlock.IsDisposed)
        {
            return;
        }

        if (lblBlock.InvokeRequired)
        {
            try
            {
                lblBlock.BeginInvoke(
                    new Action(() => UpdateBlockLabel(text)));
            }
            catch (InvalidOperationException)
            {
                // Form is closing.
            }

            return;
        }

        lblBlock.Text = text;
    }

    /// <summary>
    /// Handles diagnostics messages from the capture manager and appends them to the diagnostics log in a thread-safe manner.
    /// </summary>
    /// <param name="sender"> The source of the diagnostics message. </param>
    /// <param name="message"> The diagnostics message. </param>
    private void CaptureManager_DiagnosticsMessage(
object? sender,
string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(
                new Action<object?, string>(
                    CaptureManager_DiagnosticsMessage),
                sender,
                message);

            return;
        }

        AppendDiagnostics(
            $"[Capture Manager] {message}");
    }

    /// <summary>
    /// Handles diagnostics messages from the processing coordinator and appends them to the diagnostics log in a thread-safe manner.
    /// </summary>
    /// <param name="sender"> The source of the diagnostics message. </param>
    /// <param name="message"> The diagnostics message. </param>
    private void ProcessingCoordinator_DiagnosticsMessage(
        object? sender,
        string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(
                new Action<object?, string>(
                    ProcessingCoordinator_DiagnosticsMessage),
                sender,
                message);

            return;
        }

        AppendDiagnostics($"[Processing] {message}");
    }


    /// <summary>
    /// Determines whether the Production Setup Wizard should be opened based on the current application settings and calibration state.
    /// </summary>
    /// <returns> True if the setup wizard should be opened; otherwise, false. </returns>
    private bool ShouldOpenSetupWizard()
    {
        AppSettings settings = PipelineState.Instance.Settings;

        if (!settings.Setup.MinimumSetupComplete)
        {
            return true;
        }

        try
        {
            ScannerLookupTable table = LookupTableStore.Load();

            if (!string.Equals(
                    settings.Setup.ActiveCalibrationId,
                    table.CalibrationId,
                    StringComparison.Ordinal))
            {
                return true;
            }

            string currentGeometryHash =
                ScannerCalibrationCompatibility.ComputeAcquisitionGeometrySha256(
                    settings.Acquisition);

            if (!string.Equals(
                    currentGeometryHash,
                    table.AcquisitionGeometrySha256,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    settings.Acquisition.HeadCamera.ServerName,
                    table.HeadCameraServerName,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    settings.Acquisition.TailCamera.ServerName,
                    table.TailCameraServerName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            ReadOnlyMemory<byte> headSnapshot =
                _headCameraService?.LoadedConfigurationSnapshot ?? ReadOnlyMemory<byte>.Empty;
            ReadOnlyMemory<byte> tailSnapshot =
                _tailCameraService?.LoadedConfigurationSnapshot ?? ReadOnlyMemory<byte>.Empty;

            if (headSnapshot.IsEmpty || tailSnapshot.IsEmpty)
                return true;

            string headConfigurationHash = Convert.ToHexString(
                SHA256.HashData(headSnapshot.Span));
            string tailConfigurationHash = Convert.ToHexString(
                SHA256.HashData(tailSnapshot.Span));

            if (!string.Equals(
                    headConfigurationHash,
                    table.HeadCameraConfigurationSha256,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    tailConfigurationHash,
                    table.TailCameraConfigurationSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return Math.Abs(
                       settings.Setup.PendingLengthPixelsPerMM -
                       table.LengthPixelsPerMM) > 1e-9;
        }
        catch
        {
            return true;
        }
    }


    /// <summary>
    /// Handles the click event for the Production Setup button. It opens the Production Setup Wizard
    /// if the application is in a suitable state and no capture is currently active.
    /// </summary>
    /// <param name="sender"> The source of the click event. </param>
    /// <param name="e"> The event data. </param>
    private void BtnProductionSetup_Click(object sender, EventArgs e)
    {
        OpenProductionSetupWizard();
    }


    /// <summary>
    /// Opens the Production Setup Wizard if it is not already open and the application is in a suitable state.
    /// </summary>
    private void OpenProductionSetupWizard()
    {
        if (_setupWizardOpen)
        {
            return;
        }

        if (_captureManager?.IsCapturingActive == true ||
            _currentState == ApplicationState.Processing)
        {
            MessageBox.Show(
                this,
                "Finish or cancel the current block before opening Production Setup.",
                "Setup Unavailable",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _setupWizardOpen = true;
        AppendDiagnostics(
            "Production Setup / Calibration mode opened. Normal PLC-triggered capture is inhibited while this form is open.");

        try
        {
            var context = new ProductionSetupContext
            {
                GetCameraStatus = GetCameraHardwareStatus,

                DiscoverCameras =
        () => SaperaCameraDiscoveryService.Discover(),

                ReconnectCamerasAsync = ReconnectCamerasForSetupAsync,
               
                CaptureCalibrationFramesAsync = CaptureCalibrationFramesAsync,
                ShowHeadPreview = ShowHeadPreviewForSetup,
                ShowTailPreview = ShowTailPreviewForSetup,
                LogMessage = AppendDiagnostics
            };

            using var form = new ProductionSetupWizardForm(context);
            form.ShowDialog(this);
        }
        finally
        {
            _setupWizardOpen = false;
            AppendDiagnostics(
                "Production Setup / Calibration mode closed.");

            if (_headCameraService?.IsConnected == true &&
                _tailCameraService?.IsConnected == true)
            {
                SetUIState(ApplicationState.CameraReady);
            }
        }
    }

    /// <summary>
    /// Shows the head camera preview form for setup. If the form is unavailable or disposed, it throws an InvalidOperationException.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the head camera preview form is unavailable or disposed.</exception>
    private void ShowHeadPreviewForSetup()
    {
        if (_headCameraForm is null ||
            _headCameraForm.IsDisposed)
        {
            throw new InvalidOperationException(
                "The Head camera preview host is unavailable. " +
                "Reconnect the cameras and try again.");
        }

        if (!_headCameraForm.Visible)
            _headCameraForm.Show(this);

        _headCameraForm.WindowState =
            FormWindowState.Normal;

        _headCameraForm.BringToFront();
        _headCameraForm.Activate();
    }


    /// <summary>
    /// Shows the tail camera preview form for setup. If the form is unavailable or disposed, it throws an InvalidOperationException.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the tail camera preview form is unavailable or disposed.</exception>
    private void ShowTailPreviewForSetup()
    {
        if (_tailCameraForm is null ||
            _tailCameraForm.IsDisposed)
        {
            throw new InvalidOperationException(
                "The Tail camera preview host is unavailable. " +
                "Reconnect the cameras and try again.");
        }

        if (!_tailCameraForm.Visible)
            _tailCameraForm.Show(this);

        _tailCameraForm.WindowState =
            FormWindowState.Normal;

        _tailCameraForm.BringToFront();
        _tailCameraForm.Activate();
    }


    /// <summary>
    /// Retrieves the current status of the camera hardware, including connection status, server names, serial numbers, configuration paths,
    /// and whether configurations are loaded.
    /// </summary>
    /// <returns>The current status of the camera hardware.</returns>
    private CameraHardwareStatus GetCameraHardwareStatus()
    {
        CameraDeviceSettings head =
            PipelineState.Instance.Settings.Acquisition.HeadCamera;
        CameraDeviceSettings tail =
            PipelineState.Instance.Settings.Acquisition.TailCamera;

        return new CameraHardwareStatus
        {
            HeadConnected = _headCameraService?.IsConnected == true,
            TailConnected = _tailCameraService?.IsConnected == true,
            HeadServerName = head.ServerName,
            TailServerName = tail.ServerName,
            HeadSerialNumber = _headCameraService?.ConnectedSerialNumber ?? string.Empty,
            TailSerialNumber = _tailCameraService?.ConnectedSerialNumber ?? string.Empty,
            HeadConfigurationPath = head.GetInitializationDescription(),
            TailConfigurationPath = tail.GetInitializationDescription(),
            HeadConfigurationLoaded =
                _headCameraService?.LoadedConfigurationSnapshot.IsEmpty == false,
            TailConfigurationLoaded =
                _tailCameraService?.LoadedConfigurationSnapshot.IsEmpty == false
        };
    }

    /// <summary>
    /// Reconnects the cameras for setup by initializing the camera services and returning the current camera hardware status.
    /// </summary>
    /// <returns>The current status of the camera hardware.</returns>
    /// <exception cref="InvalidOperationException">Thrown if a camera capture is active.</exception>
    private Task<CameraHardwareStatus> ReconnectCamerasForSetupAsync()
    {
        if (_captureManager?.IsCapturingActive == true ||
            _calibrationCaptureSession?.IsActive == true)
        {
            throw new InvalidOperationException(
                "A camera capture is active. Wait for it to finish before reconnecting cameras.");
        }

        InitializeCameraServices();
        return Task.FromResult(GetCameraHardwareStatus());
    }

    /// <summary>
    /// Captures a specified number of synchronized calibration frame pairs from the head and tail cameras.
    /// </summary>
    /// <param name="pairCount">The number of synchronized frame pairs to capture.</param>
    /// <param name="token">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the captured calibration frames.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the capture cannot be performed.</exception>
    private async Task<CalibrationCaptureBatch> CaptureCalibrationFramesAsync(
        int pairCount,
        CancellationToken token)
    {
        if (!_setupWizardOpen)
        {
            throw new InvalidOperationException(
                "Calibration capture is only available from the Production Setup form.");
        }

        if (_captureManager?.IsCapturingActive == true ||
            _currentState == ApplicationState.Processing)
        {
            throw new InvalidOperationException(
                "A normal production capture or processing operation is active.");
        }

        if (_headCameraService?.IsConnected != true ||
            _tailCameraService?.IsConnected != true)
        {
            throw new InvalidOperationException(
                "Both production cameras must be connected before calibration capture.");
        }

        _calibrationCaptureSession?.Dispose();
        _calibrationCaptureSession = new CalibrationFrameCaptureSession(
            PipelineState.Instance.Settings.Acquisition,
            pairCount,
            _headCameraService.LoadedConfigurationSnapshot,
            _tailCameraService.LoadedConfigurationSnapshot);

        CalibrationFrameCaptureSession session =
            _calibrationCaptureSession;

        using CancellationTokenRegistration cancellation = token.Register(() =>
            session.Cancel("Calibration capture was cancelled."));

        try
        {
            SetUIState(ApplicationState.Capturing);
            _headCameraService.StartManagedFrameCapture();
            _tailCameraService.StartManagedFrameCapture();

            AppendDiagnostics(
                $"Calibration capture started for {pairCount} synchronized frame pairs.");

            TimeSpan timeout = TimeSpan.FromSeconds(15);
            CalibrationCaptureBatch? result = null;

            try
            {
                result = await session.Completion.WaitAsync(timeout, token);
            }
            catch (TimeoutException)
            {
                AppendDiagnostics("No camera frames were received. If Encoder mode is selected, verify that\r\n" +
                    "encoder pulses are connected to Line1, or switch to Frame Rate mode for setup.");
            }

            AppendDiagnostics(
                $"Calibration capture complete: {result?.HeadFrameCount} head and {result?.TailFrameCount} tail frames.");

            return result!;
        }
        finally
        {
            // Managed frame callbacks remain enabled because they also support
            // the normal idle live preview. The calibration session itself
            // determines whether incoming frames are retained.

            if (ReferenceEquals(_calibrationCaptureSession, session))
            {
                _calibrationCaptureSession = null;
            }

            session.Dispose();

            if (!_isDisposing)
            {
                SetUIState(ApplicationState.CameraReady);
                UpdateBlockLabel("Block: Calibration capture complete");
            }
        }
    }

    // ================================================================
    // UI BUTTON HANDLERS
    // ================================================================

    /// <summary>
    /// Handles the click event for the "Start Capture" button. It initiates the capture process if the
    /// application is in a suitable state and no setup wizard is open.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data.</param>
    /// <exception cref="InvalidOperationException">Thrown if the capture cannot be started.</exception>
    private void BtnStartCapture_Click(object sender, EventArgs e)
    {
        try
        {
            if (_setupWizardOpen)
            {
                AppendDiagnostics(
                    "Normal block capture is blocked while Production Setup / Calibration mode is open.");
                return;
            }

            bool productionMode =
                PipelineState.Instance.Settings.Production.OperatingMode ==
                ChargerOperatingMode.Production;

            if (productionMode && !_plcStartInProgress)
            {
                AppendDiagnostics(
                    "Manual capture start is blocked in Production mode. A fresh PLC ScanStart edge is required.");
                return;
            }

            if (productionMode && _plcService?.IsScanCycleActive != true)
            {
                AppendDiagnostics(
                    "PLC capture start was blocked because no active ScanStart cycle was established.");
                return;
            }

            AppendDiagnostics("Starting log capture...");

            Interlocked.Exchange(
                ref _captureFinishRequested,
                0);

            if (_captureManager is null ||
                _headCameraService is null ||
                _tailCameraService is null)
            {
                throw new InvalidOperationException(
                    "Capture services are not initialized.");
            }

            _captureManager.BeginCapture(
                _headCameraService.LoadedConfigurationSnapshot,
                _tailCameraService.LoadedConfigurationSnapshot);

            _headCameraService?.StartManagedFrameCapture();
            _tailCameraService?.StartManagedFrameCapture();

            lblBlock.Text = "Block: Capturing | Head 0 | Tail 0";

            SetUIState(ApplicationState.Capturing);
            _operatorScanQualityForm?.ShowCapturing(GetOperatorRuntimeStatus());
            // Managed frame capture is already running for live preview.
        }
        catch (Exception ex)
        {
            AppendDiagnostics($"Error starting capture: {ex.Message}");
            _operatorScanQualityForm?.ShowFailure(
                "The scan did not start correctly. Check camera and PLC status before retrying.",
                GetOperatorRuntimeStatus());
            SetUIState(ApplicationState.Faulted);
        }
    }

    /// <summary>
    /// Handles the click event for the "Stop Capture" button. It stops the capture process if the application is
    /// in a suitable state and manual capture is allowed.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data.</param>
    private void BtnStopCapture_Click(object sender, EventArgs e)
    {
        try
        {
            if (!ManualCaptureAllowed())
            {
                AppendDiagnostics(
                    "Manual early-stop is blocked in Production mode. The PLC rotation cycle and exact 36-frame quota control completion.");
                return;
            }

            AppendDiagnostics("Stopping capture...");

            // Keep managed capture running so the live previews
            // remain visible after the operator stops a log capture.
            if (_captureManager?.IsCapturingActive == true)

            {
                _ = FinishCaptureAndProcessAsync();
            }
        }
        catch (Exception ex)
        {
            AppendDiagnostics($"Error stopping capture: {ex.Message}");
            SetUIState(ApplicationState.Faulted);
        }
    }

    // ================================================================
    // CAPTURE COMPLETION AND PROCESSING
    // ================================================================

    /// <summary>
    /// Asynchronously finishes the capture process and initiates the processing of the captured frames.
    /// It handles PLC ScanComplete verification if required, updates the UI state,
    /// and manages the processing lifecycle, including saving results and sending payloads to the PLC if applicable.
    /// </summary>
    /// <returns> A task that represents the asynchronous operation. </returns>
    /// <exception cref="InvalidOperationException">Thrown if the PLC service is unavailable when required for production processing.</exception>
    private async Task FinishCaptureAndProcessAsync()
    {
        if (_captureManager == null || _processingCoordinator == null)
            return;

        try
        {
            if (!_captureManager.IsCapturingActive)
                return;

            bool productionMode =
                PipelineState.Instance.Settings.Production.OperatingMode ==
                ChargerOperatingMode.Production;
            bool requireScanComplete =
                productionMode &&
                PipelineState.Instance.Settings.Plc.RequireScanCompleteBeforeProcessing;
            bool plcScanCompleteVerified = false;

            if (requireScanComplete)
            {
                if (_plcService is null)
                {
                    throw new InvalidOperationException(
                        "Production processing requires the PLC ScanComplete handshake, but the PLC service is unavailable.");
                }

                lblBlock.Text = "Block: 36 pairs captured | Waiting for PLC ScanComplete";
                _operatorScanQualityForm?.ShowWaitingForScanComplete(GetOperatorRuntimeStatus());
                AppendDiagnostics(
                    "[PLC] All 36 frame pairs are present. Waiting for the PLC-controlled rotation cycle to assert ScanComplete...");

                await _plcService.WaitForScanCompleteAsync();
                plcScanCompleteVerified = true;
                AppendDiagnostics(
                    "[PLC] ScanComplete verified. Processing may begin.");
            }

            SetUIState(ApplicationState.CaptureComplete);

            var completedCapture = _captureManager.FinishCapture();

            // Keep managed frames enabled for the idle live preview.
            // FinishCapture removes the retained 36-pair capture from the
            // active capture manager; future idle frames are not stored there.

            lblBlock.Text =
            $"Block: Complete | " +
            $"Head {completedCapture.HeadFrameCount} | " +
            $"Tail {completedCapture.TailFrameCount}";

            AppendDiagnostics(
                $"Capture finished: " +
                $"{completedCapture.FramePairCount} frame pairs, " +
                $"{completedCapture.HeadFrameCount} head frames, " +
                $"{completedCapture.TailFrameCount} tail frames -> " +
                $"{completedCapture.FramePairCount} concatenated processing frames, " +
                $"{completedCapture.CaptureDurationSeconds:F2}s.");

            SetUIState(ApplicationState.Processing);
            _operatorScanQualityForm?.ShowProcessing(GetOperatorRuntimeStatus());

            using var processingCancellation = new CancellationTokenSource();
            _processingCancellation = processingCancellation;

            try
            {
                string diagnosticsReport = await _processingCoordinator.ProcessCompletedCaptureAsync(
                    completedCapture,
                    processingCancellation.Token);

                AppendDiagnostics(diagnosticsReport);

                if (PipelineState.Instance.CaptureAudit is not null)
                {
                    PipelineState.Instance.CaptureAudit.PlcScanCompleteRequired =
                        requireScanComplete;
                    PipelineState.Instance.CaptureAudit.PlcScanCompleteVerified =
                        plcScanCompleteVerified;
                }

                _latestResult = PipelineState.Instance.FinalSpindleRecommendation;

                DisplayFinalResults();

                OperatorScanQualityResult operatorResult =
                    OperatorScanQualityAnalyzer.Analyze(PipelineState.Instance);
                PipelineState.Instance.OperatorScanQuality = operatorResult;
                _operatorScanQualityForm?.ShowResult(
                    operatorResult,
                    GetOperatorRuntimeStatus());

                SetUIState(ApplicationState.Completed);

                AppendDiagnostics("Saving results...");

                try
                {
                    string savePath = _processingCoordinator.SaveResults();
                    AppendDiagnostics($"Results saved: {savePath}");
                }
                catch (Exception saveEx)
                {
                    AppendDiagnostics($"Save failed: {saveEx.Message}");

                    if (productionMode)
                    {
                        throw new InvalidOperationException(
                            "Production result saving failed. PLC delivery is blocked because the scan audit record was not preserved.",
                            saveEx);
                    }
                }

                ProductionReadinessReport readiness =
                    _processingCoordinator.PrepareProductionCommand(
                        _plcService?.IsConnected == true);

                if (PipelineState.Instance.Settings.Plc.AutoSendResultsAfterSave)
                {
                    if (readiness.CanSendToPlc &&
                        PipelineState.Instance.PlcPayload is not null &&
                        _plcService is not null)
                    {
                        AppendDiagnostics(
                            "[PLC] Production checks passed. Sending restricted XY payloads...");

                        var transmission = await _plcService.SendPayloadAsync(
                            PipelineState.Instance.PlcPayload,
                            processingCancellation.Token);

                        LogProcessingCoordinator.RecordPlcTransmission(transmission);

                        AppendDiagnostics(
                            transmission.Success
                                ? "[PLC] Result accepted by verified tag readback."
                                : $"[PLC] Result was not accepted: {transmission.Message}");

                        if (!transmission.Success && productionMode)
                            SetUIState(ApplicationState.Faulted);
                    }
                    else
                    {
                        AppendDiagnostics(
                            $"[PLC] Automatic send blocked by {readiness.BlockingCount} production check(s).");

                        if (productionMode)
                            SetUIState(ApplicationState.Faulted);
                    }
                }
                else
                {
                    AppendDiagnostics("[PLC] Automatic result sending is disabled.");
                }

                UpdateUIForState(_currentState);
            }
            catch (OperationCanceledException)
            {
                AppendDiagnostics("Processing cancelled by user.");
                SetUIState(ApplicationState.Cancelled);
            }
        }
        catch (Exception ex)
        {
            if (_captureManager?.IsCapturingActive == true)
                _captureManager.CancelCapture();

            AppendDiagnostics($"Processing error: {ex.Message}");
            _operatorScanQualityForm?.ShowFailure(
                "The block scan could not be processed. Rescan the block.",
                GetOperatorRuntimeStatus());
            SetUIState(ApplicationState.Faulted);
        }
        finally
        {
            _processingCancellation?.Dispose();
            _processingCancellation = null;


            // Managed callbacks remain active for the live preview.
            // Only application shutdown or camera reconnection stops them.


            Interlocked.Exchange(
                ref _captureFinishRequested,
                0);
        }
    }

    // ================================================================
    // RESULTS DISPLAY
    // ================================================================


    /// <summary>
    /// Displays the final spindle recommendation results in the diagnostics log, including optimization mode, selected strategy,
    /// </summary>
    private void DisplayFinalResults()
    {
        var state = PipelineState.Instance;

        if (state.FinalSpindleRecommendation != null)
        {
            var rec = state.FinalSpindleRecommendation;

            // Convert MM to inches (1 inch = 25.4 mm)
            double tailXInches = rec.CommandTailXMM / 25.4;
            double tailYInches = rec.CommandTailYMM / 25.4;
            double headXInches = rec.CommandHeadXMM / 25.4;
            double headYInches = rec.CommandHeadYMM / 25.4;

            AppendDiagnostics("");
            AppendDiagnostics("============================================");
            AppendDiagnostics("FINAL SPINDLE RECOMMENDATION");
            AppendDiagnostics("============================================");
            AppendDiagnostics($"Optimization Mode: {rec.OptimizationMode}");
            AppendDiagnostics($"Selected Strategy: {rec.SelectedStrategy}");

            if (rec.OptimizationMode == SpindleOptimizationMode.PenaltyScored)
            {
                AppendDiagnostics($"Baseline Risk Score: {rec.BaselineRiskScore:F1} ({rec.BaselineRiskRating})");
                AppendDiagnostics($"Optimized Risk Score: {rec.RiskScore:F1} ({rec.RiskRating})");
                AppendDiagnostics($"Improvement: {rec.RiskScoreReductionPercent:F1}%");
            }
            else
            {
                AppendDiagnostics(
                    rec.OptimizationMode == SpindleOptimizationMode.RecoveryOptimized
                        ? "Final Winner Chosen By Virtual-Peel Recovery: Yes"
                        : "Penalty Score Used For Position: No");
                AppendDiagnostics($"Usable Full 50-in Sheets: {rec.UsableFullSheets} of {rec.TotalFullSheets}");
                AppendDiagnostics($"Rejected Full 50-in Sheets: {rec.RejectedFullSheets}");
                AppendDiagnostics($"Usable Veneer Length (in): {rec.UsableVeneerLengthMM / 25.4:F2}");

                if (rec.InscribedMajorDiameterMM > 0)
                {
                    AppendDiagnostics($"Major Inscribed Diameter (in): {rec.InscribedMajorDiameterMM / 25.4:F3}");
                    AppendDiagnostics($"Minor Inscribed Diameter (in): {rec.InscribedMinorDiameterMM / 25.4:F3}");
                }
            }

            AppendDiagnostics("");
            AppendDiagnostics($"Tail X Offset (in): {tailXInches:F3}  (mm: {rec.CommandTailXMM:F2})");
            AppendDiagnostics($"Tail Y Offset (in): {tailYInches:F3}  (mm: {rec.CommandTailYMM:F2})");
            AppendDiagnostics($"Head X Offset (in): {headXInches:F3}  (mm: {rec.CommandHeadXMM:F2})");
            AppendDiagnostics($"Head Y Offset (in): {headYInches:F3}  (mm: {rec.CommandHeadYMM:F2})");
            AppendDiagnostics($"Recommended Action: {rec.RecommendedAction}");
            if (rec.Warnings.Count > 0)
            {
                AppendDiagnostics("Warnings:");
                foreach (var warning in rec.Warnings)
                {
                    AppendDiagnostics($"  - {warning}");
                }
            }
            AppendDiagnostics("============================================");
        }
    }

    // ================================================================
    // UI STATE MANAGEMENT
    // ================================================================

    /// <summary>
    /// Sets the current application state and updates the UI accordingly. This method ensures that UI updates are performed
    /// on the correct thread, using Invoke if necessary.
    /// </summary>
    /// <param name="state">The new application state to set.</param>
    private void SetUIState(ApplicationState state)
    {
        _currentState = state;

        if (InvokeRequired)
        {
            Invoke(new Action(() => UpdateUIForState(state)));
        }
        else
        {
            UpdateUIForState(state);
        }
    }

    /// <summary>
    /// Updates the UI elements based on the current application state. This method modifies labels and button states to reflect
    /// the current state of the application.
    /// </summary>
    /// <param name="state">The current application state.</param>
    private void UpdateUIForState(ApplicationState state)
    {
        switch (state)
        {
            case ApplicationState.Idle:
                lblMode.Text = "Mode: Idle";
                lblBlock.Text = "Block: None";

                BtnStartCapture.Enabled = false;
                BtnStopCapture.Enabled = false;
                break;

            case ApplicationState.CameraReady:
                lblMode.Text = "Mode: Cameras Ready";
                lblBlock.Text = "Block: None";

                BtnStartCapture.Enabled = ManualCaptureAllowed();
                BtnStopCapture.Enabled = false;
                break;

            case ApplicationState.Capturing:
                lblMode.Text = "Mode: Capturing";
                lblBlock.Text = "Block: Active";

                BtnStartCapture.Enabled = false;
                BtnStopCapture.Enabled = ManualCaptureAllowed();
                break;

            case ApplicationState.CaptureComplete:
                lblMode.Text = "Mode: Capture Complete";
                lblBlock.Text = "Block: Complete";

                BtnStartCapture.Enabled = false;
                BtnStopCapture.Enabled = false;
                break;

            case ApplicationState.Processing:
                lblMode.Text = "Mode: Processing";
                lblBlock.Text = "Block: Processing";

                BtnStartCapture.Enabled = false;
                BtnStopCapture.Enabled = false;
                break;

            case ApplicationState.Completed:
                lblMode.Text = "Mode: Complete";
                lblBlock.Text = "Block: Results Saved";

                BtnStartCapture.Enabled = ManualCaptureAllowed();
                BtnStopCapture.Enabled = false;
                break;

            case ApplicationState.Faulted:
                lblMode.Text = "Mode: Faulted";
                lblBlock.Text = "Block: Error";

                BtnStartCapture.Enabled = false;
                BtnStopCapture.Enabled = false;
                break;

            case ApplicationState.Cancelled:
                lblMode.Text = "Mode: Cancelled";
                lblBlock.Text = "Block: Cancelled";

                BtnStartCapture.Enabled = ManualCaptureAllowed();
                BtnStopCapture.Enabled = false;
                break;
        }

        cmbOptimizationMode.Enabled =
            PipelineState.Instance.Settings.Production.OperatingMode !=
                ChargerOperatingMode.Production &&
            state is ApplicationState.Idle or
                     ApplicationState.CameraReady or
                     ApplicationState.Completed or
                     ApplicationState.Cancelled;
    }


    /// <summary>
    /// Determines whether manual capture is allowed based on the current operating mode of the application.
    /// </summary>
    /// <returns>True if manual capture is allowed; otherwise, false.</returns>
    private static bool ManualCaptureAllowed() =>
        PipelineState.Instance.Settings.Production.OperatingMode !=
        ChargerOperatingMode.Production;

    // ================================================================
    // DIAGNOSTICS OUTPUT
    // ================================================================

    /// <summary>
    /// Starts the operator diagnostics refresh loop.
    /// This timer updates labels only; it does not affect camera acquisition.
    /// </summary>
    private void StartDiagnosticsTimer()
    {
        StopDiagnosticsTimer();

        _diagnosticsTimer = new System.Windows.Forms.Timer
        {
            Interval = 500
        };

        _diagnosticsTimer.Tick += DiagnosticsTimer_Tick;
        _diagnosticsTimer.Start();

        RefreshDiagnosticsLabel();
    }

    /// <summary>
    /// Handles the Tick event of the diagnostics timer, refreshing the diagnostics label to display live camera traffic and active-capture diagnostics.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The event data.</param>
    private void DiagnosticsTimer_Tick(object? sender, EventArgs e)
    {
        RefreshDiagnosticsLabel();
    }

    /// <summary>
    /// Refreshes the diagnostics label to display live camera traffic and active-capture diagnostics.
    /// </summary>
    private void RefreshDiagnosticsLabel()
    {
        if (_isDisposing ||
            IsDisposed ||
            lblDiagnostics.IsDisposed)
        {
            return;
        }

        long headLive = Interlocked.Read(ref _headLiveFramesReceived);
        long tailLive = Interlocked.Read(ref _tailLiveFramesReceived);
        long headGaps = Interlocked.Read(ref _headLiveFrameGaps);
        long tailGaps = Interlocked.Read(ref _tailLiveFrameGaps);

        // Capture counters increase only while an operator capture is active.
        long headCaptureReceived =
            _diagnostics?.GetCounter("HeadFramesReceived") ?? 0;

        long headCaptureDropped =
            _diagnostics?.GetCounter("HeadFramesDropped") ?? 0;

        long tailCaptureReceived =
            _diagnostics?.GetCounter("TailFramesReceived") ?? 0;

        long tailCaptureDropped =
            _diagnostics?.GetCounter("TailFramesDropped") ?? 0;

        lblDiagnostics.Text =
            $"Head Live: {headLive} Gap: {headGaps} " +
            $"Capture: {headCaptureReceived} Drop: {headCaptureDropped} | " +
            $"Tail Live: {tailLive} Gap: {tailGaps} " +
            $"Capture: {tailCaptureReceived} Drop: {tailCaptureDropped}";
    }

    /// <summary>
    /// Stops the operator diagnostics refresh loop and cleans up the timer resources.
    /// </summary>
    private void StopDiagnosticsTimer()
    {
        if (_diagnosticsTimer == null)
            return;

        _diagnosticsTimer.Stop();
        _diagnosticsTimer.Tick -= DiagnosticsTimer_Tick;
        _diagnosticsTimer.Dispose();
        _diagnosticsTimer = null;
    }

    /// <summary>
    /// Appends a diagnostics message to the diagnostics text box and logs it. This method ensures that UI updates
    /// are performed on the correct thread, using BeginInvoke if necessary.
    /// </summary>
    /// <param name="message">The diagnostics message to append.</param>
    private void AppendDiagnostics(string message)
    {
        if (_isDisposing ||
            IsDisposed ||
            txtDiagnostics.IsDisposed)
        {
            return;
        }

        if (txtDiagnostics.InvokeRequired)
        {
            try
            {
                txtDiagnostics.BeginInvoke(
                    new Action(() => AppendDiagnostics(message)));
            }
            catch (InvalidOperationException)
            {
                // The form handle is being destroyed.
            }

            return;
        }

        string line =
     $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}";

        Logger.Info(message);

        txtDiagnostics.AppendText(
            line + Environment.NewLine);

        try
        {
            File.AppendAllText(
                ApplicationPaths.OperatorDiagnosticsLogPath,
                line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Logger.Warn(
                ex,
                "Failed to write operator diagnostics log.");
        }
    }

    // ================================================================
    // Shutdown and Cleanup
    // ================================================================

    /// <summary>
    /// Disposes of the camera services by unsubscribing from events and stopping live capture.
    /// This method ensures that resources are released properly during application shutdown or when reconnecting cameras.
    /// </summary>
    private void DisposeCameraServices()
    {
        _headCameraService?.FrameAcquired -=
                HeadCameraService_FrameAcquired;

        _tailCameraService?.FrameAcquired -=
                TailCameraService_FrameAcquired;

        _headCameraService?.DiagnosticsMessage -=
                HeadCameraService_DiagnosticsMessage;

        _tailCameraService?.DiagnosticsMessage -=
                TailCameraService_DiagnosticsMessage;

        DisposeCameraService(ref _headCameraService);
        DisposeCameraService(ref _tailCameraService);
    }

    /// <summary>
    /// Disposes of a specific camera service by stopping managed frame capture, stopping live capture, disconnecting, and disposing of the service.
    /// </summary>
    /// <param name="service">The camera service to dispose.</param>
    private static void DisposeCameraService(
        ref GenieNanoCameraService? service)
    {
        if (service == null)
            return;

        try
        {
            service.StopManagedFrameCapture();
            service.StopLive();
            service.Disconnect();
            service.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Camera shutdown reported an error.");
        }
        finally
        {
            service = null;
        }
    }


    /// <summary>
    /// Disposes of a preview form by calling its Dispose method and setting the reference to null. This method ensures that
    /// resources are released properly when the form is no longer needed.
    /// </summary>
    /// <typeparam name="TForm">The type of the form to dispose.</typeparam>
    /// <param name="form">The form instance to dispose.</param>
    private static void DisposePreviewForm<TForm>(
        ref TForm? form)
        where TForm : Form
    {
        if (form == null)
            return;

        try
        {
            form.Dispose();
        }
        finally
        {
            form = null;
        }
    }

}
