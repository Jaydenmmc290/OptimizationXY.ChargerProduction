namespace OptimizationXY.Charger.Models.Production;

/// <summary>
/// Immutable identity of the exact application binary that produced a scan record.
/// </summary>
public sealed class SoftwareBuildIdentity
{

    /// <summary>
    /// The UTC timestamp when the build was captured.
    /// </summary>
    public DateTime CapturedUtc { get; init; }

    /// <summary>
    /// The name of the assembly that produced the scan record.
    /// </summary>
    public string AssemblyName { get; init; } = string.Empty;

    /// <summary>
    /// The assembly version of the assembly that produced the scan record.
    /// </summary>
    public string AssemblyVersion { get; init; } = string.Empty;

    /// <summary>
    /// The file version of the assembly that produced the scan record.
    /// </summary>
    public string FileVersion { get; init; } = string.Empty;

    /// <summary>
    /// The informational version of the assembly that produced the scan record.
    /// </summary>
    public string InformationalVersion { get; init; } = string.Empty;

    /// <summary>
    /// The name of the build artifact file that produced the scan record.
    /// </summary>
    public string BuildArtifactFileName { get; init; } = string.Empty;


    /// <summary>
    /// The SHA256 hash of the build artifact file that produced the scan record.
    /// </summary>
    public string BuildArtifactSha256 { get; init; } = string.Empty;
}
