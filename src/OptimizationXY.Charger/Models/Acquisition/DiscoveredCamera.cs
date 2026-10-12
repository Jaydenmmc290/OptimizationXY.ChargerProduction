namespace OptimizationXY.Charger.Models.Acquisition;

/// <summary>
/// A Sapera acquisition device detected on the current computer.
/// ServerName and DeviceIndex uniquely identify the camera location.
/// </summary>
public sealed record DiscoveredCamera
{
    public required string ServerName { get; init; }

    public required int DeviceIndex { get; init; }

    /// <summary>
    /// Identity strings the camera reported when it was probed (serial number
    /// and device ID features). Empty when the camera was not probed.
    /// </summary>
    public IReadOnlyList<string> IdentityStrings { get; init; } = [];

    public string IdentityDescription =>
        IdentityStrings.Count == 0
            ? "<no identity reported>"
            : string.Join(" / ", IdentityStrings);

    public string DisplayName =>
        $"{ServerName} [{DeviceIndex}]";

    public override string ToString() => DisplayName;
}
