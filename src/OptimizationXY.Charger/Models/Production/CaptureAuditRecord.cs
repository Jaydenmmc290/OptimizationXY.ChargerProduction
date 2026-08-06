using OptimizationXY.Charger.Models.Acquisition;


/// <summary>
/// Represents an audit record for a completed log capture, including details
/// about the capture process, frame counts, duration, and acquisition mode.
/// </summary>
namespace OptimizationXY.Charger.Models.Production;

public sealed class CaptureAuditRecord
{

    /// <summary>
    /// Gets or sets the unique identifier for the log associated with this capture audit record.
    /// </summary>
    public string LogId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of head frames captured during the log capture process.
    /// </summary>
    public int HeadFrameCount { get; set; }

    /// <summary>
    /// Gets or sets the number of tail frames captured during the log capture process.
    /// </summary>
    public int TailFrameCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of concatenated frames captured during the log capture process.
    /// </summary>
    public int ConcatenatedFrameCount { get; set; }

    /// <summary>
    /// Gets or sets the number of frames that were dropped during the log capture process.
    /// </summary>
    public int DroppedFrameCount { get; set; }

    /// <summary>
    /// Gets or sets the duration of the log capture process in seconds.
    /// </summary>
    public double CaptureDurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp indicating when the log capture process started.
    /// </summary>
    public DateTime CaptureStartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp indicating when the log capture process completed.
    /// </summary>
    public DateTime CaptureCompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the acquisition mode used during the log capture process.
    /// </summary>
    public AcquisitionMode AcquisitionMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a PLC scan completion is required for this capture audit record.
    /// </summary>
    public bool PlcScanCompleteRequired { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the PLC scan completion has been verified for this capture audit record.
    /// </summary>
    public bool PlcScanCompleteVerified { get; set; }

    /// <summary>
    /// Gets or sets any additional notes or comments related to the log capture process.
    /// </summary>
    public string Notes { get; set; } = string.Empty;


    /// <summary>
    /// Creates a new instance of <see cref="CaptureAuditRecord"/> from a given <see cref="CompletedLogCapture"/>.
    /// </summary>
    /// <param name="capture"></param>
    /// <returns></returns>
    public static CaptureAuditRecord FromCapture(CompletedLogCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);

        return new CaptureAuditRecord
        {
            LogId = capture.LogId,
            HeadFrameCount = capture.HeadFrameCount,
            TailFrameCount = capture.TailFrameCount,
            ConcatenatedFrameCount = capture.FramePairCount,
            DroppedFrameCount = capture.DroppedFrameCount,
            CaptureDurationSeconds = capture.CaptureDurationSeconds,
            CaptureStartedUtc = capture.CaptureStartedUtc,
            CaptureCompletedUtc = capture.CaptureCompletedUtc,
            AcquisitionMode = capture.AcquisitionMode,
            Notes = capture.Notes
        };
    }
}
