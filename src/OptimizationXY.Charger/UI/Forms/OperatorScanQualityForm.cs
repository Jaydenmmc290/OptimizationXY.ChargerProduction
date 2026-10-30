using OptimizationXY.Charger.Models.Operator;

namespace OptimizationXY.Charger.UI.Forms;

/// <summary>
/// Read-only lathe-operator display. The form is updated by the main production
/// workflow and intentionally contains no camera/live-view controls.
/// <para>
/// Screen states in a normal cycle: Waiting ("READY FOR NEXT BLOCK") →
/// Capturing ("SCANNING BLOCK", frame counter) → waiting for ScanComplete →
/// Processing ("BUILDING LOG PROFILE") → Result (GOOD / CHECK SCAN / RESCAN)
/// or Failure. The colour bar matches the state. Every Show… method may be
/// called from any thread; the work is moved to the UI thread.
/// The operator cannot close this window; only application exit closes it.
/// </para>
/// </summary>
public partial class OperatorScanQualityForm : Form
{
    // Set only by CloseForApplicationExit, so the operator cannot close the window.
    private bool _allowClose;

    /// <summary>Creates the window showing the waiting state.</summary>
    public OperatorScanQualityForm()
    {
        InitializeComponent();
        ShowWaiting(new OperatorRuntimeStatus());
    }

    // True while the screen still shows the result (or failure) of the block
    // scanned before. Only touched on the UI thread.
    private bool _previousBlockOnScreen;

    /// <summary>Idle: ready for the next block, waiting for the PLC ScanStart signal.</summary>
    public void ShowWaiting(OperatorRuntimeStatus runtime)
    {
        RunOnUiThread(() =>
        {
            SetStatus(ScanQualityStatus.Waiting, "READY FOR NEXT BLOCK", "Waiting for PLC ScanStart.");
            ClearResultValues();
            logProfileControl.SetProfile(null);
            UpdateRuntime(runtime);
        });
    }

    /// <summary>Capture running: shows captured / expected frame pairs.</summary>
    public void ShowCapturing(OperatorRuntimeStatus runtime)
    {
        RunOnUiThread(() =>
        {
            SetStatus(
                ScanQualityStatus.Capturing,
                "SCANNING BLOCK",
                $"Capturing rotating profile: {runtime.CapturedPairs} / {runtime.ExpectedPairs} frame pairs.");

            // A new block is being scanned: clear the previous block's numbers
            // and profile picture so the screen is empty until the new one is ready.
            // This method is called for every frame during a capture, so the
            // clearing is done once, on the first call after a result was shown.
            if (_previousBlockOnScreen)
            {
                _previousBlockOnScreen = false;
                ClearResultValues();
                logProfileControl.SetProfile(null);
                lblDetail.Text = "Scanning. The new log profile will appear when processing is complete.";
            }

            lblFramesValue.Text = $"{runtime.CapturedPairs} / {runtime.ExpectedPairs}";
            UpdateRuntime(runtime);
        });
    }

    /// <summary>All frames are in; waiting for the PLC ScanComplete signal.</summary>
    public void ShowWaitingForScanComplete(OperatorRuntimeStatus runtime)
    {
        RunOnUiThread(() =>
        {
            SetStatus(
                ScanQualityStatus.Processing,
                "SCAN CAPTURED",
                "All camera frames are present. Waiting for PLC ScanComplete.");
            UpdateRuntime(runtime);
        });
    }

    /// <summary>Frames are being processed into the log profile.</summary>
    public void ShowProcessing(OperatorRuntimeStatus runtime)
    {
        RunOnUiThread(() =>
        {
            SetStatus(
                ScanQualityStatus.Processing,
                "BUILDING LOG PROFILE",
                "Processing the scan and checking profile coverage.");
            UpdateRuntime(runtime);
        });
    }

    /// <summary>
    /// Shows the scan-quality result: headline and colour, frames used, profile
    /// coverage, valid slices, length, average diameter (inches), largest gap,
    /// and the log profile drawing.
    /// </summary>
    public void ShowResult(
        OperatorScanQualityResult result,
        OperatorRuntimeStatus runtime)
    {
        ArgumentNullException.ThrowIfNull(result);

        RunOnUiThread(() =>
        {
            string headline = result.Status switch
            {
                ScanQualityStatus.Good => "SCAN GOOD",
                ScanQualityStatus.Warning => "CHECK SCAN",
                _ => "RESCAN BLOCK"
            };

            SetStatus(result.Status, headline, result.OperatorMessage);
            lblFramesValue.Text = $"{result.FramesUsed} / {result.ExpectedFrames}";
            lblCoverageValue.Text = $"{result.ProfileCoveragePercent:F1}%";
            lblValidSlicesValue.Text = $"{result.ValidSlicePercent:F1}%";
            lblLengthValue.Text = $"{result.LogLengthInches:F1} in";
            lblDiameterValue.Text = $"{result.AverageDiameterInches:F1} in";
            lblGapValue.Text = $"{result.LargestMissingGapInches:F2} in";
            lblDiameterRange.Text =
                $"Diameter range: {result.MinimumDiameterInches:F1}–{result.MaximumDiameterInches:F1} in";
            lblDetail.Text = result.DetailMessage;
            lblProcessingTime.Text = result.ProcessingSeconds > 0
                ? $"Profile processing: {result.ProcessingSeconds:F2} s"
                : "Profile processing complete";
            logProfileControl.SetProfile(result.ProfilePoints);
            _previousBlockOnScreen = true;
            UpdateRuntime(runtime);
        });
    }

