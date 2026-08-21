namespace OptimizationXY.Charger.Models.Acquisition;

/// <summary>
/// A Sapera acquisition device detected on the current computer.
/// ServerName and DeviceIndex uniquely identify the camera location.
/// </summary>
public sealed record DiscoveredCamera
{
    public required string ServerName { get; init; }

    public required int DeviceIndex { get; init; }

    public string DisplayName =>
        $"{ServerName} [{DeviceIndex}]";

    public override string ToString() => DisplayName;
}
