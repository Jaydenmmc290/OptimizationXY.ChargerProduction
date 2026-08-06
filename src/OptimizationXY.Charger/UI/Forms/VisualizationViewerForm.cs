using Emgu.CV;


namespace OptimizationXY.Charger.UI.Forms;
/// <summary>
/// A form that displays a visualization image in a scrollable panel.
/// </summary>
public partial class VisualizationViewerForm : Form
{
    private readonly Mat? _image;


    /// <summary>
    /// Initializes a new instance of the <see cref="VisualizationViewerForm"/> class with the specified image and title.
    /// </summary>
    /// <param name="image">The image to display in the form.</param>
    /// <param name="title">The title of the form.</param>
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

    /// <summary>
    /// Handles the FormClosed event to dispose of the image resources.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        pictureBoxImage.Image?.Dispose();

        // Do NOT dispose _image here if it belongs to PipelineState.
        // _image?.Dispose();

        base.OnFormClosed(e);
    }
}
