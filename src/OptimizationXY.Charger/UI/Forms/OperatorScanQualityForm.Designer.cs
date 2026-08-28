using OptimizationXY.Charger.UI.Controls;

namespace OptimizationXY.Charger.UI.Forms;

partial class OperatorScanQualityForm
{
    private System.ComponentModel.IContainer components;
    private TableLayoutPanel mainLayout = null!;
    private Panel statusPanel = null!;
    private Label lblScanStatus = null!;
    private Label lblScanMessage = null!;
    private LogProfileControl logProfileControl = null!;
    private Label lblFramesValue = null!;
    private Label lblCoverageValue = null!;
    private Label lblValidSlicesValue = null!;
    private Label lblLengthValue = null!;
    private Label lblDiameterValue = null!;
    private Label lblGapValue = null!;
    private Label lblDiameterRange = null!;
    private Label lblDetail = null!;
    private Label lblProcessingTime = null!;
    private Label lblPlcConnection = null!;
    private Label lblScanStart = null!;
    private Label lblScanComplete = null!;
    private Label lblCalibrationStatus = null!;
    private Label lblSystemReadyStatus = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        mainLayout = new TableLayoutPanel();
        statusPanel = new Panel();
        statusLayout = new TableLayoutPanel();
        lblScanStatus = new Label();
        lblScanMessage = new Label();
        profilePanel = new Panel();
        profileLayout = new TableLayoutPanel();
        orientation = new TableLayoutPanel();
        logProfileControl = new LogProfileControl();
        metrics = new TableLayoutPanel();
        details = new TableLayoutPanel();
        lblDiameterRange = new Label();
        lblProcessingTime = new Label();
        lblDetail = new Label();
        plcBar = new TableLayoutPanel();
        lblPlcConnection = new Label();
        lblScanStart = new Label();
        lblScanComplete = new Label();
        lblCalibrationStatus = new Label();
        lblSystemReadyStatus = new Label();
        lblFramesValue = new Label();
        lblCoverageValue = new Label();
        lblValidSlicesValue = new Label();
        lblLengthValue = new Label();
        lblDiameterValue = new Label();
        lblGapValue = new Label();
        mainLayout.SuspendLayout();
        statusPanel.SuspendLayout();
        statusLayout.SuspendLayout();
        profilePanel.SuspendLayout();
        profileLayout.SuspendLayout();
        details.SuspendLayout();
        plcBar.SuspendLayout();
        SuspendLayout();
        // 
        // mainLayout
        // 
        mainLayout.AutoSize = false;
        mainLayout.ColumnCount = 1;
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainLayout.Controls.Add(statusPanel, 0, 0);
        mainLayout.Controls.Add(profilePanel, 0, 1);
        mainLayout.Controls.Add(metrics, 0, 2);
        mainLayout.Controls.Add(details, 0, 3);
        mainLayout.Controls.Add(plcBar, 0, 4);
        var systemStatusBar = new TableLayoutPanel();
        systemStatusBar.AutoSize = false;
        systemStatusBar.ColumnCount = 2;
        systemStatusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        systemStatusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        systemStatusBar.Controls.Add(lblCalibrationStatus, 0, 0);
        systemStatusBar.Controls.Add(lblSystemReadyStatus, 1, 0);
        systemStatusBar.Dock = DockStyle.Fill;
        systemStatusBar.Location = new Point(21, 715);
        systemStatusBar.Name = "systemStatusBar";
        systemStatusBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        systemStatusBar.Size = new Size(1238, 48);
        systemStatusBar.TabIndex = 5;
        mainLayout.Controls.Add(systemStatusBar, 0, 5);
        mainLayout.Dock = DockStyle.Fill;
        mainLayout.Location = new Point(0, 0);
        mainLayout.Name = "mainLayout";
        mainLayout.Padding = new Padding(18);
        mainLayout.RowCount = 6;
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 125F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 175F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        mainLayout.Size = new Size(1280, 800);
        mainLayout.TabIndex = 0;
        // 
        // statusPanel
        // 
        statusPanel.AutoSize = false;
        statusPanel.Controls.Add(statusLayout);
        statusPanel.Dock = DockStyle.Fill;
        statusPanel.Location = new Point(18, 18);
        statusPanel.Margin = new Padding(0, 0, 0, 12);
        statusPanel.Name = "statusPanel";
        statusPanel.Padding = new Padding(8);
        statusPanel.Size = new Size(1244, 113);
        statusPanel.TabIndex = 0;
        // 
        // statusLayout
        // 
        statusLayout.AutoSize = false;
        statusLayout.ColumnCount = 1;
        statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        statusLayout.Controls.Add(lblScanStatus, 0, 0);
        statusLayout.Controls.Add(lblScanMessage, 0, 1);
        statusLayout.Dock = DockStyle.Fill;
        statusLayout.Location = new Point(8, 8);
        statusLayout.Name = "statusLayout";
        statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
        statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
        statusLayout.Size = new Size(1228, 97);
        statusLayout.TabIndex = 0;
        // 
        // lblScanStatus
        // 
        lblScanStatus.Dock = DockStyle.Fill;
        lblScanStatus.Font = new Font("Segoe UI", 31F, FontStyle.Bold);
        lblScanStatus.ForeColor = Color.White;
        lblScanStatus.Location = new Point(3, 0);
        lblScanStatus.Name = "lblScanStatus";
        lblScanStatus.Size = new Size(194, 58);
        lblScanStatus.TabIndex = 0;
        lblScanStatus.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // lblScanMessage
        // 
        lblScanMessage.Dock = DockStyle.Fill;
        lblScanMessage.Font = new Font("Segoe UI", 15F);
        lblScanMessage.ForeColor = Color.White;
        lblScanMessage.Location = new Point(3, 58);
        lblScanMessage.Name = "lblScanMessage";
        lblScanMessage.Size = new Size(194, 42);
        lblScanMessage.TabIndex = 1;
        lblScanMessage.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // profilePanel
        // 
        profilePanel.AutoSize = false;
        profilePanel.Controls.Add(profileLayout);
        profilePanel.Dock = DockStyle.Fill;
        profilePanel.Location = new Point(21, 155);
        profilePanel.Name = "profilePanel";
        profilePanel.Size = new Size(1238, 251);
        profilePanel.TabIndex = 1;
        // 
        // profileLayout
        // 
        profileLayout.AutoSize = false;
        profileLayout.ColumnCount = 1;
        profileLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        profileLayout.Controls.Add(orientation, 0, 0);
        profileLayout.Controls.Add(logProfileControl, 0, 1);
        profileLayout.Dock = DockStyle.Fill;
        profileLayout.Location = new Point(0, 0);
        profileLayout.Name = "profileLayout";
        profileLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        profileLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        profileLayout.Size = new Size(1238, 251);
        profileLayout.TabIndex = 0;
        // 
        // orientation
        // 
        orientation.AutoSize = false;
        orientation.ColumnCount = 2;
        orientation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        orientation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        orientation.Dock = DockStyle.Fill;
        orientation.Location = new Point(3, 3);
        orientation.Name = "orientation";
        orientation.RowCount = 1;
        orientation.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        orientation.Size = new Size(1232, 30);
        orientation.TabIndex = 0;
        // Add orientation labels (HEAD/DOG on left, TAIL on right)
        orientation.Controls.Add(NewOrientationLabel("HEAD / DOG", ContentAlignment.MiddleLeft), 0, 0);
        orientation.Controls.Add(NewOrientationLabel("TAIL", ContentAlignment.MiddleRight), 1, 0);
        // 
        // logProfileControl
        // 
        logProfileControl.BackColor = Color.White;
        logProfileControl.Dock = DockStyle.Fill;
        logProfileControl.Location = new Point(3, 39);
        logProfileControl.Name = "logProfileControl";
        logProfileControl.Size = new Size(1232, 209);
        logProfileControl.TabIndex = 1;
        // 
        // metrics
        // 
        metrics.AutoSize = false;
        metrics.ColumnCount = 3;
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));
        metrics.Dock = DockStyle.Fill;
        metrics.Location = new Point(21, 406);
        metrics.Name = "metrics";
        metrics.RowCount = 2;
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        metrics.Size = new Size(1238, 175);
        metrics.TabIndex = 2;
        // Add metric cards
        metrics.Controls.Add(NewMetricCard("FRAMES USED", lblFramesValue), 0, 0);
        metrics.Controls.Add(NewMetricCard("PROFILE COVERAGE", lblCoverageValue), 1, 0);
        metrics.Controls.Add(NewMetricCard("VALID PROFILE", lblValidSlicesValue), 2, 0);
        metrics.Controls.Add(NewMetricCard("BLOCK LENGTH", lblLengthValue), 0, 1);
        metrics.Controls.Add(NewMetricCard("AVERAGE DIAMETER", lblDiameterValue), 1, 1);
        metrics.Controls.Add(NewMetricCard("LARGEST MISSING GAP", lblGapValue), 2, 1);
        // 
        // details
        // 
        details.AutoSize = false;
        details.ColumnCount = 2;
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 67F));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
        details.Controls.Add(lblDiameterRange, 0, 0);
        details.Controls.Add(lblProcessingTime, 1, 0);
        details.Controls.Add(lblDetail, 0, 1);
        details.Dock = DockStyle.Fill;
        details.Location = new Point(21, 581);
        details.Name = "details";
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        details.Size = new Size(1238, 78);
        details.TabIndex = 3;
        // 
        // lblDiameterRange
        // 
        lblDiameterRange.Dock = DockStyle.Fill;
        lblDiameterRange.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        lblDiameterRange.Location = new Point(3, 0);
        lblDiameterRange.Name = "lblDiameterRange";
        lblDiameterRange.Size = new Size(128, 20);
        lblDiameterRange.TabIndex = 0;
        lblDiameterRange.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // lblProcessingTime
        // 
        lblProcessingTime.Dock = DockStyle.Fill;
        lblProcessingTime.Font = new Font("Segoe UI", 10F);
        lblProcessingTime.Location = new Point(137, 0);
        lblProcessingTime.Name = "lblProcessingTime";
        lblProcessingTime.Size = new Size(60, 20);
        lblProcessingTime.TabIndex = 1;
        lblProcessingTime.TextAlign = ContentAlignment.MiddleRight;
        // 
        // lblDetail
        // 
        details.SetColumnSpan(lblDetail, 2);
        lblDetail.Dock = DockStyle.Fill;
        lblDetail.Font = new Font("Segoe UI", 10.5F);
        lblDetail.ForeColor = Color.DimGray;
        lblDetail.Location = new Point(3, 20);
        lblDetail.Name = "lblDetail";
        lblDetail.Size = new Size(194, 52);
        lblDetail.TabIndex = 2;
        // 
        // plcBar
        // 
        plcBar.AutoSize = false;
        plcBar.ColumnCount = 3;
        plcBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        plcBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        plcBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        plcBar.Controls.Add(lblPlcConnection, 0, 0);
        plcBar.Controls.Add(lblScanStart, 1, 0);
        plcBar.Controls.Add(lblScanComplete, 2, 0);
        plcBar.Dock = DockStyle.Fill;
        plcBar.Location = new Point(21, 659);
        plcBar.Name = "plcBar";
        plcBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        plcBar.Size = new Size(1238, 48);
        plcBar.TabIndex = 4;
        // 
        // lblPlcConnection
        // 
        lblPlcConnection.Dock = DockStyle.Fill;
        lblPlcConnection.Location = new Point(3, 0);
        lblPlcConnection.Name = "lblPlcConnection";
        lblPlcConnection.Size = new Size(489, 48);
        lblPlcConnection.TabIndex = 0;
        lblPlcConnection.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // lblScanStart
        // 
        lblScanStart.Dock = DockStyle.Fill;
        lblScanStart.Location = new Point(495, 0);
        lblScanStart.Name = "lblScanStart";
        lblScanStart.Size = new Size(365, 48);
        lblScanStart.TabIndex = 1;
        lblScanStart.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // lblScanComplete
        // 
        lblScanComplete.Dock = DockStyle.Fill;
        lblScanComplete.Location = new Point(863, 0);
        lblScanComplete.Name = "lblScanComplete";
        lblScanComplete.Size = new Size(372, 48);
        lblScanComplete.TabIndex = 2;
        lblScanComplete.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // lblCalibrationStatus
        // 
        lblCalibrationStatus.AutoSize = false;
        lblCalibrationStatus.BackColor = Color.Firebrick;
        lblCalibrationStatus.Dock = DockStyle.Fill;
        lblCalibrationStatus.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        lblCalibrationStatus.ForeColor = Color.White;
        lblCalibrationStatus.Location = new Point(3, 0);
        lblCalibrationStatus.Name = "lblCalibrationStatus";
        lblCalibrationStatus.Size = new Size(613, 48);
        lblCalibrationStatus.TabIndex = 0;
        lblCalibrationStatus.Text = "CALIBRATION: NOT ACTIVE";
        lblCalibrationStatus.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // lblSystemReadyStatus
        // 
        lblSystemReadyStatus.AutoSize = false;
        lblSystemReadyStatus.BackColor = Color.Firebrick;
        lblSystemReadyStatus.Dock = DockStyle.Fill;
        lblSystemReadyStatus.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        lblSystemReadyStatus.ForeColor = Color.White;
        lblSystemReadyStatus.Location = new Point(622, 0);
        lblSystemReadyStatus.Name = "lblSystemReadyStatus";
        lblSystemReadyStatus.Size = new Size(613, 48);
        lblSystemReadyStatus.TabIndex = 1;
        lblSystemReadyStatus.Text = "SYSTEM: NOT READY";
        lblSystemReadyStatus.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // lblFramesValue
        // 
        lblFramesValue.Location = new Point(0, 0);
        lblFramesValue.Name = "lblFramesValue";
        lblFramesValue.Size = new Size(100, 23);
        lblFramesValue.TabIndex = 0;
        // 
        // lblCoverageValue
        // 
        lblCoverageValue.Location = new Point(0, 0);
        lblCoverageValue.Name = "lblCoverageValue";
        lblCoverageValue.Size = new Size(100, 23);
        lblCoverageValue.TabIndex = 0;
        // 
        // lblValidSlicesValue
        // 
        lblValidSlicesValue.Location = new Point(0, 0);
        lblValidSlicesValue.Name = "lblValidSlicesValue";
        lblValidSlicesValue.Size = new Size(100, 23);
        lblValidSlicesValue.TabIndex = 0;
        // 
        // lblLengthValue
        // 
        lblLengthValue.Location = new Point(0, 0);
        lblLengthValue.Name = "lblLengthValue";
        lblLengthValue.Size = new Size(100, 23);
        lblLengthValue.TabIndex = 0;
        // 
        // lblDiameterValue
        // 
        lblDiameterValue.Location = new Point(0, 0);
        lblDiameterValue.Name = "lblDiameterValue";
        lblDiameterValue.Size = new Size(100, 23);
        lblDiameterValue.TabIndex = 0;
        // 
        // lblGapValue
        // 
        lblGapValue.Location = new Point(0, 0);
        lblGapValue.Name = "lblGapValue";
        lblGapValue.Size = new Size(100, 23);
        lblGapValue.TabIndex = 0;
        // 
        // OperatorScanQualityForm
        // 
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.White;
        ClientSize = new Size(1280, 800);
        Controls.Add(mainLayout);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(1024, 700);
        Name = "OperatorScanQualityForm";
        StartPosition = FormStartPosition.Manual;
        Text = "XY - Lathe Operator Scan Check";
        mainLayout.ResumeLayout(false);
        mainLayout.PerformLayout();
        statusPanel.ResumeLayout(false);
        statusLayout.ResumeLayout(false);
        profilePanel.ResumeLayout(false);
        profileLayout.ResumeLayout(false);
        details.ResumeLayout(false);
        plcBar.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }

    private static Label NewOrientationLabel(string text, ContentAlignment alignment) =>
        new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            TextAlign = alignment
        };

    private static Panel NewMetricCard(string title, Label valueLabel)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(4),
            Padding = new Padding(8)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        }, 0, 0);
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Font = new Font("Segoe UI", 21F, FontStyle.Bold);
        valueLabel.TextAlign = ContentAlignment.MiddleCenter;
        layout.Controls.Add(valueLabel, 0, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private static void ConfigureRuntimeLabel(Label label, ContentAlignment alignment)
    {
        label.Dock = DockStyle.Fill;
        label.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        label.TextAlign = alignment;
    }

    private TableLayoutPanel statusLayout;
    private Panel profilePanel;
    private TableLayoutPanel profileLayout;
    private TableLayoutPanel orientation;
    private TableLayoutPanel metrics;
    private TableLayoutPanel details;
    private TableLayoutPanel plcBar;
}
