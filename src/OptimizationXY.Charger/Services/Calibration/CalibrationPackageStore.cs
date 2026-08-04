using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OptimizationXY.Charger.Infrastructure;
using OptimizationXY.Charger.Models.Calibration;

namespace OptimizationXY.Charger.Services.Calibration;

public static class CalibrationPackageStore
{
    private static readonly string[] PackageFileNames =
    [
        "scanner-lookup-table.json",
        "scanner-calibration.json",
        "head-camera-configuration.snapshot",
        "tail-camera-configuration.snapshot",
        "sha256-manifest.txt"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static void EnsureActivePackageValid()
    {
        ApplicationPaths.EnsureCreated();
        RemoveTemporaryActiveFiles();

        try
        {
            ValidatePackageDirectory(
                ApplicationPaths.CalibrationDirectory);
        }
        catch (Exception validationException)
        {
            if (TryRestoreNewestArchive())
            {
                return;
            }

            throw new InvalidDataException(
                "No complete, integrity-checked scanner calibration package is active. Run the in-program Production Setup / Calibration wizard.",
                validationException);
        }
    }

    public static string Activate(
        ScannerLookupTable table,
        byte[] headCameraConfiguration,
        byte[] tailCameraConfiguration)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(headCameraConfiguration);
        ArgumentNullException.ThrowIfNull(tailCameraConfiguration);

        if (!table.IsValid)
        {
            throw new InvalidDataException(
                "The scanner calibration cannot be activated because its lookup table is invalid.");
        }

        if (headCameraConfiguration.Length == 0 ||
            tailCameraConfiguration.Length == 0)
        {
            throw new InvalidDataException(
                "Both capture-time camera configuration snapshots are required to activate calibration.");
        }

        VerifySnapshotHashes(
            table,
            headCameraConfiguration,
            tailCameraConfiguration);

        ApplicationPaths.EnsureCreated();

        string stagingDirectory = Path.Combine(
            ApplicationPaths.CalibrationStagingDirectory,
            table.CalibrationId);

        if (Directory.Exists(stagingDirectory))
        {
            Directory.Delete(stagingDirectory, recursive: true);
        }

        Directory.CreateDirectory(stagingDirectory);

        try
        {
            CreateStagedPackage(
                stagingDirectory,
                table,
                headCameraConfiguration,
                tailCameraConfiguration);

            ValidatePackageDirectory(
                stagingDirectory,
                table.CalibrationId);

            ArchiveCurrentCalibrationIfPresent();

            // Each file is copied to a temporary path in the active directory
            // and then renamed. A process interruption can therefore leave an
            // old complete package or an obviously invalid mixed package; the
            // next activation restores the newest valid archive.
            foreach (string fileName in PackageFileNames)
            {
                ReplaceFileAtomically(
                    Path.Combine(stagingDirectory, fileName),
                    Path.Combine(ApplicationPaths.CalibrationDirectory, fileName));
            }

            ValidatePackageDirectory(
                ApplicationPaths.CalibrationDirectory,
                table.CalibrationId);

            // Final runtime validation uses the same loader as production scans.
            ScannerLookupTable active = LookupTableStore.Load();
            if (!string.Equals(
                active.CalibrationId,
                table.CalibrationId,
                StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The activated calibration does not match the staged calibration.");
            }

            RemoveLegacyActiveCameraFiles();
            return ApplicationPaths.CalibrationLookupTablePath;
        }
        catch
        {
            RemoveTemporaryActiveFiles();

            if (!TryRestoreNewestArchive() && !IsActivePackageValid())
            {
                DeleteActivePackage();
            }

            throw;
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    private static void CreateStagedPackage(
        string stagingDirectory,
        ScannerLookupTable table,
        byte[] headCameraConfiguration,
        byte[] tailCameraConfiguration)
    {
        WriteJsonAtomic(
            Path.Combine(stagingDirectory, "scanner-lookup-table.json"),
            table);
        WriteJsonAtomic(
            Path.Combine(stagingDirectory, "scanner-calibration.json"),
            BuildMetadata(table));
        File.WriteAllBytes(
            Path.Combine(stagingDirectory, "head-camera-configuration.snapshot"),
            headCameraConfiguration);
        File.WriteAllBytes(
            Path.Combine(stagingDirectory, "tail-camera-configuration.snapshot"),
            tailCameraConfiguration);
        WriteManifest(stagingDirectory);
    }

    private static object BuildMetadata(ScannerLookupTable table) => new
    {
        table.SchemaVersion,
        table.CalibrationId,
        table.CreatedUtc,
        table.OperatorName,
        table.MachineName,
        table.SoftwareVersion,
        table.HeadCameraServerName,
        table.TailCameraServerName,
        table.HeadCameraConfigurationSha256,
        table.TailCameraConfigurationSha256,
        table.AcquisitionGeometrySha256,
        table.ImageWidth,
        table.ImageHeight,
        table.LengthPixelsPerMM,
        table.MinimumCalibratedRadiusMM,
        table.MaximumCalibratedRadiusMM,
        table.References,
        table.Quality
    };

    private static void RemoveLegacyActiveCameraFiles()
    {
        foreach (string fileName in new[] { "head-camera.ccf", "tail-camera.ccf" })
        {
            string path = Path.Combine(ApplicationPaths.CalibrationDirectory, fileName);
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static void ArchiveCurrentCalibrationIfPresent()
    {
        if (!File.Exists(ApplicationPaths.CalibrationLookupTablePath))
        {
            return;
        }

        string archiveDirectory = Path.Combine(
            ApplicationPaths.CalibrationArchiveDirectory,
            $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}");

        Directory.CreateDirectory(archiveDirectory);

        foreach (string fileName in PackageFileNames)
        {
            string source = Path.Combine(
                ApplicationPaths.CalibrationDirectory,
                fileName);

            if (File.Exists(source))
            {
                File.Copy(
                    source,
                    Path.Combine(archiveDirectory, fileName),
                    overwrite: true);
            }
        }
    }

    private static bool TryRestoreNewestArchive()
    {
        if (IsActivePackageValid())
        {
            return true;
        }

        DirectoryInfo archiveRoot = new(
            ApplicationPaths.CalibrationArchiveDirectory);

        if (!archiveRoot.Exists)
        {
            return false;
        }

        foreach (DirectoryInfo candidate in archiveRoot
            .EnumerateDirectories()
            .OrderByDescending(directory => directory.Name))
        {
            try
            {
                ValidatePackageDirectory(candidate.FullName);

                foreach (string fileName in PackageFileNames)
                {
                    ReplaceFileAtomically(
                        Path.Combine(candidate.FullName, fileName),
                        Path.Combine(ApplicationPaths.CalibrationDirectory, fileName));
                }

                ValidatePackageDirectory(
                    ApplicationPaths.CalibrationDirectory);
                return true;
            }
            catch
            {
                // Continue to the next older archive.
            }
        }

        return false;
    }

    private static bool IsActivePackageValid()
    {
        try
        {
            ValidatePackageDirectory(
                ApplicationPaths.CalibrationDirectory);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ValidatePackageDirectory(
        string directory,
        string? expectedCalibrationId = null)
    {
        foreach (string fileName in PackageFileNames)
        {
            string path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Calibration package file is missing: {path}",
                    path);
            }
        }

        VerifyManifest(directory);

        ScannerLookupTable table = LoadAndValidate(
            Path.Combine(directory, "scanner-lookup-table.json"));

        if (!string.IsNullOrWhiteSpace(expectedCalibrationId) &&
            !string.Equals(
                table.CalibrationId,
                expectedCalibrationId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The calibration package identifier does not match the expected identifier.");
        }

        VerifyMetadata(
            Path.Combine(directory, "scanner-calibration.json"),
            table);

        byte[] head = File.ReadAllBytes(
            Path.Combine(directory, "head-camera-configuration.snapshot"));
        byte[] tail = File.ReadAllBytes(
            Path.Combine(directory, "tail-camera-configuration.snapshot"));
        VerifySnapshotHashes(table, head, tail);
    }

    private static void VerifyMetadata(
        string metadataPath,
        ScannerLookupTable table)
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(metadataPath, Encoding.UTF8));
        JsonElement root = document.RootElement;

        int schemaVersion = root.GetProperty("SchemaVersion").GetInt32();
        string calibrationId = root.GetProperty("CalibrationId").GetString() ?? string.Empty;
        string headHash = root.GetProperty("HeadCameraConfigurationSha256").GetString() ?? string.Empty;
        string tailHash = root.GetProperty("TailCameraConfigurationSha256").GetString() ?? string.Empty;
        string geometryHash = root.GetProperty("AcquisitionGeometrySha256").GetString() ?? string.Empty;
        int imageWidth = root.GetProperty("ImageWidth").GetInt32();
        int imageHeight = root.GetProperty("ImageHeight").GetInt32();
        double lengthPixelsPerMM = root.GetProperty("LengthPixelsPerMM").GetDouble();
        double minimumRadius = root.GetProperty("MinimumCalibratedRadiusMM").GetDouble();
        double maximumRadius = root.GetProperty("MaximumCalibratedRadiusMM").GetDouble();

        if (schemaVersion != table.SchemaVersion ||
            !string.Equals(calibrationId, table.CalibrationId, StringComparison.Ordinal) ||
            !string.Equals(headHash, table.HeadCameraConfigurationSha256, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(tailHash, table.TailCameraConfigurationSha256, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(geometryHash, table.AcquisitionGeometrySha256, StringComparison.OrdinalIgnoreCase) ||
            imageWidth != table.ImageWidth ||
            imageHeight != table.ImageHeight ||
            Math.Abs(lengthPixelsPerMM - table.LengthPixelsPerMM) > 1e-9 ||
            Math.Abs(minimumRadius - table.MinimumCalibratedRadiusMM) > 1e-9 ||
            Math.Abs(maximumRadius - table.MaximumCalibratedRadiusMM) > 1e-9)
        {
            throw new InvalidDataException(
                "The scanner calibration metadata does not match the lookup table.");
        }
    }

    private static void VerifySnapshotHashes(
        ScannerLookupTable table,
        byte[] head,
        byte[] tail)
    {
        string actualHead = Convert.ToHexString(SHA256.HashData(head));
        string actualTail = Convert.ToHexString(SHA256.HashData(tail));

        if (!string.Equals(
                actualHead,
                table.HeadCameraConfigurationSha256,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                actualTail,
                table.TailCameraConfigurationSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The camera-configuration snapshots do not match the hashes recorded in the scanner calibration.");
        }
    }

    private static void ReplaceFileAtomically(
        string sourcePath,
        string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException(
                $"Calibration source file is missing: {sourcePath}",
                sourcePath);
        }

        string temporaryPath = destinationPath + ".new";

        try
        {
            File.Copy(sourcePath, temporaryPath, overwrite: true);
            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void RemoveTemporaryActiveFiles()
    {
        foreach (string fileName in PackageFileNames)
        {
            string temporaryPath = Path.Combine(
                ApplicationPaths.CalibrationDirectory,
                fileName + ".new");

            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void DeleteActivePackage()
    {
        foreach (string fileName in PackageFileNames)
        {
            string path = Path.Combine(
                ApplicationPaths.CalibrationDirectory,
                fileName);

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static ScannerLookupTable LoadAndValidate(string path)
    {
        string json = File.ReadAllText(path, Encoding.UTF8);
        ScannerLookupTable? table =
            JsonSerializer.Deserialize<ScannerLookupTable>(json, JsonOptions);

        if (table?.IsValid != true)
        {
            throw new InvalidDataException(
                "The staged scanner lookup table failed reload validation.");
        }

        return table;
    }

    private static void WriteJsonAtomic<T>(string path, T value)
    {
        string temporaryPath = path + ".tmp";
        string json = JsonSerializer.Serialize(value, JsonOptions);

        try
        {
            File.WriteAllText(
                temporaryPath,
                json,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void WriteManifest(string directory)
    {
        string manifestPath = Path.Combine(directory, "sha256-manifest.txt");
        var lines = Directory.EnumerateFiles(directory)
            .Where(path => !string.Equals(
                path,
                manifestPath,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(path =>
                $"{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}  {Path.GetFileName(path)}")
            .ToArray();

        File.WriteAllLines(
            manifestPath,
            lines,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void VerifyManifest(string directory)
    {
        string manifestPath = Path.Combine(directory, "sha256-manifest.txt");
        string[] lines = File.ReadAllLines(manifestPath);

        if (lines.Length != PackageFileNames.Length - 1)
        {
            throw new InvalidDataException(
                "The calibration SHA-256 manifest does not contain the expected number of files.");
        }

        var seenFileNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (string line in lines)
        {
            string[] parts = line.Split(
                "  ",
                2,
                StringSplitOptions.None);

            if (parts.Length != 2 ||
                string.IsNullOrWhiteSpace(parts[0]) ||
                string.IsNullOrWhiteSpace(parts[1]))
            {
                throw new InvalidDataException(
                    "The calibration SHA-256 manifest is malformed.");
            }

            string fileName = Path.GetFileName(parts[1]);
            if (!string.Equals(fileName, parts[1], StringComparison.Ordinal) ||
                !PackageFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase) ||
                string.Equals(fileName, "sha256-manifest.txt", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "The calibration SHA-256 manifest contains an unexpected file name.");
            }

            if (!seenFileNames.Add(fileName))
            {
                throw new InvalidDataException(
                    "The calibration SHA-256 manifest contains a duplicate file name.");
            }

            string filePath = Path.Combine(directory, fileName);
            string actual = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(filePath)));

            if (!string.Equals(actual, parts[0], StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Calibration package integrity check failed for {fileName}.");
            }
        }

        string[] expectedManifestFiles = PackageFileNames
            .Where(fileName => !string.Equals(
                fileName,
                "sha256-manifest.txt",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (expectedManifestFiles.Any(fileName => !seenFileNames.Contains(fileName)))
        {
            throw new InvalidDataException(
                "The calibration SHA-256 manifest does not list every required package file.");
        }
    }
}
