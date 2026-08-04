namespace OptimizationXY.Charger.Models.Production;

/// <summary>
/// Immutable identity of the exact application binary that produced a scan record.
/// </summary>
public sealed class SoftwareBuildIdentity
{
    public DateTime CapturedUtc { get; init; }

    public string AssemblyName { get; init; } = string.Empty;

    public string AssemblyVersion { get; init; } = string.Empty;

    public string FileVersion { get; init; } = string.Empty;

    public string InformationalVersion { get; init; } = string.Empty;

    public string BuildArtifactFileName { get; init; } = string.Empty;

    public string BuildArtifactSha256 { get; init; } = string.Empty;
}
