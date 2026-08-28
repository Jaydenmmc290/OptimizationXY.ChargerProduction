using OptimizationXY.Charger.Models.Operator;

namespace OptimizationXY.Charger.UI.Forms;

/// <summary>
/// Read-only lathe-operator display. The form is updated by the main production
/// workflow and intentionally contains no camera/live-view controls.
/// </summary>
public partial class OperatorScanQualityForm : Form
{
    private bool _allowClose;

    public OperatorScanQualityForm()
    {
        InitializeComponent();
        ShowWaiting(new OperatorRuntimeStatus());
    }

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

    public void ShowCapturing(OperatorRuntimeStatus runtime)
    {
        RunOnUiThread(() =>
        {
            SetStatus(
                ScanQualityStatus.Capturing,
                "SCANNING BLOCK",
                $"Capturing rotating profile: {runtime.CapturedPairs} / {runtime.ExpectedPairs} frame pairs.");
            lblFramesValue.Text = $"{runtime.CapturedPairs} / {runtime.ExpectedPairs}";
            UpdateRuntime(runtime);
        });
    }

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
            UpdateRuntime(runtime);
        });
    }

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
            UpdateRuntime(runtime);
        });
    }

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

    public void CloseForApplicationExit()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

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
