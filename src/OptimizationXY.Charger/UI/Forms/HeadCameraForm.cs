

namespace OptimizationXY.Charger.UI.Forms;
/// <summary>
/// Represents a form that displays the head camera view.
/// </summary>

public partial class HeadCameraForm : Form
{

    /// <summary>
    /// Initializes a new instance of the <see cref="HeadCameraForm"/> class.
    /// </summary>
    public HeadCameraForm()
    {
        pictureBoxHead = new DoubleBufferedPictureBox(); // Initialize the field
        InitializeComponent();
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
    /// Initializes the components of the HeadCameraForm.
    /// </summary>
    private void InitializeComponent()
    {
        pictureBoxHead = new DoubleBufferedPictureBox();
        ((System.ComponentModel.ISupportInitialize)pictureBoxHead).BeginInit();
        SuspendLayout();
        //
        // pictureBoxHead
        //
        pictureBoxHead.BackColor = Color.Black;
        pictureBoxHead.Dock = DockStyle.Fill;
        pictureBoxHead.Location = new Point(0, 0);
        pictureBoxHead.Name = "pictureBoxHead";
        pictureBoxHead.Size = new Size(648, 427);
        pictureBoxHead.SizeMode = PictureBoxSizeMode.Zoom;
        pictureBoxHead.TabIndex = 0;
        pictureBoxHead.TabStop = false;
        //
        // HeadCameraForm
        //
        ClientSize = new Size(648, 427);
        Controls.Add(pictureBoxHead);
        Name = "HeadCameraForm";
        ((System.ComponentModel.ISupportInitialize)pictureBoxHead).EndInit();
        ResumeLayout(false);

    }
}