    /// <summary>Processing failed: red "SCAN FAILED" with the message; the block must be rescanned.</summary>
    public void ShowFailure(string message, OperatorRuntimeStatus runtime)
    {
        RunOnUiThread(() =>
        {
            SetStatus(
                ScanQualityStatus.Failed,
                "SCAN FAILED",
                string.IsNullOrWhiteSpace(message)
                    ? "The scan could not be processed. Rescan the block."
                    : message);
            lblDetail.Text = "Do not use the current optimization result until a complete scan is obtained.";
            _previousBlockOnScreen = true;
            UpdateRuntime(runtime);
        });
    }

    /// <summary>Refreshes the PLC, ScanStart/ScanComplete, calibration and system-ready indicators.</summary>
    public void UpdateRuntime(OperatorRuntimeStatus runtime)
    {
        RunOnUiThread(() =>
        {
            lblPlcConnection.Text = runtime.PlcEnabled
                ? runtime.PlcConnected ? "PLC: CONNECTED" : "PLC: DISCONNECTED"
                : "PLC: DISABLED";
            lblPlcConnection.ForeColor = runtime.PlcConnected
                ? Color.DarkGreen
                : runtime.PlcEnabled ? Color.Firebrick : Color.DimGray;
            lblScanStart.Text = $"ScanStart: {(runtime.ScanStart ? "ON" : "OFF")}";
            lblScanComplete.Text = $"ScanComplete: {(runtime.ScanComplete ? "ON" : "OFF")}";

            // Update calibration status
            lblCalibrationStatus.Text = runtime.CalibrationActive
                ? "CALIBRATION: ACTIVE"
                : "CALIBRATION: NOT ACTIVE";
            lblCalibrationStatus.BackColor = runtime.CalibrationActive
                ? Color.DarkGreen
                : Color.Firebrick;

            // Update system ready status
            lblSystemReadyStatus.Text = runtime.SystemReady
                ? "SYSTEM: READY"
                : "SYSTEM: NOT READY";
            lblSystemReadyStatus.BackColor = runtime.SystemReady
                ? Color.DarkGreen
                : Color.Firebrick;
        });
    }

    /// <summary>Closes the window when the program exits (the only way it can close).</summary>
    public void CloseForApplicationExit()
    {
        _allowClose = true;
        Close();
    }

    /// <summary>Cancels a close by the user (X button / Alt+F4) unless the program is exiting.</summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    /// <summary>Sets the coloured status bar, headline and message.</summary>
    private void SetStatus(
        ScanQualityStatus status,
        string headline,
        string message)
    {
        statusPanel.BackColor = status switch
        {
            ScanQualityStatus.Good => Color.ForestGreen,
            ScanQualityStatus.Warning => Color.DarkOrange,
            ScanQualityStatus.Failed => Color.Firebrick,
            ScanQualityStatus.Capturing => Color.RoyalBlue,
            ScanQualityStatus.Processing => Color.SteelBlue,
            _ => Color.DimGray
        };

        lblScanStatus.Text = headline;
        lblScanMessage.Text = message;
    }

    /// <summary>Resets every result value to "--".</summary>
    private void ClearResultValues()
    {
        lblFramesValue.Text = "--";
        lblCoverageValue.Text = "--";
        lblValidSlicesValue.Text = "--";
        lblLengthValue.Text = "--";
        lblDiameterValue.Text = "--";
        lblGapValue.Text = "--";
        lblDiameterRange.Text = "Diameter range: --";
        lblDetail.Text = "The profile will appear automatically after each completed scan.";
        lblProcessingTime.Text = string.Empty;
    }

    /// <summary>
    /// Runs the action on the UI thread (queued with BeginInvoke when called from
    /// another thread). Does nothing once the window is disposed.
    /// </summary>
    private void RunOnUiThread(Action action)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
                // The application is closing.
            }
            return;
        }

        action();
    }
}
