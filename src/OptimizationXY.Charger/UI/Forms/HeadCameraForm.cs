

namespace OptimizationXY.Charger.UI.Forms;
/// <summary>
/// Represents a form that displays the head camera view with exposure adjustment controls.
/// </summary>

public partial class HeadCameraForm : Form
{
    private MainForm? _parentMainForm;
    private TrackBar? _exposureTrackBar;
    private Label? _exposureLabel;
    private Panel? _exposurePanel;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeadCameraForm"/> class.
    /// </summary>
    public HeadCameraForm()
    {
        pictureBoxHead = new DoubleBufferedPictureBox(); // Initialize the field
        InitializeComponent();
    }

    /// <summary>
    /// Sets the parent MainForm reference to enable exposure adjustment callbacks.
    /// </summary>
    public void SetParentMainForm(MainForm mainForm)
    {
        _parentMainForm = mainForm;
    }

    public Control CameraViewHost => pictureBoxHead;

    /// <summary>
    /// Ensures that the camera view is ready for rendering.
    /// </summary>
    public void EnsureCameraViewReady()
    {
        if (!IsHandleCreated)
            CreateControl();

        if (!pictureBoxHead.IsHandleCreated)
            pictureBoxHead.CreateControl();

        _ = pictureBoxHead.Handle;
    }

    /// <summary>
    /// Updates the preview image displayed in the form.
    /// </summary>
    /// <param name="bitmap">The bitmap image to display as the preview.</param>
    public void UpdatePreview(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        if (IsDisposed || Disposing)
        {
            bitmap.Dispose();
            return;
        }

        Image? previous = pictureBoxHead.Image;
        pictureBoxHead.Image = bitmap;
        previous?.Dispose();
    }

    /// <summary>
    /// Handles the form's FormClosed event to dispose of the previous image.
    /// </summary>
    /// <param name="e">The event data for the FormClosed event.</param>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Image? previous = pictureBoxHead.Image;
        pictureBoxHead.Image = null;
        previous?.Dispose();

        base.OnFormClosed(e);
    }

    protected override void OnFormClosing(
    FormClosingEventArgs e)
    {
        if (e.CloseReason ==
            CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    /// <summary>
    /// A custom PictureBox control that enables double buffering to reduce flickering during rendering.
    /// </summary>
    public sealed class DoubleBufferedPictureBox : PictureBox
    {
        public DoubleBufferedPictureBox()
        {
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);

            UpdateStyles();
        }
    }

    private DoubleBufferedPictureBox pictureBoxHead;


    /// <summary>
    /// Initializes the components of the HeadCameraForm, including exposure adjustment controls.
    /// </summary>
    private void InitializeComponent()
    {
        pictureBoxHead = new DoubleBufferedPictureBox();
        _exposureTrackBar = new TrackBar();
        _exposureLabel = new Label();
        _exposurePanel = new Panel();

        ((System.ComponentModel.ISupportInitialize)pictureBoxHead).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_exposureTrackBar).BeginInit();
        SuspendLayout();

        // pictureBoxHead - fills remaining space after panel
        pictureBoxHead.BackColor = Color.Black;
        pictureBoxHead.Dock = DockStyle.Fill;
        pictureBoxHead.Location = new Point(0, 0);
        pictureBoxHead.Name = "pictureBoxHead";
        pictureBoxHead.Size = new Size(648, 387);
        pictureBoxHead.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBoxHead.TabIndex = 0;
        pictureBoxHead.TabStop = false;

        // _exposureLabel
        _exposureLabel.AutoSize = false;
        _exposureLabel.BackColor = Color.Black;
        _exposureLabel.ForeColor = Color.White;
        _exposureLabel.Location = new Point(8, 8);
        _exposureLabel.Name = "_exposureLabel";
        _exposureLabel.Size = new Size(120, 20);
        _exposureLabel.TabIndex = 1;
        _exposureLabel.Text = "Exposure: 0";
        _exposureLabel.TextAlign = ContentAlignment.MiddleLeft;

        // _exposureTrackBar
        _exposureTrackBar.AutoSize = false;
        _exposureTrackBar.BackColor = Color.Black;
        _exposureTrackBar.Location = new Point(140, 5);
        _exposureTrackBar.Maximum = 100;
        _exposureTrackBar.Minimum = -100;
        _exposureTrackBar.Name = "_exposureTrackBar";
        _exposureTrackBar.Size = new Size(500, 25);
        _exposureTrackBar.TabIndex = 0;
        _exposureTrackBar.TickStyle = TickStyle.None;
        _exposureTrackBar.Value = 0;
        _exposureTrackBar.Scroll += ExposureTrackBar_Scroll;

        // _exposurePanel - docked at bottom
        _exposurePanel.BackColor = Color.Black;
        _exposurePanel.Controls.Add(_exposureLabel);
        _exposurePanel.Controls.Add(_exposureTrackBar);
        _exposurePanel.Dock = DockStyle.Bottom;
        _exposurePanel.Height = 40;
        _exposurePanel.Name = "_exposurePanel";

        // HeadCameraForm
        ClientSize = new Size(648, 427);
        Controls.Add(pictureBoxHead);
        Controls.Add(_exposurePanel);
        Name = "HeadCameraForm";
        ((System.ComponentModel.ISupportInitialize)pictureBoxHead).EndInit();
        ((System.ComponentModel.ISupportInitialize)_exposureTrackBar).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    private void ExposureTrackBar_Scroll(object? sender, EventArgs e)
    {
        if (_exposureTrackBar == null || _exposureLabel == null || _parentMainForm == null)
            return;

        int exposureValue = _exposureTrackBar.Value;
        _exposureLabel.Text = $"Exposure: {exposureValue}";
        _parentMainForm.HeadPreviewExposure = exposureValue;
    }
}
