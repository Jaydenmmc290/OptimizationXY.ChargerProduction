namespace OptimizationXY.Charger.Models.Plc;

public sealed class PlcTransmissionRecord
{
    public DateTime AttemptedUtc { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; }
    public bool ReadbackVerified { get; set; }
    public string Message { get; set; } = string.Empty;
    public string[] TagsWritten { get; set; } = [.. Array.Empty<string>()];
}
