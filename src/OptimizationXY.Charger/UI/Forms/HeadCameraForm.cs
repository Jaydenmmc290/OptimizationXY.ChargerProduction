using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace OptimizationXY.Charger.UI.Forms;

public partial class HeadCameraForm : Form
{
    public HeadCameraForm()
    {
        pictureBoxHead = new DoubleBufferedPictureBox(); // Initialize the field
        InitializeComponent();
    }

    public Control CameraViewHost => pictureBoxHead;

    public void EnsureCameraViewReady()
    {
        if (!IsHandleCreated)
            CreateControl();

        if (!pictureBoxHead.IsHandleCreated)
            pictureBoxHead.CreateControl();

        _ = pictureBoxHead.Handle;
    }

    public void UpdatePreview(Bitmap bitmap)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => UpdatePreview(bitmap));
            return;
        }

        Image? oldImage = pictureBoxHead.Image;
        pictureBoxHead.Image = bitmap;
        oldImage?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            pictureBoxHead?.Image?.Dispose();
            pictureBoxHead?.Dispose();
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

    private DoubleBufferedPictureBox pictureBoxHead;

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
