using OptimizationXY.Charger.Models.Acquisition;

namespace OptimizationXY.Charger.Models.Production;

public sealed class CaptureAuditRecord
{
    public string LogId { get; set; } = string.Empty;
    public int HeadFrameCount { get; set; }
    public int TailFrameCount { get; set; }
    public int ConcatenatedFrameCount { get; set; }
    public int DroppedFrameCount { get; set; }
    public double CaptureDurationSeconds { get; set; }
    public DateTime CaptureStartedUtc { get; set; }
    public DateTime CaptureCompletedUtc { get; set; }
    public AcquisitionMode AcquisitionMode { get; set; }
    public bool PlcScanCompleteRequired { get; set; }
    public bool PlcScanCompleteVerified { get; set; }
    public string Notes { get; set; } = string.Empty;

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
