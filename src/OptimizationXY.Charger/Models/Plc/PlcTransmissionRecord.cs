
/// <summary>
/// Represents a record of a PLC transmission attempt, including the timestamp,
/// success status, readback verification, message, and tags written.
/// </summary>
namespace OptimizationXY.Charger.Models.Plc;

public sealed class PlcTransmissionRecord
{

    /// <summary>
    /// Gets or sets the UTC timestamp of the transmission attempt.
    /// </summary>
    public DateTime AttemptedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets a value indicating whether the transmission was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the readback verification was successful.
    /// </summary>
    public bool ReadbackVerified { get; set; }

    /// <summary>
    /// Gets or sets the message associated with the transmission attempt.
    /// </summary>  
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tags that were written during the transmission attempt.
    /// </summary>
    public string[] TagsWritten { get; set; } = [.. Array.Empty<string>()];
}
