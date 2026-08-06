using Emgu.CV;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using OptimizationXY.Charger.Infrastructure;
using OptimizationXY.Charger.Models.Calibration;
using OptimizationXY.Charger.Models.Acquisition;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Calibration;
using OptimizationXY.Charger.Services.Configuration;
using OptimizationXY.Charger.Services.Core;

namespace OptimizationXY.Charger.UI.Forms;

/// <summary>
/// A Windows Forms wizard for guiding the user through the production setup and scanner calibration process.
/// </summary>
public sealed class ProductionSetupWizardForm : Form
{
    private readonly ProductionSetupContext _context;
    private readonly AppSettings _settings;
    private readonly SqlDatabaseSetupService _databaseSetup = new();
    private readonly List<CalibrationReferenceCapture> _references = [.. new List<CalibrationReferenceCapture>()];

    private byte[] _headCameraSnapshot = [];
    private byte[] _tailCameraSnapshot = [];
    private bool _laserVerifiedThisSession;

    private readonly TabControl _tabs = new();
    private readonly Label _overallStatus = new();
    private readonly TextBox _diagnostics = new();

    private readonly TextBox _connectionString = new();
    private readonly Label _databaseStatus = new();

    private readonly TextBox _headServer = new();
    private readonly TextBox _tailServer = new();
    private readonly NumericUpDown _headDeviceIndex = new();
    private readonly NumericUpDown _tailDeviceIndex = new();
    private readonly TextBox _headExpectedSerial = new();
    private readonly TextBox _tailExpectedSerial = new();
    private readonly ComboBox _headAcquisitionMode = new();
    private readonly ComboBox _tailAcquisitionMode = new();
    private readonly CheckBox _physicalPositionConfirmed = new();
    private readonly Label _cameraStatus = new();
    private readonly Label _laserStatus = new();

    private readonly NumericUpDown _knownLengthMM = new();
    private readonly NumericUpDown _startPixelX = new();
    private readonly NumericUpDown _endPixelX = new();
    private readonly NumericUpDown _lengthPixelsPerMM = new();
    private readonly Label _lengthStatus = new();
    private readonly PictureBox _lengthCalibrationImage = new();
    private int _lengthMarkClickCount;
    private bool _lengthScaleMeasuredThisSession;

    private readonly TextBox _operatorName = new();
    private readonly TextBox _referenceName = new();
    private readonly NumericUpDown _diameterMM = new();
    private readonly CheckBox _constantDiameterConfirmed = new();
    private readonly DataGridView _referenceGrid = new();
    private readonly Label _referenceStatus = new();

    private readonly CheckBox _independentValidationCompleted = new();
    private readonly NumericUpDown _validationMeanError = new();
    private readonly NumericUpDown _validationMaxError = new();
    private readonly TextBox _calibrationNotes = new();
    private readonly Label _activationStatus = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductionSetupWizardForm"/> class with the specified production setup context.
    /// </summary>
    /// <param name="context">The production setup context.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="context"/> is null.</exception>
    public ProductionSetupWizardForm(ProductionSetupContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _settings = PipelineState.Instance.Settings;

        Text = "Optimization XY Charger - Production Setup and Scanner Calibration";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1050, 720);
        Size = new Size(1180, 790);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;

