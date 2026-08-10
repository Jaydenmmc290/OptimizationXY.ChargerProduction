

namespace OptimizationXY.Charger.UI.Forms;
/// <summary>
/// Represents a form that displays the tail camera view.
/// </summary>

public partial class TailCameraForm : Form
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TailCameraForm"/> class.
    /// </summary>
    public TailCameraForm()
    {
        pictureBoxTail = new DoubleBufferedPictureBox(); // Initialize the field
        InitializeComponent();
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
    /// Initializes the components of the TailCameraForm, including the picture box for displaying the tail camera view.
    /// </summary>
    private void InitializeComponent()
    {
        pictureBoxTail = new DoubleBufferedPictureBox();
        ((System.ComponentModel.ISupportInitialize)pictureBoxTail).BeginInit();
        SuspendLayout();
        //
        // pictureBoxTail
        //
        pictureBoxTail.BackColor = Color.Black;
        pictureBoxTail.Dock = DockStyle.Fill;
        pictureBoxTail.Location = new Point(0, 0);
        pictureBoxTail.Name = "pictureBoxTail";
        pictureBoxTail.Size = new Size(648, 427);
        pictureBoxTail.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBoxTail.TabIndex = 0;
        pictureBoxTail.TabStop = false;
        //
        // TailCameraForm
        //
        ClientSize = new Size(648, 427);
        Controls.Add(pictureBoxTail);
        Name = "TailCameraForm";
        ((System.ComponentModel.ISupportInitialize)pictureBoxTail).EndInit();
        ResumeLayout(false);

    }
}
