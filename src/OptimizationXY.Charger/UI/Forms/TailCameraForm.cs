

namespace OptimizationXY.Charger.UI.Forms;
/// <summary>
/// Live preview window for the Tail camera, with a brightness slider.
/// <para>
/// The slider (-100 … +100, 0 = unchanged) only changes how bright the
/// PREVIEW looks on screen (MainForm.TailPreviewExposure). It does NOT change
/// the camera's exposure and does not affect captured frames, calibration or
/// measurements. Closing the window only hides it.
/// </para>
/// </summary>

public partial class TailCameraForm : Form
{
    private MainForm? _parentMainForm;
    private TrackBar? _exposureTrackBar;
    private Label? _exposureLabel;
    private Panel? _exposurePanel;

    /// <summary>
    /// Initializes a new instance of the <see cref="TailCameraForm"/> class.
    /// </summary>
    public TailCameraForm()
    {
        pictureBoxTail = new DoubleBufferedPictureBox(); // Initialize the field
        InitializeComponent();
    }

    /// <summary>
    /// Sets the parent MainForm reference to enable exposure adjustment callbacks.
    /// </summary>
    public void SetParentMainForm(MainForm mainForm)
    {
        _parentMainForm = mainForm;
    }

    public Control CameraViewHost => pictureBoxTail;

    /// <summary>
    /// Ensures that the camera view is ready for display by creating the necessary controls and handles.
    /// </summary>
    public void EnsureCameraViewReady()
    {
        if (!IsHandleCreated)
            CreateControl();

        if (!pictureBoxTail.IsHandleCreated)
            pictureBoxTail.CreateControl();

        _ = pictureBoxTail.Handle;
    }

    /// <summary>
    /// Updates the preview image displayed in the tail camera view.
    /// </summary>
    /// <param name="bitmap"></param>
    public void UpdatePreview(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        if (IsDisposed || Disposing)
        {
            bitmap.Dispose();
            return;
        }

        Image? previous = pictureBoxTail.Image;
        pictureBoxTail.Image = bitmap;
        previous?.Dispose();
    }

    /// <summary>
    /// Handles the form's FormClosed event to dispose of the previous image in the picture box.
    /// </summary>
    /// <param name="e">The event data for the FormClosed event.</param>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Image? previous = pictureBoxTail.Image;
        pictureBoxTail.Image = null;
        previous?.Dispose();

        base.OnFormClosed(e);
    }

    /// <summary>
    /// The X button hides the window instead of closing it, so the camera view
    /// host stays alive. Other close reasons (program exit) close it normally.
    /// </summary>
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
    /// A custom PictureBox control that enables double buffering to reduce flickering during image updates.
    /// </summary>
    public sealed class DoubleBufferedPictureBox : PictureBox
    {
        /// <summary>Turns on double buffering so the live image does not flicker.</summary>
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

    private DoubleBufferedPictureBox pictureBoxTail;


    /// <summary>
    /// Initializes the components of the TailCameraForm, including exposure adjustment controls.
    /// </summary>
    private void InitializeComponent()
    {
        pictureBoxTail = new DoubleBufferedPictureBox();
        _exposureTrackBar = new TrackBar();
        _exposureLabel = new Label();
        _exposurePanel = new Panel();

        ((System.ComponentModel.ISupportInitialize)pictureBoxTail).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_exposureTrackBar).BeginInit();
        SuspendLayout();

        // pictureBoxTail - fills remaining space after panel
        pictureBoxTail.BackColor = Color.Black;
        pictureBoxTail.Dock = DockStyle.Fill;
        pictureBoxTail.Location = new Point(0, 0);
        pictureBoxTail.Name = "pictureBoxTail";
        pictureBoxTail.Size = new Size(648, 387);
        pictureBoxTail.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBoxTail.TabIndex = 0;
        pictureBoxTail.TabStop = false;

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

        // TailCameraForm
        ClientSize = new Size(648, 427);
        Controls.Add(pictureBoxTail);
        Controls.Add(_exposurePanel);
        Name = "TailCameraForm";
        ((System.ComponentModel.ISupportInitialize)pictureBoxTail).EndInit();
        ((System.ComponentModel.ISupportInitialize)_exposureTrackBar).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    /// <summary>
    /// Slider moved: shows the value and passes it to MainForm, which brightens
    /// or darkens the preview image only (display adjustment, not camera exposure).
    /// </summary>
    private void ExposureTrackBar_Scroll(object? sender, EventArgs e)
    {
        if (_exposureTrackBar == null || _exposureLabel == null || _parentMainForm == null)
            return;

        int exposureValue = _exposureTrackBar.Value;
        _exposureLabel.Text = $"Exposure: {exposureValue}";
        _parentMainForm.TailPreviewExposure = exposureValue;
    }
}
