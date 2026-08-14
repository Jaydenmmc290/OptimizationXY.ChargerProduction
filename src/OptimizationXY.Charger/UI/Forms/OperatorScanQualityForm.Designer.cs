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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        mainLayout = new TableLayoutPanel();
        statusPanel = new Panel();
        lblScanStatus = new Label();
        lblScanMessage = new Label();
        logProfileControl = new LogProfileControl();
        lblFramesValue = new Label();
        lblCoverageValue = new Label();
        lblValidSlicesValue = new Label();
        lblLengthValue = new Label();
        lblDiameterValue = new Label();
        lblGapValue = new Label();
        lblDiameterRange = new Label();
        lblDetail = new Label();
        lblProcessingTime = new Label();
        lblPlcConnection = new Label();
        lblScanStart = new Label();
        lblScanComplete = new Label();

        SuspendLayout();

        mainLayout.Dock = DockStyle.Fill;
        mainLayout.Padding = new Padding(18);
        mainLayout.ColumnCount = 1;
        mainLayout.RowCount = 5;
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 125F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 175F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

        statusPanel.Dock = DockStyle.Fill;
        statusPanel.Margin = new Padding(0, 0, 0, 12);
        statusPanel.Padding = new Padding(8);
        var statusLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
        statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
        lblScanStatus.Dock = DockStyle.Fill;
        lblScanStatus.Font = new Font("Segoe UI", 31F, FontStyle.Bold);
        lblScanStatus.ForeColor = Color.White;
        lblScanStatus.TextAlign = ContentAlignment.MiddleCenter;
        lblScanMessage.Dock = DockStyle.Fill;
        lblScanMessage.Font = new Font("Segoe UI", 15F, FontStyle.Regular);
        lblScanMessage.ForeColor = Color.White;
        lblScanMessage.TextAlign = ContentAlignment.MiddleCenter;
        statusLayout.Controls.Add(lblScanStatus, 0, 0);
        statusLayout.Controls.Add(lblScanMessage, 0, 1);
        statusPanel.Controls.Add(statusLayout);
        mainLayout.Controls.Add(statusPanel, 0, 0);

        var profilePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 12)
        };
        var profileLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        profileLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        profileLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        var orientation = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        orientation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        orientation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        orientation.Controls.Add(NewOrientationLabel("HEAD / DOG", ContentAlignment.MiddleLeft), 0, 0);
        orientation.Controls.Add(NewOrientationLabel("TAIL", ContentAlignment.MiddleRight), 1, 0);
        profileLayout.Controls.Add(orientation, 0, 0);
        profileLayout.Controls.Add(logProfileControl, 0, 1);
        profilePanel.Controls.Add(profileLayout);
        mainLayout.Controls.Add(profilePanel, 0, 1);

        var metrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 10)
        };
        metrics.ColumnStyles.Add(
    new ColumnStyle(SizeType.Percent, 33.333F));

metrics.ColumnStyles.Add(
    new ColumnStyle(SizeType.Percent, 33.333F));

metrics.ColumnStyles.Add(
    new ColumnStyle(SizeType.Percent, 33.334F));
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        metrics.Controls.Add(NewMetricCard("FRAMES USED", lblFramesValue), 0, 0);
        metrics.Controls.Add(NewMetricCard("PROFILE COVERAGE", lblCoverageValue), 1, 0);
        metrics.Controls.Add(NewMetricCard("VALID PROFILE", lblValidSlicesValue), 2, 0);
        metrics.Controls.Add(NewMetricCard("BLOCK LENGTH", lblLengthValue), 0, 1);
        metrics.Controls.Add(NewMetricCard("AVERAGE DIAMETER", lblDiameterValue), 1, 1);
        metrics.Controls.Add(NewMetricCard("LARGEST MISSING GAP", lblGapValue), 2, 1);
        mainLayout.Controls.Add(metrics, 0, 2);

        var details = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2
        };
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 67F));
        details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
        lblDiameterRange.Dock = DockStyle.Fill;
        lblDiameterRange.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        lblDiameterRange.TextAlign = ContentAlignment.MiddleLeft;
        lblDetail.Dock = DockStyle.Fill;
        lblDetail.Font = new Font("Segoe UI", 10.5F);
        lblDetail.ForeColor = Color.DimGray;
        lblDetail.TextAlign = ContentAlignment.TopLeft;
        lblProcessingTime.Dock = DockStyle.Fill;
        lblProcessingTime.Font = new Font("Segoe UI", 10F);
        lblProcessingTime.TextAlign = ContentAlignment.MiddleRight;
        details.Controls.Add(lblDiameterRange, 0, 0);
        details.Controls.Add(lblProcessingTime, 1, 0);
        details.Controls.Add(lblDetail, 0, 1);
        details.SetColumnSpan(lblDetail, 2);
        mainLayout.Controls.Add(details, 0, 3);

        var plcBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.FromArgb(242, 242, 242),
            Padding = new Padding(8, 0, 8, 0)
        };
        plcBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        plcBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        plcBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
        ConfigureRuntimeLabel(lblPlcConnection, ContentAlignment.MiddleLeft);
        ConfigureRuntimeLabel(lblScanStart, ContentAlignment.MiddleCenter);
        ConfigureRuntimeLabel(lblScanComplete, ContentAlignment.MiddleRight);
        plcBar.Controls.Add(lblPlcConnection, 0, 0);
        plcBar.Controls.Add(lblScanStart, 1, 0);
        plcBar.Controls.Add(lblScanComplete, 2, 0);
        mainLayout.Controls.Add(plcBar, 0, 4);

        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.White;
        ClientSize = new Size(1280, 800);
        Controls.Add(mainLayout);

        ControlBox = true;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = true;
        MinimizeBox = true;

        MinimumSize = new Size(1024, 700);
        Name = "OperatorScanQualityForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "XY - Lathe Operator Scan Check";

        ResumeLayout(false);
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
}
