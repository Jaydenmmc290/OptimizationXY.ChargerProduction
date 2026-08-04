using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace OptimizationXY.Charger.UI.Forms;

public partial class TailCameraForm : Form
{
    public TailCameraForm()
    {
        pictureBoxTail = new DoubleBufferedPictureBox(); // Initialize the field
        InitializeComponent();
    }

    public Control CameraViewHost => pictureBoxTail;

    public void EnsureCameraViewReady()
    {
        if (!IsHandleCreated)
            CreateControl();

        if (!pictureBoxTail.IsHandleCreated)
            pictureBoxTail.CreateControl();

        _ = pictureBoxTail.Handle;
    }

    public void UpdatePreview(Bitmap bitmap)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => UpdatePreview(bitmap));
            return;
        }

        Image? oldImage = pictureBoxTail.Image;
        pictureBoxTail.Image = bitmap;
        oldImage?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            pictureBoxTail?.Image?.Dispose();
            pictureBoxTail?.Dispose();
        }
        base.Dispose(disposing);
    }

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
