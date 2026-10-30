
// PlcTransmissionRecord.cs - what happened when the command was written to the PLC.
namespace OptimizationXY.Charger.Models.Plc;

/// <summary>
/// Record of one attempt to write a command to the PLC: when, whether it
/// worked, whether reading the tags back matched what was written, a
/// message, and which tags were written. Saved with the scan for the audit trail.
/// </summary>
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
    /// Gets or sets whether the values read back from the PLC after the write matched what was sent.
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
