using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using OptimizationXY.Charger.Models.Production;
using OptimizationXY.Charger.Services.Core;

namespace OptimizationXY.Charger.Services.Production;

public static class ProductionAuditWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

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
        WriteJson(Path.Combine(folder, "software-build.json"), CreateSoftwareBuildIdentity());

        string summary =
            state.ProductionReadiness?.BuildSummary() ??
            "Production readiness has not been evaluated.";

        WriteTextAtomic(
            Path.Combine(folder, "production-summary.txt"),
            summary);

        WriteIntegrityManifest(folder);
    }

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

    private static void WriteIntegrityManifest(string folder)
    {
        string manifestPath = Path.Combine(folder, "sha256-manifest.txt");

        string[] files = Directory
            .EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(
                path,
                manifestPath,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

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
