namespace OptimizationXY.Charger.Models.Acquisition;

/// <summary>
/// A Sapera acquisition device detected on the current computer.
/// ServerName and DeviceIndex are runtime locations.
/// SerialNumber identifies the physical camera.
/// </summary>
public sealed record DiscoveredCamera
{
    public required string ServerName { get; init; }

    public required int DeviceIndex { get; init; }

    public required string SerialNumber { get; init; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(SerialNumber)
            ? $"{ServerName} [{DeviceIndex}]"
            : $"{ServerName} [{DeviceIndex}] - Serial {SerialNumber}";

    public override string ToString() => DisplayName;
}
