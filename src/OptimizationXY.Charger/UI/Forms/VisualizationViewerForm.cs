using Emgu.CV;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace OptimizationXY.Charger.UI.Forms
{
    public partial class VisualizationViewerForm : Form
    {
        private Mat? _image;

        public VisualizationViewerForm(Mat image, string title)
        {
            InitializeComponent();

            Text = title;
            Width = 1200;
            Height = 800;
            StartPosition = FormStartPosition.CenterScreen;

            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Black
            };

            pictureBoxImage.Dock = DockStyle.None;
            pictureBoxImage.SizeMode = PictureBoxSizeMode.Normal;
            pictureBoxImage.BackColor = Color.Black;
            pictureBoxImage.Location = new Point(0, 0);

            _image = image;
            var bitmap = _image.ToBitmap();

            pictureBoxImage.Image = bitmap;
            pictureBoxImage.Width = bitmap.Width;
            pictureBoxImage.Height = bitmap.Height;

            Controls.Remove(pictureBoxImage);
            scrollPanel.Controls.Add(pictureBoxImage);
            Controls.Add(scrollPanel);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            pictureBoxImage.Image?.Dispose();

            // Do NOT dispose _image here if it belongs to PipelineState.
            // _image?.Dispose();

            base.OnFormClosed(e);
        }
    }
}
