using System.Drawing.Imaging;
using OptimizationXY.Charger.Models.Calibration;
using OptimizationXY.Charger.Services.Calibration;

namespace OptimizationXY.Charger.UI.Forms;

/// <summary>
/// Helper class for integrating calibration session save/load functionality into the ProductionSetupWizardForm.
/// </summary>
internal static class CalibrationSessionHelper
{
    /// <summary>
    /// Converts an image to base64-encoded PNG string for session storage.
    /// </summary>
    public static string ImageToBase64(string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            return string.Empty;
        }

        try
        {
            using Image image = Image.FromFile(imagePath);
            using var ms = new MemoryStream();
            image.Save(ms, ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Converts a PictureBox image to base64-encoded PNG string for session storage.
    /// </summary>
    public static string PictureBoxToBase64(PictureBox? pictureBox)
    {
        if (pictureBox?.Image == null)
        {
            return string.Empty;
        }

        try
        {
            using var ms = new MemoryStream();
            pictureBox.Image.Save(ms, ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Restores a PictureBox image from base64-encoded PNG string.
    /// </summary>
    public static void Base64ToPictureBox(PictureBox pictureBox, string base64Data)
    {
        if (string.IsNullOrEmpty(base64Data))
        {
            return;
        }

        try
        {
            byte[] imageData = Convert.FromBase64String(base64Data);
            using var ms = new MemoryStream(imageData);
            Image? oldImage = pictureBox.Image;
            pictureBox.Image = Image.FromStream(ms);
            oldImage?.Dispose();
        }
        catch
        {
            // Silently fail if image restoration fails
        }
    }

    /// <summary>
    /// Saves the current calibration session state to disk.
    /// </summary>
    public static void SaveSession(
        string operatorName,
        bool headOnLeft,
        bool reverseHeadHorizontal,
        bool reverseTailHorizontal,
        int headCropLeftPixels,
        int headCropRightPixels,
        int tailCropLeftPixels,
        int tailCropRightPixels,
        int tailVerticalOffsetPixels,
        int overlapPixels,
        bool cameraSeamConfirmed,
        PictureBox? cameraSeamPreview,
        double knownLengthMM,
        int startPixelX,
        int endPixelX,
        double lengthPixelsPerMM,
        bool lengthScaleConfirmed,
        PictureBox? lengthCalibrationImage,
        List<CalibrationReferenceCapture> capturedReferences,
        bool independentValidationCompleted,
        double? independentValidationMeanErrorMM,
        double? independentValidationMaximumErrorMM,
        string calibrationNotes)
    {
        try
        {
            var state = CalibrationSessionStore.CreateSessionState(
                operatorName,
                headOnLeft,
                reverseHeadHorizontal,
                reverseTailHorizontal,
                headCropLeftPixels,
                headCropRightPixels,
                tailCropLeftPixels,
                tailCropRightPixels,
                tailVerticalOffsetPixels,
                overlapPixels,
                cameraSeamConfirmed,
                PictureBoxToBase64(cameraSeamPreview),
                knownLengthMM,
                startPixelX,
                endPixelX,
                lengthPixelsPerMM,
                lengthScaleConfirmed,
                PictureBoxToBase64(lengthCalibrationImage),
                capturedReferences,
                independentValidationCompleted,
                independentValidationMeanErrorMM,
                independentValidationMaximumErrorMM,
                calibrationNotes);

            CalibrationSessionStore.SaveIncompleteSession(state);
        }
        catch
        {
            // Silently fail on session save; don't break the workflow
        }
    }

    /// <summary>
    /// Checks if a previous calibration session exists and can be resumed.
    /// </summary>
    public static bool HasPreviousSession() =>
        CalibrationSessionStore.IncompleteSessionExists();

    /// <summary>
    /// Loads a previous calibration session from disk.
    /// </summary>
    public static CalibrationSessionState? LoadSession()
    {
        try
        {
            return CalibrationSessionStore.LoadIncompleteSession();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Clears the saved calibration session after successful completion.
    /// </summary>
    public static void ClearSession()
    {
        try
        {
            CalibrationSessionStore.ClearIncompleteSession();
        }
        catch
        {
            // Silently fail on session clear
        }
    }
}