        BuildLayout();
        LoadCurrentSettings();
        RefreshOverallStatus();
    }



    /// <summary>
    /// Handles the form's FormClosed event to dispose of the length calibration image and perform any necessary cleanup.
    /// </summary>
    /// <param name="e">The event data for the FormClosed event.</param>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Image? oldImage = _lengthCalibrationImage.Image;
        _lengthCalibrationImage.Image = null;
        oldImage?.Dispose();
        base.OnFormClosed(e);
    }


    /// <summary>
    /// Builds the layout of the production setup wizard form, including the header, tabs, diagnostics area,
    /// and footer with buttons for closing and opening folders.
    /// </summary>
    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Fill };
        header.Controls.Add(new Label
        {
            Text = "Production Setup / Calibration Wizard",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(0, 0)
        });
        _overallStatus.AutoSize = true;
        _overallStatus.Location = new Point(0, 28);
        header.Controls.Add(_overallStatus);
        root.Controls.Add(header, 0, 0);

        _tabs.Dock = DockStyle.Fill;
        _tabs.TabPages.Add(BuildOverviewPage());
        _tabs.TabPages.Add(BuildDatabasePage());
        _tabs.TabPages.Add(BuildCameraPage());
        _tabs.TabPages.Add(BuildLengthPage());
        _tabs.TabPages.Add(BuildReferencePage());
        _tabs.TabPages.Add(BuildActivationPage());
        root.Controls.Add(_tabs, 0, 1);

        _diagnostics.Dock = DockStyle.Fill;
        _diagnostics.Multiline = true;
        _diagnostics.ReadOnly = true;
        _diagnostics.ScrollBars = ScrollBars.Vertical;
        root.Controls.Add(_diagnostics, 0, 2);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var close = new Button
        {
            Text = "Close",
            AutoSize = true,
            Padding = new Padding(16, 4, 16, 4)
        };
        close.Click += (_, _) => Close();

        var openCalibration = new Button
        {
            Text = "Open Calibration Folder",
            AutoSize = true,
            Padding = new Padding(10, 4, 10, 4)
        };
        openCalibration.Click += (_, _) => OpenFolder(ApplicationPaths.CalibrationDirectory);

        var openData = new Button
        {
            Text = "Open Data Folder",
            AutoSize = true,
            Padding = new Padding(10, 4, 10, 4)
        };
        openData.Click += (_, _) => OpenFolder(ApplicationPaths.RootDirectory);

        footer.Controls.Add(close);
        footer.Controls.Add(openCalibration);
        footer.Controls.Add(openData);
        root.Controls.Add(footer, 0, 3);
    }

    /// <summary>
    /// Builds the "Overview" tab page of the production setup wizard, providing information about machine-specific files,
    /// </summary>
    /// <returns>The constructed "Overview" tab page.</returns>
    private static TabPage BuildOverviewPage()
    {
        var page = NewPage("1. Overview");
        var panel = NewVerticalPanel();
        page.Controls.Add(panel);

        panel.Controls.Add(NewHeading("Machine-specific files are never written to a development-computer path."));
        panel.Controls.Add(NewWrappedLabel(
            "The program resolves its runtime data root from OPTIMIZATION_XY_DATA_DIR. " +
            "When that variable is not set, it uses the current Windows user's LocalAppData folder. " +
            "The production installer can set the data root to D:\\OptimizationXYData or another approved writable drive."));

        panel.Controls.Add(NewPathLabel("Runtime data root", ApplicationPaths.RootDirectory));
        panel.Controls.Add(NewPathLabel("Settings", AppSettingsStore.SettingsPath));
        panel.Controls.Add(NewPathLabel("Active lookup table", ApplicationPaths.CalibrationLookupTablePath));
        panel.Controls.Add(NewPathLabel("Calibration metadata", ApplicationPaths.CalibrationMetadataPath));
        panel.Controls.Add(NewPathLabel("Saved scans", ApplicationPaths.SavedScansDirectory));

        panel.Controls.Add(NewWrappedLabel(
            "The setup form always leaves the machine in Commissioning mode. It does not certify physical safety, travel limits, PLC sign conventions, emergency stops, or knife clearance. Those approvals remain separate production release gates."));

        return page;
    }


    /// <summary>
    /// Builds the "Local SQL Express" tab page of the production setup wizard, providing
    /// controls for testing, creating, and saving the SQL Server Express database connection.
    /// </summary>
    /// <returns>The constructed "Local SQL Express" tab page.</returns>
    private TabPage BuildDatabasePage()
    {
        var page = NewPage("2. Local SQL Express");
        var panel = NewVerticalPanel();
        page.Controls.Add(panel);

        panel.Controls.Add(NewWrappedLabel(
            "SQL Server Express is expected on this computer. The wizard uses Windows Integrated Security, creates the OptimizationXY database if needed, creates or upgrades the schema, grants the current Windows login read/write access, tests the schema version, and then enables database saving."));

        panel.Controls.Add(NewLabeledControl("Connection string", _connectionString, 900));

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        var test = new Button { Text = "Test SQL Server", AutoSize = true };
        test.Click += async (_, _) => await TestDatabaseAsync();
        var create = new Button { Text = "Create / Upgrade Database", AutoSize = true };
        create.Click += async (_, _) => await CreateDatabaseAsync();
        var save = new Button { Text = "Save and Enable SQL", AutoSize = true };
        save.Click += (_, _) => SaveDatabaseSettings();

        buttons.Controls.Add(test);
        buttons.Controls.Add(create);
        buttons.Controls.Add(save);
        panel.Controls.Add(buttons);

        _databaseStatus.AutoSize = true;
        _databaseStatus.MaximumSize = new Size(980, 0);
        panel.Controls.Add(_databaseStatus);

        return page;
    }

    /// <summary>
    /// Builds the "Cameras and Laser" tab page of the production setup wizard, providing controls for configuring
    /// </summary>
    /// <returns>The constructed "Cameras and Laser" tab page.</returns>
    private TabPage BuildCameraPage()
    {
        var page = NewPage("3. Cameras and Laser");
        var panel = NewVerticalPanel();
        page.Controls.Add(panel);

        panel.Controls.Add(NewWrappedLabel(
            "Connect each production camera directly through Sapera. The program does not load a CCF file " +
            "and does not change exposure, gain, pixel format, frame rate, image geometry, camera user sets. " +
            "Frame Rate mode disables external triggering by setting TriggerMode to Off. Encoder mode +" +
            "enables FrameStart triggering from Line1 using a rising edge. When Encoder mode is selected, the preview will" +
            "not update until encoder trigger pulses are received.\" "));

        ConfigureNumeric(_headDeviceIndex, 0M, 32M, 0, 0M);
        ConfigureNumeric(_tailDeviceIndex, 0M, 32M, 0, 0M);
        ConfigureModeCombo(_headAcquisitionMode);
        ConfigureModeCombo(_tailAcquisitionMode);

        panel.Controls.Add(NewHeading("Head / Dog Camera"));
        panel.Controls.Add(NewLabeledControl("Sapera server", _headServer, 500));
        panel.Controls.Add(NewLabeledControl("Device index", _headDeviceIndex, 160));
        panel.Controls.Add(NewLabeledControl("Expected serial (blank to learn)", _headExpectedSerial, 360));
        panel.Controls.Add(NewLabeledControl("Acquisition mode", _headAcquisitionMode, 240));

        panel.Controls.Add(NewHeading("Tail Camera"));
        panel.Controls.Add(NewLabeledControl("Sapera server", _tailServer, 500));
        panel.Controls.Add(NewLabeledControl("Device index", _tailDeviceIndex, 160));
        panel.Controls.Add(NewLabeledControl("Expected serial (blank to learn)", _tailExpectedSerial, 360));
        panel.Controls.Add(NewLabeledControl("Acquisition mode", _tailAcquisitionMode, 240));

        _physicalPositionConfirmed.Text =
            "I verified that the head preview shows the head/dog side and the tail preview shows the tail side.";
        _physicalPositionConfirmed.AutoSize = true;
        _physicalPositionConfirmed.CheckedChanged += (_, _) =>
        {
            _settings.Setup.CameraPhysicalPositionsConfirmed =
                _physicalPositionConfirmed.Checked;
            SaveSettings();
            RefreshOverallStatus();
        };
        panel.Controls.Add(_physicalPositionConfirmed);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        var reconnect = new Button
        {
            Text = "Reconnect Cameras",
            AutoSize = true
        };
        reconnect.Click += async (_, _) => await SaveAndReconnectCamerasAsync();

        var showHead = new Button
        {
            Text = "Show Head Preview",
            AutoSize = true
        };
        showHead.Click += (_, _) => ShowCameraPreview(_context.ShowHeadPreview, "head");

        var showTail = new Button
        {
            Text = "Show Tail Preview",
            AutoSize = true
        };
        showTail.Click += (_, _) => ShowCameraPreview(_context.ShowTailPreview, "tail");

        var verifyLaser = new Button
        {
            Text = "Verify Laser",
            AutoSize = true
        };
        verifyLaser.Click += async (_, _) => await VerifyLaserAsync();

        buttons.Controls.Add(reconnect);
        buttons.Controls.Add(showHead);
        buttons.Controls.Add(showTail);
        buttons.Controls.Add(verifyLaser);
        panel.Controls.Add(buttons);

        panel.Controls.Add(NewHeading("Status"));
        _cameraStatus.AutoSize = true;
        _cameraStatus.MaximumSize = new Size(980, 0);
        panel.Controls.Add(_cameraStatus);

        _laserStatus.AutoSize = true;
        _laserStatus.MaximumSize = new Size(980, 0);
        panel.Controls.Add(_laserStatus);

        return page;
    }


    /// <summary>
    /// Builds the "Axial Length Scale" tab page of the production setup wizard, providing controls for capturing an image,
    /// </summary>
    /// <returns></returns>
    private TabPage BuildLengthPage()
    {
        var page = NewPage("4. Axial Length Scale");
        var panel = NewVerticalPanel();
        page.Controls.Add(panel);

        panel.Controls.Add(NewWrappedLabel(
            "LengthPixelsPerMM converts image X distance along the block into millimeters. " +
            "Place two visible marks a precisely known physical distance apart. Capture one " +
            "concatenated image, click the first mark and then the second mark, enter the known " +
            "distance, and let the wizard calculate the scale. This is separate from the radial lookup table."));

        ConfigureNumeric(_knownLengthMM, 0.001M, 100000M, 3, 500M);
        ConfigureNumeric(_startPixelX, 0M, 100000M, 0, 0M);
        ConfigureNumeric(_endPixelX, 0M, 100000M, 0, 500M);
        ConfigureNumeric(_lengthPixelsPerMM, 0.000001M, 10000M, 6, 0.539300M);

        var imageButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        var captureImage = new Button
        {
            Text = "Capture Image for Mark Clicking",
            AutoSize = true
        };
        captureImage.Click += async (_, _) => await CaptureLengthCalibrationImageAsync();
        var resetMarks = new Button
        {
            Text = "Reset Clicked Marks",
            AutoSize = true
        };
        resetMarks.Click += (_, _) => ResetLengthMarks();
        imageButtons.Controls.Add(captureImage);
        imageButtons.Controls.Add(resetMarks);
        panel.Controls.Add(imageButtons);

        _lengthCalibrationImage.Width = 940;
        _lengthCalibrationImage.Height = 270;
        _lengthCalibrationImage.BorderStyle = BorderStyle.FixedSingle;
        _lengthCalibrationImage.BackColor = Color.Black;
        _lengthCalibrationImage.SizeMode = PictureBoxSizeMode.Zoom;
        _lengthCalibrationImage.Cursor = Cursors.Cross;
        _lengthCalibrationImage.MouseClick += LengthCalibrationImage_MouseClick;
        panel.Controls.Add(_lengthCalibrationImage);

        panel.Controls.Add(NewLabeledControl("Known physical distance (mm)", _knownLengthMM, 220));
        panel.Controls.Add(NewLabeledControl("First mark X pixel", _startPixelX, 220));
        panel.Controls.Add(NewLabeledControl("Second mark X pixel", _endPixelX, 220));
        panel.Controls.Add(NewLabeledControl("LengthPixelsPerMM", _lengthPixelsPerMM, 220));

        var buttons = new FlowLayoutPanel { AutoSize = true };
        var calculate = new Button { Text = "Calculate", AutoSize = true };
        calculate.Click += (_, _) => CalculateLengthScale();
        var save = new Button { Text = "Save Length Scale", AutoSize = true };
        save.Click += (_, _) => SaveLengthScale();
        buttons.Controls.Add(calculate);
        buttons.Controls.Add(save);
        panel.Controls.Add(buttons);

        _lengthStatus.AutoSize = true;
        _lengthStatus.MaximumSize = new Size(980, 0);
        panel.Controls.Add(_lengthStatus);

        return page;
    }

    /// <summary>
    /// Builds the "Known References" tab page of the production setup wizard, providing controls for capturing and
    /// managing known reference blocks for scanner calibration.
    /// </summary>
    /// <returns>The constructed "Known References" tab page.</returns>
    private TabPage BuildReferencePage()
    {
        var page = NewPage("5. Known References");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        page.Controls.Add(root);

        var input = NewVerticalPanel();
        input.AutoScroll = false;
        input.Controls.Add(NewWrappedLabel(
            "Use a known round block or a carefully measured reference. Enter the measured diameter; radius is calculated automatically. The program captures synchronized head/tail frames, concatenates them using the production settings, detects the laser line, rejects unstable samples, averages each image column, and creates one physical radius band. At least two different diameters are required."));

        _operatorName.Width = 350;
        _referenceName.Width = 350;
        ConfigureNumeric(_diameterMM, 1M, 2000M, 3, 250M);

        var inputRows = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        inputRows.Controls.Add(NewInlineLabeledControl("Operator", _operatorName, 220));
        inputRows.Controls.Add(NewInlineLabeledControl("Reference name", _referenceName, 240));
        inputRows.Controls.Add(NewInlineLabeledControl("Measured diameter (mm)", _diameterMM, 160));
        input.Controls.Add(inputRows);

        _constantDiameterConfirmed.Text =
            "I measured this round reference in multiple directions and confirm the entered diameter applies across the scanned width.";
        _constantDiameterConfirmed.AutoSize = true;
        input.Controls.Add(_constantDiameterConfirmed);

        var capture = new Button
        {
            Text = "Capture 36 Pairs and Add Reference",
            AutoSize = true
        };
        capture.Click += async (_, _) => await CaptureReferenceAsync();
        input.Controls.Add(capture);
        root.Controls.Add(input, 0, 0);

        ConfigureReferenceGrid();
        root.Controls.Add(_referenceGrid, 0, 1);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight
        };
        var remove = new Button { Text = "Remove Selected Reference", AutoSize = true };
        remove.Click += (_, _) => RemoveSelectedReference();
        footer.Controls.Add(remove);
        _referenceStatus.AutoSize = true;
        _referenceStatus.Padding = new Padding(12, 7, 0, 0);
        footer.Controls.Add(_referenceStatus);
        root.Controls.Add(footer, 0, 2);

        return page;
    }

    /// <summary>
    /// Builds the "Review and Activate" tab page of the production setup wizard, providing controls for reviewing calibration settings,
    /// </summary>
    /// <returns></returns>
    private TabPage BuildActivationPage()
    {
        var page = NewPage("6. Review and Activate");
        var panel = NewVerticalPanel();
        page.Controls.Add(panel);

        panel.Controls.Add(NewWrappedLabel(
            "Activation writes a staged calibration package, reloads and validates it, verifies SHA-256 hashes, archives any previous active calibration, and then atomically replaces the active files. If activation fails, the newest valid archive is restored. With only two measured logs and no independent reference, the saved quality status remains CommissioningOnly."));

        _independentValidationCompleted.Text =
            "An independent known reference was measured and checked against this calibration.";
        _independentValidationCompleted.AutoSize = true;
        _independentValidationCompleted.CheckedChanged += (_, _) =>
        {
            _validationMeanError.Enabled = _independentValidationCompleted.Checked;
            _validationMaxError.Enabled = _independentValidationCompleted.Checked;
        };
        panel.Controls.Add(_independentValidationCompleted);

        ConfigureNumeric(_validationMeanError, 0M, 1000M, 3, 0M);
        ConfigureNumeric(_validationMaxError, 0M, 1000M, 3, 0M);
        _validationMeanError.Enabled = false;
        _validationMaxError.Enabled = false;
        panel.Controls.Add(NewLabeledControl("Independent validation mean error (mm)", _validationMeanError, 220));
        panel.Controls.Add(NewLabeledControl("Independent validation maximum error (mm)", _validationMaxError, 220));

        _calibrationNotes.Multiline = true;
        _calibrationNotes.Height = 90;
        panel.Controls.Add(NewLabeledControl("Calibration notes", _calibrationNotes, 900));

        var activate = new Button
        {
            Text = "Generate, Validate, Archive, and Activate Calibration",
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4)
        };
        activate.Click += (_, _) => ActivateCalibration();
        panel.Controls.Add(activate);

        _activationStatus.AutoSize = true;
        _activationStatus.MaximumSize = new Size(980, 0);
        panel.Controls.Add(_activationStatus);

        return page;
    }

    /// <summary>
    /// Configures a ComboBox to display the available acquisition modes from the AcquisitionMode enum.
    /// </summary>
    /// <param name="combo">The ComboBox to configure.</param>

    private static void ConfigureModeCombo(ComboBox combo)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.DataSource = Enum.GetValues<AcquisitionMode>();
    }


    /// <summary>
    /// Loads the current settings from the application settings into the form controls, including database connection string,
    /// </summary>
    private void LoadCurrentSettings()
    {
        _connectionString.Text = string.IsNullOrWhiteSpace(
            _settings.Database.ConnectionString)
            ? SqlDatabaseSetupService.DefaultConnectionString
            : _settings.Database.ConnectionString;

        CameraDeviceSettings headCamera = _settings.Acquisition.HeadCamera;
        CameraDeviceSettings tailCamera = _settings.Acquisition.TailCamera;
        _headServer.Text = headCamera.ServerName;
        _tailServer.Text = tailCamera.ServerName;
        _headDeviceIndex.Value = ClampDecimal(headCamera.DeviceIndex, _headDeviceIndex.Minimum, _headDeviceIndex.Maximum);
        _tailDeviceIndex.Value = ClampDecimal(tailCamera.DeviceIndex, _tailDeviceIndex.Minimum, _tailDeviceIndex.Maximum);
        _headExpectedSerial.Text = headCamera.ExpectedSerialNumber;
        _tailExpectedSerial.Text = tailCamera.ExpectedSerialNumber;
        _headAcquisitionMode.SelectedItem = headCamera.Mode;
        _tailAcquisitionMode.SelectedItem = tailCamera.Mode;
        _physicalPositionConfirmed.Checked =
            _settings.Setup.CameraPhysicalPositionsConfirmed;

        _operatorName.Text = string.IsNullOrWhiteSpace(
            _settings.Production.CommissioningApprovedBy)
            ? Environment.UserName
            : _settings.Production.CommissioningApprovedBy;

        if (double.IsFinite(_settings.Setup.PendingLengthPixelsPerMM) &&
            _settings.Setup.PendingLengthPixelsPerMM > 0)
        {
            _lengthPixelsPerMM.Value = ClampDecimal(
                _settings.Setup.PendingLengthPixelsPerMM,
                _lengthPixelsPerMM.Minimum,
                _lengthPixelsPerMM.Maximum);
            _lengthScaleMeasuredThisSession =
                _settings.Setup.LengthCalibrationConfigured;
        }

        try
        {
            ScannerLookupTable existing = LookupTableStore.Load();
            if (!_lengthScaleMeasuredThisSession)
            {
                _lengthPixelsPerMM.Value = ClampDecimal(
                    existing.LengthPixelsPerMM,
                    _lengthPixelsPerMM.Minimum,
                    _lengthPixelsPerMM.Maximum);
                _lengthScaleMeasuredThisSession =
                    _settings.Setup.LengthCalibrationConfigured;
            }

            _activationStatus.Text =
                $"Active calibration: {existing.CalibrationId} | " +
                $"image {existing.ImageWidth}x{existing.ImageHeight} | " +
                $"radius range {existing.MinimumCalibratedRadiusMM:F3} to {existing.MaximumCalibratedRadiusMM:F3} mm | " +
                $"status {existing.Quality.Status}.";
        }
        catch (Exception ex)
        {
            _activationStatus.Text = "No valid active scanner calibration: " + ex.Message;
        }

        RefreshCameraStatus();
        RefreshReferenceGrid();

    }

    /// <summary>
    /// Tests the SQL Server connection using the provided connection string and updates the database status label with the result.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task TestDatabaseAsync()
    {
        await RunBusyAsync("Testing SQL Server connection...", async () =>
        {
            DatabaseSetupResult result = await Task.Run(() =>
                SqlDatabaseSetupService.TestConnection(_connectionString.Text));

            _databaseStatus.Text =
                $"Connected to {result.ServerDescription}; SQL {result.SqlVersion}; " +
                $"Windows identity {result.LoginIdentity}; schema " +
                $"{result.SchemaVersion?.ToString(CultureInfo.InvariantCulture) ?? "not installed"}.";

        });
    }


    /// <summary>
    /// Creates or upgrades the local SQL database using the provided connection string, updates the database status label,
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task CreateDatabaseAsync()
    {
        await RunBusyAsync("Creating or upgrading the local SQL database...", async () =>
        {
            DatabaseSetupResult result = await Task.Run(() =>
                SqlDatabaseSetupService.CreateOrUpgrade(_connectionString.Text));

            _databaseStatus.Text =
                $"Database {result.DatabaseName} is ready on {result.ServerDescription}. " +
                $"Schema version {result.SchemaVersion}; Windows identity {result.LoginIdentity}.";

            _settings.Database.ConnectionString = _connectionString.Text.Trim();
            _settings.Database.EnableDatabaseSaving = true;
            _settings.Setup.DatabaseConfigured = true;
            SaveSettings();
            RefreshOverallStatus();
        });
    }

    /// <summary>
    /// Saves the database connection string and enables SQL saving in the application settings.
    /// It validates the schema version and updates the database status label accordingly.
    /// </summary>
    private void SaveDatabaseSettings()
    {
        try
        {
            int? version = SqlDatabaseSetupService.ValidateSchema(
                _connectionString.Text);

            _settings.Database.ConnectionString = _connectionString.Text.Trim();
            _settings.Database.EnableDatabaseSaving = true;
            _settings.Setup.DatabaseConfigured =
                version == SqlDatabaseSetupService.ExpectedSchemaVersion;
            SaveSettings();

            _databaseStatus.Text =
                $"SQL saving enabled. Verified schema version {version}.";
            Append("Database connection string saved using Windows Integrated Security.");
            RefreshOverallStatus();
        }
        catch (Exception ex)
        {
            ShowError("SQL settings were not saved", ex);
        }
    }

    /// <summary>
    /// Saves the camera connection settings and attempts to reconnect the cameras using the updated settings.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the head or tail camera server name is not provided.</exception>
    private async Task SaveAndReconnectCamerasAsync()
    {
        await RunBusyAsync(
            "Saving camera connection and applying the selected acquisition mode...",
            async () =>
            {
                CameraDeviceSettings head = _settings.Acquisition.HeadCamera;
                CameraDeviceSettings tail = _settings.Acquisition.TailCamera;

                string previousHeadServer = head.ServerName;
                string previousTailServer = tail.ServerName;
                int previousHeadIndex = head.DeviceIndex;
                int previousTailIndex = tail.DeviceIndex;
                string previousHeadSerial = head.ExpectedSerialNumber;
                string previousTailSerial = tail.ExpectedSerialNumber;

                string headServer = _headServer.Text.Trim();
                string tailServer = _tailServer.Text.Trim();
                if (string.IsNullOrWhiteSpace(headServer) ||
                    string.IsNullOrWhiteSpace(tailServer))
                {
                    throw new InvalidOperationException(
                        "Enter a Sapera server name for both the head and tail cameras.");
                }

                head.InitializationMode = CameraInitializationMode.Programmatic;
                tail.InitializationMode = CameraInitializationMode.Programmatic;
                head.ServerName = headServer;
                tail.ServerName = tailServer;
                head.DeviceIndex = decimal.ToInt32(_headDeviceIndex.Value);
                tail.DeviceIndex = decimal.ToInt32(_tailDeviceIndex.Value);
                head.ExpectedSerialNumber = _headExpectedSerial.Text.Trim();
                tail.ExpectedSerialNumber = _tailExpectedSerial.Text.Trim();
                head.Mode = _headAcquisitionMode.SelectedItem is AcquisitionMode headMode
                    ? headMode
                    : AcquisitionMode.Encoder;
                tail.Mode = _tailAcquisitionMode.SelectedItem is AcquisitionMode tailMode
                    ? tailMode
                    : AcquisitionMode.Encoder;
                head.TriggerSource = head.Mode == AcquisitionMode.Encoder
                    ? "Line1"
                    : "FixedRate";
                tail.TriggerSource = tail.Mode == AcquisitionMode.Encoder
                    ? "Line1"
                    : "FixedRate";

                bool connectionIdentityChanged =
                    !string.Equals(previousHeadServer, head.ServerName, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(previousTailServer, tail.ServerName, StringComparison.OrdinalIgnoreCase) ||
                    previousHeadIndex != head.DeviceIndex ||
                    previousTailIndex != tail.DeviceIndex ||
                    !string.Equals(previousHeadSerial, head.ExpectedSerialNumber, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(previousTailSerial, tail.ExpectedSerialNumber, StringComparison.OrdinalIgnoreCase);

                if (connectionIdentityChanged)
                    InvalidateCalibrationDependentSetup();

                SaveSettings();
                CameraHardwareStatus status = await _context.ReconnectCamerasAsync();

                if (status.HeadConnected &&
                    !string.IsNullOrWhiteSpace(status.HeadSerialNumber))
                {
                    head.ExpectedSerialNumber = status.HeadSerialNumber;
                    _headExpectedSerial.Text = status.HeadSerialNumber;
                }

                if (status.TailConnected &&
                    !string.IsNullOrWhiteSpace(status.TailSerialNumber))
                {
                    tail.ExpectedSerialNumber = status.TailSerialNumber;
                    _tailExpectedSerial.Text = status.TailSerialNumber;
                }

                _settings.Setup.CameraConfigurationVerified =
                    status.HeadConnected &&
                    status.TailConnected &&
                    status.HeadConfigurationLoaded &&
                    status.TailConfigurationLoaded;

                SaveSettings();
                RefreshCameraStatus(status);
                RefreshOverallStatus();

               Append(
                $"Camera reconnect complete. " +
                $"Head mode={head.Mode}; Tail mode={tail.Mode}. " +
                $"FrameRate uses TriggerMode=Off. " +
                $"Encoder uses TriggerMode=On and TriggerSource=Line1.");
            });
    }


    /// <summary>
    /// Verifies the laser detection by capturing calibration frames and analyzing them. It checks if the number of
    /// valid laser frames and average detected columns meet the commissioning threshold. If successful,
    /// it updates the camera configuration snapshots and marks the laser as verified for this session.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the laser verification does not meet the commissioning threshold.</exception>
    private async Task VerifyLaserAsync()
    {
        if (!_physicalPositionConfirmed.Checked)
        {
            MessageBox.Show(
                this,
                "Confirm the physical head/tail camera positions before running laser verification.",
                "Camera Position Confirmation Required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!ConfirmCalibrationSafety())
        {
            return;
        }

        await RunBusyAsync("Capturing camera and laser verification frames...", async () =>
        {
            using CalibrationCaptureBatch batch =
                await _context.CaptureCalibrationFramesAsync(
                    6,
                    CancellationToken.None);

            LaserDetectionSummary summary =
                ScannerCalibrationBuilder.AnalyzeLaser(batch);

            int minimumValidFrames = Math.Max(
                3,
                (int)Math.Ceiling(summary.CapturedFrameCount * 0.80));

            if (summary.ValidLaserFrameCount < minimumValidFrames ||
                summary.AverageDetectedColumns < 30)
            {
                throw new InvalidOperationException(
                    $"Laser verification did not meet the commissioning threshold. " +
                    $"Detected {summary.ValidLaserFrameCount}/{summary.CapturedFrameCount} usable frames " +
                    $"with {summary.AverageDetectedColumns:F0} average columns; required at least " +
                    $"{minimumValidFrames} frames and 30 columns.");
            }

            _headCameraSnapshot = [.. batch.HeadCameraConfigurationSnapshot];
            _tailCameraSnapshot = [.. batch.TailCameraConfigurationSnapshot];
            _laserVerifiedThisSession = true;

            _settings.Setup.CameraConfigurationVerified = true;
            _settings.Setup.CameraPhysicalPositionsConfirmed = true;
            _settings.Setup.LaserDetectionVerified = true;
            SaveSettings();

            _laserStatus.Text =
                $"Laser detected on {summary.ValidLaserFrameCount}/{summary.CapturedFrameCount} frames. " +
                $"Image {summary.ImageWidth}x{summary.ImageHeight}; average detected columns " +
                $"{summary.AverageDetectedColumns:F0}; Y range {summary.MinimumLaserY:F2} to {summary.MaximumLaserY:F2}.";

            Append(_laserStatus.Text);
            RefreshOverallStatus();
        });
    }

    /// <summary>
    /// Captures a concatenated image for axial length calibration by taking a single calibration frame. It normalizes the image, displays
    /// it in the UI, and prompts the user to click on the first and second physical marks for length measurement.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task CaptureLengthCalibrationImageAsync()
    {
        if (!ConfirmCalibrationSafety())
        {
            return;
        }

        await RunBusyAsync("Capturing a concatenated image for axial mark selection...", async () =>
        {
            using CalibrationCaptureBatch batch =
                await _context.CaptureCalibrationFramesAsync(
                    1,
                    CancellationToken.None);

            EnsureSameCameraSnapshot(batch);

            Mat source = batch.ConcatenatedFrames[0];
            using Mat normalized = new();
            CvInvoke.Normalize(
                source,
                normalized,
                0,
                255,
                Emgu.CV.CvEnum.NormType.MinMax,
                Emgu.CV.CvEnum.DepthType.Cv8U);

            Bitmap bitmap = normalized.ToBitmap();
            Image? oldImage = _lengthCalibrationImage.Image;
            _lengthCalibrationImage.Image = bitmap;
            oldImage?.Dispose();
            ResetLengthMarks(clearImage: false);

            _lengthStatus.Text =
                $"Captured {bitmap.Width} x {bitmap.Height}. Click the first physical mark, then the second physical mark.";
            Append(_lengthStatus.Text);
        });
    }


    /// <summary>
    /// Handles mouse click events on the length calibration image. It maps the clicked point to the image coordinates and updates the start and end pixel values for length measurement.
    /// After both marks are clicked, it calculates the length scale based on the known physical distance.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="MouseEventArgs"/> instance containing the event data.</param>
    private void LengthCalibrationImage_MouseClick(
        object? sender,
        MouseEventArgs e)
    {
        if (_lengthCalibrationImage.Image is null)
        {
            return;
        }

        if (!TryMapZoomedImagePoint(
            _lengthCalibrationImage,
            e.Location,
            out Point imagePoint))
        {
            return;
        }

        if (_lengthMarkClickCount == 0)
        {
            _startPixelX.Value = Math.Clamp(
                imagePoint.X,
                decimal.ToInt32(_startPixelX.Minimum),
                decimal.ToInt32(_startPixelX.Maximum));
            _lengthMarkClickCount = 1;
            _lengthStatus.Text =
                $"First mark selected at image X={imagePoint.X}. Click the second mark.";
        }
        else
        {
            _endPixelX.Value = Math.Clamp(
                imagePoint.X,
                decimal.ToInt32(_endPixelX.Minimum),
                decimal.ToInt32(_endPixelX.Maximum));
            _lengthMarkClickCount = 2;
            CalculateLengthScale();
        }
    }

    /// <summary>
    /// Resets the length mark click count, clears the start and end pixel values, and optionally clears the calibration image.
    /// It also updates the length status label to prompt the user for the next action.
    /// </summary>
    /// <param name="clearImage">If set to <c>true</c>, clears the calibration image.</param>
    private void ResetLengthMarks(bool clearImage = false)
    {
        _lengthMarkClickCount = 0;
        _lengthScaleMeasuredThisSession = false;
        _startPixelX.Value = 0;
        _endPixelX.Value = 0;

        if (clearImage)
        {
            Image? oldImage = _lengthCalibrationImage.Image;
            _lengthCalibrationImage.Image = null;
            oldImage?.Dispose();
        }

        _lengthStatus.Text = _lengthCalibrationImage.Image is null
            ? "Capture an image before clicking marks."
            : "Click the first physical mark, then the second physical mark.";
    }

    /// <summary>
    /// Attempts to map a point in the client coordinates of a PictureBox to the corresponding point in the zoomed image coordinates.
    /// </summary>
    /// <param name="pictureBox">The PictureBox containing the image.</param>
    /// <param name="clientPoint">The point in the client coordinates of the PictureBox.</param>
    /// <param name="imagePoint">When this method returns, contains the mapped point in the image coordinates, if the mapping was successful; otherwise, <see cref="Point.Empty"/>.</param>
    /// <returns><c>true</c> if the point was successfully mapped; otherwise, <c>false</c>.</returns>
    private static bool TryMapZoomedImagePoint(
        PictureBox pictureBox,
        Point clientPoint,
        out Point imagePoint)
    {
        imagePoint = Point.Empty;
        Image? image = pictureBox.Image;
        if (image is null || pictureBox.ClientSize.Width <= 0 || pictureBox.ClientSize.Height <= 0)
        {
            return false;
        }

        double scale = Math.Min(
            pictureBox.ClientSize.Width / (double)image.Width,
            pictureBox.ClientSize.Height / (double)image.Height);
        double displayedWidth = image.Width * scale;
        double displayedHeight = image.Height * scale;
        double left = (pictureBox.ClientSize.Width - displayedWidth) / 2.0;
        double top = (pictureBox.ClientSize.Height - displayedHeight) / 2.0;

        if (clientPoint.X < left || clientPoint.X >= left + displayedWidth ||
            clientPoint.Y < top || clientPoint.Y >= top + displayedHeight)
        {
            return false;
        }

        int imageX = (int)Math.Floor((clientPoint.X - left) / scale);
        int imageY = (int)Math.Floor((clientPoint.Y - top) / scale);
        imagePoint = new Point(
            Math.Clamp(imageX, 0, image.Width - 1),
            Math.Clamp(imageY, 0, image.Height - 1));
        return true;
    }


    /// <summary>
    /// Calculates the length scale in pixels per millimeter based on the clicked start and end pixel positions and the known physical distance.
    /// </summary>
    private void CalculateLengthScale()
    {
        decimal pixelSpan = Math.Abs(_endPixelX.Value - _startPixelX.Value);
        if (pixelSpan <= 0 || _knownLengthMM.Value <= 0)
        {
            MessageBox.Show(
                this,
                "The two X pixel positions must be different and the known distance must be greater than zero.",
                "Invalid Length Calibration",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        decimal result = pixelSpan / _knownLengthMM.Value;
        _lengthPixelsPerMM.Value = Math.Clamp(
            result,
            _lengthPixelsPerMM.Minimum,
            _lengthPixelsPerMM.Maximum);
        _lengthScaleMeasuredThisSession = true;
        _lengthStatus.Text =
            $"Calculated {result:F6} pixels/mm from {pixelSpan:F0} pixels over {_knownLengthMM.Value:F3} mm.";
    }

    /// <summary>
    /// Saves the calculated length scale in pixels per millimeter to the application settings, marking it as configured and recording the measurement timestamp.
    /// </summary>
    private void SaveLengthScale()
    {
        if (!_lengthScaleMeasuredThisSession ||
            _lengthMarkClickCount != 2 ||
            _lengthPixelsPerMM.Value <= 0)
        {
            MessageBox.Show(
                this,
                "Capture the final concatenated camera image and click both known-distance marks before saving the axial length scale.",
                "Invalid Length Scale",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _settings.Setup.LengthCalibrationConfigured = true;
        _settings.Setup.PendingLengthPixelsPerMM =
            decimal.ToDouble(_lengthPixelsPerMM.Value);
        _settings.Setup.LengthCalibrationMeasuredUtc = DateTime.UtcNow;
        SaveSettings();
        _lengthStatus.Text =
            $"Length scale saved for calibration generation: {_lengthPixelsPerMM.Value:F6} pixels/mm.";
        RefreshOverallStatus();
    }

    /// <summary>
    /// Captures a known reference block for scanner calibration by taking multiple synchronized head/tail frames,
    /// extracting the reference data, and adding it to the list of references.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>

    private async Task CaptureReferenceAsync()
    {
        string referenceName = _referenceName.Text.Trim();
        double diameter = decimal.ToDouble(_diameterMM.Value);

        if (string.IsNullOrWhiteSpace(referenceName))
        {
            MessageBox.Show(
                this,
                "Enter a reference name.",
                "Reference Name Required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!_constantDiameterConfirmed.Checked)
        {
            MessageBox.Show(
                this,
                "Confirm that the entered diameter applies across the scanned width. A tapered or irregular log cannot be treated as one constant-radius reference.",
                "Reference Geometry Confirmation Required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!_settings.Setup.CameraConfigurationVerified ||
            !_settings.Setup.CameraPhysicalPositionsConfirmed ||
            (!_settings.Setup.LaserDetectionVerified && !_laserVerifiedThisSession))
        {
            MessageBox.Show(
                this,
                "Complete camera connection, physical-position confirmation, and laser verification first.",
                "Camera Verification Required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!ConfirmCalibrationSafety())
        {
            return;
        }

        await RunBusyAsync($"Capturing known reference '{referenceName}'...", async () =>
        {
            using CalibrationCaptureBatch batch =
                await _context.CaptureCalibrationFramesAsync(
                    _settings.Acquisition.TargetFramePairsPerLog,
                    CancellationToken.None);

            EnsureSameCameraSnapshot(batch);

            int nextBandId = _references.Count == 0
                ? 1
                : _references.Max(reference => reference.Summary.RadiusBandId) + 1;

            CalibrationReferenceCapture reference =
                await Task.Run(() => ScannerCalibrationBuilder.ExtractReference(
                    batch,
                    referenceName,
                    diameter,
                    nextBandId));

            _references.Add(reference);
            _referenceName.Clear();
            _constantDiameterConfirmed.Checked = false;
            RefreshReferenceGrid();
            RefreshOverallStatus();

            Append(
                $"Added {reference.Summary.Name}: diameter {reference.Summary.DiameterMM:F3} mm, " +
                $"radius {reference.Summary.RadiusMM:F3} mm, " +
                $"{reference.Summary.AcceptedPointCount} stable lookup columns from " +
                $"{reference.Summary.CapturedFrameCount} frames.");
        });
    }

    /// <summary>
    /// Ensures that the camera configuration snapshots from the provided calibration capture batch match the previously stored snapshots.
    /// </summary>
    /// <param name="batch">The calibration capture batch containing the camera configuration snapshots to verify.</param>
    /// <exception cref="InvalidOperationException">Thrown if the camera configuration snapshots do not match the previously stored snapshots.</exception>
    private void EnsureSameCameraSnapshot(CalibrationCaptureBatch batch)
    {
        byte[] head = [.. batch.HeadCameraConfigurationSnapshot];
        byte[] tail = [.. batch.TailCameraConfigurationSnapshot];

        if (_headCameraSnapshot.Length == 0 && _tailCameraSnapshot.Length == 0)
        {
            _headCameraSnapshot = head;
            _tailCameraSnapshot = tail;
            return;
        }

        if (!CryptographicOperations.FixedTimeEquals(_headCameraSnapshot, head) ||
            !CryptographicOperations.FixedTimeEquals(_tailCameraSnapshot, tail))
        {
            throw new InvalidOperationException(
                "A camera-configuration snapshot changed between calibration captures. Clear the references, reconnect both cameras with the final direct camera settings, and start calibration again.");
        }
    }

    /// <summary>
    /// Activates the scanner calibration by validating the collected references, generating a scanner lookup table,
    /// and saving it as the active calibration package.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the calibration cannot be activated due to insufficient or invalid references,
    /// incomplete length calibration, or missing camera snapshots.</exception>
    private void ActivateCalibration()
    {
        try
        {
            if (_references.Count < 2)
            {
                throw new InvalidOperationException(
                    "At least two different known diameter references are required.");
            }

            if (_references
                .Select(reference => Math.Round(reference.Summary.RadiusMM, 4))
                .Distinct()
                .Count() < 2)
            {
                throw new InvalidOperationException(
                    "The references must have at least two different measured diameters.");
            }

            if (!_settings.Setup.LengthCalibrationConfigured ||
                !double.IsFinite(_settings.Setup.PendingLengthPixelsPerMM) ||
                _settings.Setup.PendingLengthPixelsPerMM <= 0 ||
                _lengthPixelsPerMM.Value <= 0)
            {
                throw new InvalidOperationException(
                    "Complete and save the axial length scale first.");
            }

            if (_headCameraSnapshot.Length == 0 || _tailCameraSnapshot.Length == 0)
            {
                throw new InvalidOperationException(
                    "Capture camera/laser verification or calibration references in this session so the exact camera-configuration snapshots can be preserved.");
            }

            bool independentValidation =
                _independentValidationCompleted.Checked;

            ScannerLookupTable table = ScannerCalibrationBuilder.BuildTable(
                _settings,
                _operatorName.Text,
                _settings.Setup.PendingLengthPixelsPerMM,
                _references,
                _headCameraSnapshot,
                _tailCameraSnapshot,
                independentValidation,
                independentValidation
                    ? decimal.ToDouble(_validationMeanError.Value)
                    : null,
                independentValidation
                    ? decimal.ToDouble(_validationMaxError.Value)
                    : null,
                _calibrationNotes.Text);

            string activePath = CalibrationPackageStore.Activate(
                table,
                _headCameraSnapshot,
                _tailCameraSnapshot);

            _settings.Setup.PendingLengthPixelsPerMM = table.LengthPixelsPerMM;
            _settings.Setup.ScannerCalibrationCreated = true;
            _settings.Setup.ScannerCalibrationRepeatabilityVerified =
                double.IsFinite(table.Quality.MeanRepeatabilityErrorMM) &&
                double.IsFinite(table.Quality.MaximumRepeatabilityErrorMM);
            _settings.Setup.ActiveCalibrationId = table.CalibrationId;
            _settings.Setup.LastSetupCompletedUtc = DateTime.UtcNow;

            // Activation creates evidence but does not self-approve the physical
            // scanner for unrestricted production. A responsible person must
            // complete the commissioning checklist and set this release gate.
            _settings.Production.ScannerCalibrationVerified = false;
            SaveSettings();

            _activationStatus.Text =
                $"Calibration {table.CalibrationId} activated at {activePath}. " +
                $"Image {table.ImageWidth}x{table.ImageHeight}; " +
                $"range {table.MinimumCalibratedRadiusMM:F3} to {table.MaximumCalibratedRadiusMM:F3} mm radius; " +
                $"repeatability mean {table.Quality.MeanRepeatabilityErrorMM:F3} mm, " +
                $"maximum {table.Quality.MaximumRepeatabilityErrorMM:F3} mm; " +
                $"quality status {table.Quality.Status}. ScannerCalibrationVerified remains false until commissioning approval.";

            Append(_activationStatus.Text);
            RefreshOverallStatus();

            MessageBox.Show(
                this,
                "The scanner lookup table and calibration package were generated and activated safely. " +
                "The machine remains in Commissioning mode until physical validation and the commissioning checklist are approved.",
                "Calibration Activated",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowError("Calibration activation failed", ex);
        }
    }


    /// <summary>
    /// Invalidates the calibration-dependent setup by resetting relevant flags and clearing references, snapshots, and measurements.
    /// </summary>
    private void InvalidateCalibrationDependentSetup()
    {
        _settings.Setup.CameraConfigurationVerified = false;
        _settings.Setup.CameraPhysicalPositionsConfirmed = false;
        _settings.Setup.LaserDetectionVerified = false;
        _settings.Setup.LengthCalibrationConfigured = false;
        _settings.Setup.PendingLengthPixelsPerMM = 0.0;
        _settings.Setup.LengthCalibrationMeasuredUtc = null;
        _settings.Setup.ScannerCalibrationCreated = false;
        _settings.Setup.ScannerCalibrationRepeatabilityVerified = false;
        _settings.Setup.ActiveCalibrationId = string.Empty;
        _settings.Setup.LastSetupCompletedUtc = null;

        _settings.Production.CameraSeamCalibrationVerified = false;
        _settings.Production.ScannerCalibrationVerified = false;

        _physicalPositionConfirmed.Checked = false;
        _laserVerifiedThisSession = false;
        _lengthScaleMeasuredThisSession = false;
        _headCameraSnapshot = [];
        _tailCameraSnapshot = [];
        _references.Clear();
        RefreshReferenceGrid();

        Append(
            "Camera settings were reconnected. Laser verification, axial length scale, references, and active-calibration approval were invalidated for safety.");
    }

    /// <summary>
    /// Configures the reference grid DataGridView with appropriate columns, styles, and properties for displaying calibration reference summaries.
    /// </summary>
    private void ConfigureReferenceGrid()
    {
        _referenceGrid.Dock = DockStyle.Fill;
        _referenceGrid.ReadOnly = true;
        _referenceGrid.AllowUserToAddRows = false;
        _referenceGrid.AllowUserToDeleteRows = false;
        _referenceGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _referenceGrid.MultiSelect = false;
        _referenceGrid.AutoGenerateColumns = false;
        _referenceGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        _referenceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Band",
            HeaderText = "Band",
            DataPropertyName = "RadiusBandId",
            FillWeight = 30
        });
        _referenceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Reference",
            HeaderText = "Reference",
            DataPropertyName = "Name",
            FillWeight = 100
        });
        _referenceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Diameter",
            HeaderText = "Diameter mm",
            DataPropertyName = "DiameterMM",
            FillWeight = 70,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "F3" }
        });
        _referenceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Radius",
            HeaderText = "Radius mm",
            DataPropertyName = "RadiusMM",
            FillWeight = 70,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "F3" }
        });
        _referenceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Frames",
            HeaderText = "Frames",
            DataPropertyName = "CapturedFrameCount",
            FillWeight = 45
        });
        _referenceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Points",
            HeaderText = "Stable points",
            DataPropertyName = "AcceptedPointCount",
            FillWeight = 60
        });
        _referenceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "MeanY",
            HeaderText = "Mean Laser Y",
            DataPropertyName = "MeanDetectedLaserY",
            FillWeight = 65,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "F3" }
        });
        _referenceGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "StdY",
            HeaderText = "Laser Y std dev",
            DataPropertyName = "LaserYStandardDeviation",
            FillWeight = 65,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "F3" }
        });
    }

    /// <summary>
    /// Refreshes the reference grid DataGridView with the current list of calibration references, updating the displayed data and
    /// the reference status label with counts and distinct radii information.
    /// </summary>
    private void RefreshReferenceGrid()
    {
        _referenceGrid.DataSource = null;
        _referenceGrid.DataSource = _references
            .Select(reference => reference.Summary)
            .ToList();

        int distinctRadii = _references
            .Select(reference => Math.Round(reference.Summary.RadiusMM, 4))
            .Distinct()
            .Count();

        _referenceStatus.Text =
            $"References: {_references.Count}; distinct radii: {distinctRadii}; " +
            $"minimum required: 2.";
    }

    /// <summary>
    /// Removes the currently selected calibration reference from the list of references, if any, and refreshes the reference grid and overall status.
    /// </summary>
    private void RemoveSelectedReference()
    {
        if (_referenceGrid.CurrentRow?.DataBoundItem is not
            CalibrationReferenceSummary selected)
        {
            return;
        }

        CalibrationReferenceCapture? match = _references.FirstOrDefault(
            reference => reference.Summary.RadiusBandId == selected.RadiusBandId);

        if (match is not null)
        {
            _references.Remove(match);
            RefreshReferenceGrid();
            RefreshOverallStatus();
        }
    }

    /// <summary>
    /// Attempts to show the camera preview for either the head or tail camera by invoking the provided action. If an
    /// exception occurs during the preview, it displays an error message.
    /// </summary>
    /// <param name="showPreview">The action to invoke to show the camera preview.</param>
    /// <param name="position">A string indicating the position of the camera (e.g., "head" or "tail").</param>
    private void ShowCameraPreview(Action showPreview, string position)
    {
        try
        {
            showPreview();
            Append($"Opened the {position} camera preview for physical-position verification.");
        }
        catch (Exception ex)
        {
            ShowError($"Unable to open the {position} camera preview", ex);
        }
    }

    /// <summary>
    /// Refreshes the camera status display with the current connection and configuration information for both the head and tail cameras.
    /// </summary>
    /// <param name="status">The current status of the camera hardware. If null, the status will be retrieved from the context.</param>
    private void RefreshCameraStatus(CameraHardwareStatus? status = null)
    {
        status ??= _context.GetCameraStatus();

        CameraDeviceSettings head = _settings.Acquisition.HeadCamera;
        CameraDeviceSettings tail = _settings.Acquisition.TailCamera;

        _cameraStatus.Text =
            $"HEAD / DOG CAMERA: {(status.HeadConnected ? "CONNECTED" : "NOT CONNECTED")}\r\n" +
            $"Server: {status.HeadServerName} | Serial: {DisplaySerial(status.HeadSerialNumber)}\r\n" +
            $"Mode: {head.Mode} | TriggerSource: {head.TriggerSource}\r\n\r\n" +
            $"TAIL CAMERA: {(status.TailConnected ? "CONNECTED" : "NOT CONNECTED")}\r\n" +
            $"Server: {status.TailServerName} | Serial: {DisplaySerial(status.TailSerialNumber)}\r\n" +
            $"Mode: {tail.Mode} | TriggerSource: {tail.TriggerSource}";
    }


    /// <summary>
    /// Refreshes the overall status display with the current readiness of various setup components, including database configuration, camera verification, physical position confirmation,
    /// laser detection, length calibration, known reference counts, active calibration status, and operating mode.
    /// </summary>
    private void RefreshOverallStatus()
    {
        int distinctRadii = _references
            .Select(reference => Math.Round(reference.Summary.RadiusMM, 4))
            .Distinct()
            .Count();

        _overallStatus.Text =
            $"SQL: {Flag(_settings.Setup.DatabaseConfigured)} | " +
            $"Cameras: {Flag(_settings.Setup.CameraConfigurationVerified)} | " +
            $"Physical sides: {Flag(_settings.Setup.CameraPhysicalPositionsConfirmed)} | " +
            $"Laser: {Flag(_settings.Setup.LaserDetectionVerified)} | " +
            $"Length scale: {Flag(_settings.Setup.LengthCalibrationConfigured)} | " +
            $"Known radius bands this session: {distinctRadii}/2 | " +
            $"Active calibration: {Flag(_settings.Setup.ScannerCalibrationCreated)} | " +
            $"Mode: {_settings.Production.OperatingMode}";
    }


    /// <summary>
    /// Returns a display-friendly string for the camera serial number, showing "unavailable" if the value is null or whitespace.
    /// </summary>
    /// <param name="value">The camera serial number to display.</param>
    /// <returns>A display-friendly string for the camera serial number.</returns>
    private static string DisplaySerial(string value) =>
        string.IsNullOrWhiteSpace(value) ? "unavailable" : value;


    /// <summary>
    /// Returns a string indicating whether a setup component is "ready" or "needed" based on the provided boolean value.
    /// </summary>
    /// <param name="value">A boolean value indicating the readiness of a setup component.</param>
    /// <returns>A string indicating whether the setup component is "ready" or "needed".</returns>
    private static string Flag(bool value) => value ? "ready" : "needed";


    /// <summary>
    /// Prompts the user with a safety confirmation dialog before performing calibration-related operations, ensuring that machine motion is inhibited,
    /// the knife is retracted, the reference is secured, no person is inside the guarded area, and the PLC cannot start a normal production scan.
    /// </summary>
    /// <returns>True if the user confirms the safety conditions; otherwise, false.</returns>
    private bool ConfirmCalibrationSafety()
    {
        DialogResult result = MessageBox.Show(
            this,
            "Before capture, confirm that machine motion is inhibited, the knife is retracted, the reference is secured, no person is inside the guarded area, and the PLC cannot start a normal production scan. Continue?",
            "Calibration Safety Confirmation",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        return result == DialogResult.Yes;
    }


    /// <summary>
    /// Saves the current application settings to persistent storage and writes the
    /// production setup status, ensuring that any changes made during the setup process are preserved.
    /// </summary>
    private void SaveSettings()
    {
        AppSettingsStore.Save(_settings);
        _ = ProductionSetupStatusWriter.Write(_settings);
    }


    /// <summary>
    /// Runs an asynchronous operation while displaying a busy cursor and disabling the form, appending a message to the diagnostics log.
    /// If an exception occurs during
    /// the operation, it shows an error message. After the operation completes, it re-enables the form and restores the cursor.
    /// </summary>
    /// <param name="message">The message to append to the diagnostics log.</param>
    /// <param name="operation">The asynchronous operation to run.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task RunBusyAsync(string message, Func<Task> operation)
    {
        try
        {
            UseWaitCursor = true;
            Enabled = false;
            Append(message);
            await operation();
        }
        catch (Exception ex)
        {
            ShowError(message.TrimEnd('.'), ex);
        }
        finally
        {
            Enabled = true;
            UseWaitCursor = false;
            Activate();
        }
    }

    /// <summary>
    /// Appends a message to the diagnostics log with a timestamp and also logs the message to the application context.
    /// </summary>
    /// <param name="message">The message to append to the diagnostics log and log to the application context.</param>
    private void Append(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _diagnostics.AppendText(line + Environment.NewLine);
        _context.LogMessage("[Setup] " + message);
    }

    /// <summary>
    /// Displays an error message to the user in a message box and appends the error details to the diagnostics log.
    /// </summary>
    /// <param name="title">The title of the error message.</param>
    /// <param name="exception">The exception containing the error details.</param>
    private void ShowError(string title, Exception exception)
    {
        Append(title + ": " + exception.Message);
        MessageBox.Show(
            this,
            exception.Message,
            title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }


    /// <summary>
    /// Creates a new TabPage with the specified text and sets its padding to 8 pixels on all sides.
    /// </summary>
    /// <param name="text">The text to display on the TabPage.</param>
    /// <returns>The newly created TabPage.</returns>
    private static TabPage NewPage(string text) => new(text)
    {
        Padding = new Padding(8)
    };


    /// <summary>
    /// Creates a new vertical FlowLayoutPanel with top-down flow direction, no wrapping, auto-scrolling enabled, and padding of 8 pixels on all sides.
    /// </summary>
    /// <returns>The newly created FlowLayoutPanel.</returns>
    private static FlowLayoutPanel NewVerticalPanel() => new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        Padding = new Padding(8)
    };


    /// <summary>
    /// Creates a new Label with the specified text, bold font style, auto-sizing enabled, and margin settings for spacing.
    /// </summary>
    /// <param name="text">The text to display on the Label.</param>
    /// <returns>The newly created Label.</returns>
    private static Label NewHeading(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
        Margin = new Padding(3, 6, 3, 8)
    };


    /// <summary>
    /// Creates a new Label with the specified text, auto-sizing enabled, a maximum width of 980 pixels for wrapping, and margin settings for spacing.
    /// </summary>
    /// <param name="text">The text to display on the Label.</param>
    /// <returns>The newly created Label.</returns>
    private static Label NewWrappedLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(980, 0),
        Margin = new Padding(3, 4, 3, 10)
    };

    /// <summary>
    /// Creates a new TableLayoutPanel with two columns, where the first column contains a bold label
    /// and the second column contains a path label. The panel is auto-sized and has margin settings for spacing.
    /// </summary>
    /// <param name="label">The text to display on the bold label.</param>
    /// <param name="path">The text to display on the path label.</param>
    /// <returns>The newly created TableLayoutPanel.</returns>
    private static TableLayoutPanel NewPathLabel(string label, string path)
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(3, 3, 3, 6)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(new Label
        {
            Text = label + ":",
            AutoSize = true,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        }, 0, 0);
        row.Controls.Add(new Label
        {
            Text = path,
            AutoSize = true,
            MaximumSize = new Size(780, 0)
        }, 1, 0);
        return row;
    }

    /// <summary>
    /// Creates a new TableLayoutPanel with two columns, where the first column contains a label and the second
    /// column contains a specified control (e.g., TextBox, NumericUpDown).
    /// </summary>
    /// <param name="label">The text to display on the label.</param>
    /// <param name="control">The control to display in the second column (e.g., TextBox, NumericUpDown).</param>
    /// <param name="width">The width of the control.</param>
    /// <returns>The newly created TableLayoutPanel.</returns>
    private static TableLayoutPanel NewLabeledControl(
        string label,
        Control control,
        int width)
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(3, 3, 3, 7)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Padding = new Padding(0, 6, 0, 0)
        }, 0, 0);
        control.Width = width;
        row.Controls.Add(control, 1, 0);
        return row;
    }


    /// <summary>
    /// Creates a new vertical FlowLayoutPanel with a label and a specified control (e.g., TextBox, NumericUpDown) arranged
    /// inline, with the control having a specified width.
    /// </summary>
    /// <param name="label">The text to display on the label.</param>
    /// <param name="control">The control to display below the label (e.g., TextBox, NumericUpDown).</param>
    /// <param name="width">The width of the control.</param>
    /// <returns>The newly created FlowLayoutPanel.</returns>
    private static FlowLayoutPanel NewInlineLabeledControl(
        string label,
        Control control,
        int width)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(3, 0, 14, 0)
        };
        panel.Controls.Add(new Label { Text = label, AutoSize = true });
        control.Width = width;
        panel.Controls.Add(control);
        return panel;
    }

    /// <summary>
    /// Configures a NumericUpDown control with specified minimum, maximum, decimal places, and initial value, ensuring that the value is
    /// clamped within the defined range.
    /// </summary>
    /// <param name="control">The NumericUpDown control to configure.</param>
    /// <param name="minimum">The minimum value allowed.</param>
    /// <param name="maximum">The maximum value allowed.</param>
    /// <param name="decimalPlaces">The number of decimal places to display.</param>
    /// <param name="value">The initial value to set.</param>
    private static void ConfigureNumeric(
        NumericUpDown control,
        decimal minimum,
        decimal maximum,
        int decimalPlaces,
        decimal value)
    {
        control.Minimum = minimum;
        control.Maximum = maximum;
        control.DecimalPlaces = decimalPlaces;
        control.ThousandsSeparator = true;
        control.Value = Math.Clamp(value, minimum, maximum);
    }

    /// <summary>
    /// Clamps a double value to a specified decimal range, converting it to decimal and handling overflow or non-finite values.
    /// </summary>
    /// <param name="value">The double value to clamp.</param>
    /// <param name="minimum">The minimum value allowed.</param>
    /// <param name="maximum">The maximum value allowed.</param>
    /// <returns>The clamped decimal value.</returns>
    private static decimal ClampDecimal(
        double value,
        decimal minimum,
        decimal maximum)
    {
        if (!double.IsFinite(value))
        {
            return minimum;
        }

        decimal converted;
        try
        {
            converted = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }
        catch (OverflowException)
        {
            return value < 0 ? minimum : maximum;
        }

        return Math.Clamp(converted, minimum, maximum);
    }


    /// <summary>
    /// Opens the specified folder path in the system's file explorer, ensuring that the application data directory
    /// exists and creating the folder if it does not already exist.
    /// </summary>
    /// <param name="path">The folder path to open.</param>
    private static void OpenFolder(string path)
    {
        ApplicationPaths.EnsureCreated();
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
