using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using OptimizationXY.Charger.Models.Production;
using OptimizationXY.Charger.Services.Core;

// ProductionAuditWriter.cs - writes the production audit files into a saved scan folder.
namespace OptimizationXY.Charger.Services.Production;

/// <summary>
/// Writes the production audit record into the saved Scan_#### folder:
/// capture-audit.json, plc-payload.json (INCHES), production-readiness.json,
/// plc-transmission.json, machine-feedback.json, settings-snapshot.json (with
/// the database connection string redacted), direct-recovery-search.json,
/// final-spindle-recommendation.json, kinematic-visualization-state.json,
/// software-build.json, production-summary.txt and a sha256-manifest.txt of
/// every file in the folder. Called again after the PLC write so the record
/// always includes the transmission result.
/// </summary>
public static class ProductionAuditWriter
{

    /// <summary>
    /// The JSON serialization options used for writing the production audit record, including indentation and enum string conversion.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };


    /// <summary>
    /// Writes the production audit record to disk, capturing the current state of the pipeline and relevant information for traceability and debugging.
    /// </summary>
    /// <param name="state">The current state of the pipeline.</param>
    /// <exception cref="InvalidOperationException">Thrown if the audit record cannot be written.</exception>
    public static void Write(PipelineState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        string folder = state.LastSavedRunFolder;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            throw new InvalidOperationException(
                "A saved scan folder is required before writing the production audit record.");
        }

        WriteJson(Path.Combine(folder, "capture-audit.json"), state.CaptureAudit);
        WriteJson(Path.Combine(folder, "plc-payload.json"), state.PlcPayload);
        WriteJson(Path.Combine(folder, "production-readiness.json"), state.ProductionReadiness);
        WriteJson(Path.Combine(folder, "plc-transmission.json"), state.PlcTransmission);
        WriteJson(Path.Combine(folder, "machine-feedback.json"), state.MachineFeedback);
        WriteJson(
            Path.Combine(folder, "settings-snapshot.json"),
            CreateRedactedSettingsSnapshot(state.Settings));
        WriteJson(Path.Combine(folder, "direct-recovery-search.json"), state.DirectRecoverySearch);
        WriteJson(Path.Combine(folder, "final-spindle-recommendation.json"), state.FinalSpindleRecommendation);
        WriteJson(Path.Combine(folder, "kinematic-visualization-state.json"), state.KinematicVisualizationState);
        WriteJson(Path.Combine(folder, "software-build.json"), CreateSoftwareBuildIdentity());

        string summary =
            state.ProductionReadiness?.BuildSummary() ??
            "Production readiness has not been evaluated.";

        WriteTextAtomic(
            Path.Combine(folder, "production-summary.txt"),
            summary);

        WriteIntegrityManifest(folder);
    }

    /// <summary>
    /// Creates a redacted snapshot of the runtime settings, removing sensitive information such as database connection strings.
    /// </summary>
    /// <param name="settings">The runtime settings to redact.</param>
    /// <returns>A redacted JSON snapshot of the runtime settings.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the settings cannot be serialized.</exception>
    private static JsonNode CreateRedactedSettingsSnapshot(object settings)
    {
        JsonNode snapshot = JsonSerializer.SerializeToNode(settings, JsonOptions)
            ?? throw new InvalidOperationException(
                "The runtime settings could not be serialized for the production audit record.");

        if (snapshot["Database"] is JsonObject database)
        {
            string connectionString =
                database["ConnectionString"]?.GetValue<string>() ?? string.Empty;

            database["ConnectionString"] = string.IsNullOrWhiteSpace(connectionString)
                ? string.Empty
                : "<redacted>";
        }

        return snapshot;
    }

    /// <summary>
    /// Creates a software build identity record, capturing information about the running application binary, including
    /// assembly name, version, file version, informational version, and SHA256 hash of the binary.
    /// </summary>
    /// <returns>A software build identity record.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the running application binary cannot be located.</exception>
    private static SoftwareBuildIdentity CreateSoftwareBuildIdentity()
    {
        Assembly assembly = typeof(ProductionAuditWriter).Assembly;
        string assemblyPath = assembly.Location;

        if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
        {
            assemblyPath = Environment.ProcessPath ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
        {
            throw new InvalidOperationException(
                "The running application binary could not be located for the production audit record.");
        }

        FileVersionInfo versionInfo =
            FileVersionInfo.GetVersionInfo(assemblyPath);
        string informationalVersion =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? string.Empty;

        using FileStream stream = File.OpenRead(assemblyPath);
        string hash = Convert.ToHexString(SHA256.HashData(stream));

        return new SoftwareBuildIdentity
        {
            CapturedUtc = DateTime.UtcNow,
            AssemblyName = assembly.GetName().Name ?? string.Empty,
            AssemblyVersion = assembly.GetName().Version?.ToString() ?? string.Empty,
            FileVersion = versionInfo.FileVersion ?? string.Empty,
            InformationalVersion = informationalVersion,
            BuildArtifactFileName = Path.GetFileName(assemblyPath),
            BuildArtifactSha256 = hash
        };
    }

    /// <summary>
    /// Writes the provided value as JSON to the specified path. If the value is null, the file is deleted if it exists.
    /// </summary>
    /// <param name="path">The file path to write the JSON to.</param>
    /// <param name="value">The value to serialize to JSON.</param>
    private static void WriteJson(string path, object? value)
    {
        if (value is null)
        {
            if (File.Exists(path))
                File.Delete(path);
            return;
        }

        string json = JsonSerializer.Serialize(value, JsonOptions);
        WriteTextAtomic(path, json);
    }

    /// <summary>
    /// Writes a SHA256 integrity manifest for all files in the specified folder, excluding the manifest itself.
    /// The manifest lists each file's SHA256 hash and relative path.
    /// </summary>
    /// <param name="folder">The folder to write the integrity manifest for.</param>
    private static void WriteIntegrityManifest(string folder)
    {
        string manifestPath = Path.Combine(folder, "sha256-manifest.txt");

        string[] files = [.. Directory
            .EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(
                path,
                manifestPath,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)];

        string temporaryPath = manifestPath + ".tmp";

        try
        {
            using (var writer = new StreamWriter(
                temporaryPath,
                append: false,
                new UTF8Encoding(false)))
            {
                foreach (string file in files)
                {
                    using FileStream stream = File.OpenRead(file);
                    string hash = Convert.ToHexString(SHA256.HashData(stream));
                    string relative = Path.GetRelativePath(folder, file)
                        .Replace('\\', '/');
                    writer.WriteLine($"{hash}  {relative}");
                }
            }

            File.Move(temporaryPath, manifestPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// Writes text to a file atomically by first writing to a temporary file and then moving it to the target path.
    /// </summary>
    /// <param name="path">The file path to write the text to.</param>
    /// <param name="content">The text content to write to the file.</param>
    private static void WriteTextAtomic(string path, string content)
    {
        string temporaryPath = path + ".tmp";

        try
        {
            File.WriteAllText(
                temporaryPath,
                content,
                new UTF8Encoding(false));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
