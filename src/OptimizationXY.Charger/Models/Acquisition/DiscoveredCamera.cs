namespace OptimizationXY.Charger.Models.Acquisition;

/// <summary>
/// A Sapera acquisition device detected on the current computer.
/// ServerName and DeviceIndex uniquely identify the camera location.
/// </summary>
public sealed record DiscoveredCamera
{
    /// <summary>Sapera server (acquisition device) name the camera was found on.</summary>
    public required string ServerName { get; init; }

    /// <summary>Resource index on that server.</summary>
    public required int DeviceIndex { get; init; }

    /// <summary>
    /// Identity strings the camera reported when it was probed (serial number
    /// and device ID features). Empty when the camera was not probed.
    /// </summary>
    public IReadOnlyList<string> IdentityStrings { get; init; } = [];

    /// <summary>
    /// True when the camera could be opened during probing, so its own serial
    /// number features were read. False when it could not be opened (for example
    /// because it was still in use) and only Sapera's location serial is known.
    /// </summary>
    public bool IdentityProbed { get; init; }

    /// <summary>All identity strings joined with " / " for display, or a placeholder if none were reported.</summary>
    public string IdentityDescription =>
        IdentityStrings.Count == 0
            ? "<no identity reported>"
            : string.Join(" / ", IdentityStrings);

    /// <summary>Short label "ServerName [DeviceIndex]" used in lists and logs.</summary>
    public string DisplayName =>
        $"{ServerName} [{DeviceIndex}]";

    public override string ToString() => DisplayName;
}
