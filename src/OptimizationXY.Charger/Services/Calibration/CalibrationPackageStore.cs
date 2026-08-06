using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OptimizationXY.Charger.Infrastructure;
using OptimizationXY.Charger.Models.Calibration;


/// <summary>
/// Provides methods to manage the scanner calibration package, including activation, validation, and restoration of calibration data.
/// </summary>

namespace OptimizationXY.Charger.Services.Calibration;

public static class CalibrationPackageStore
{

    /// <summary>
    /// Defines the required file names for a complete scanner calibration package.
    /// </summary>
    private static readonly string[] PackageFileNames =
    [
        "scanner-lookup-table.json",
        "scanner-calibration.json",
        "head-camera-configuration.snapshot",
        "tail-camera-configuration.snapshot",
        "sha256-manifest.txt"
    ];


    /// <summary>
    /// Defines the JSON serialization options used for reading and writing calibration package files.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Ensures that the active scanner calibration package is valid. If the active package is invalid,
    /// attempts to restore the newest valid archive. Throws an exception if no valid package can be found.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown if no complete, integrity-checked scanner calibration package is active.</exception>
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

    /// <summary>
    /// Activates a new scanner calibration package by validating the provided lookup table and camera configuration snapshots.
    /// </summary>
    /// <param name="table">The scanner lookup table to activate.</param>
    /// <param name="headCameraConfiguration">The head camera configuration snapshot.</param>
    /// <param name="tailCameraConfiguration">The tail camera configuration snapshot.</param>
    /// <returns>The path to the activated scanner lookup table.</returns>
    /// <exception cref="InvalidDataException">Thrown if the scanner calibration cannot be activated due to invalid data.</exception>
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

    /// <summary>
    /// Creates a staged scanner calibration package in the specified directory, including the lookup table,
    /// metadata, camera configuration snapshots, and SHA-256 manifest.
    /// </summary>
    /// <param name="stagingDirectory">The directory in which to create the staged package.</param>
    /// <param name="table">The scanner lookup table to include in the package.</param>
    /// <param name="headCameraConfiguration">The head camera configuration snapshot.</param>
    /// <param name="tailCameraConfiguration">The tail camera configuration snapshot.</param>
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

    /// <summary>
    /// Builds a metadata object from the provided scanner lookup table, containing essential calibration information.
    /// </summary>
    /// <param name="table">The scanner lookup table from which to build the metadata.</param>
    /// <returns>An object containing the metadata.</returns>
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


    /// <summary>
    /// Removes legacy camera configuration files from the active calibration directory, if they exist.
    /// These files are no longer used in the current calibration package format.
    /// </summary>
    private static void RemoveLegacyActiveCameraFiles()
    {
        foreach (string fileName in new[] { "head-camera.ccf", "tail-camera.ccf" })
        {
            string path = Path.Combine(ApplicationPaths.CalibrationDirectory, fileName);
            if (File.Exists(path))
                File.Delete(path);
        }
    }
    
    /// <summary>
    /// Archives the current calibration package if it exists.
    /// </summary>  
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

    /// <summary>
    /// Attempts to restore the newest valid scanner calibration package from the archive directory.
    /// </summary>
    /// <returns>True if a valid package was restored; otherwise, false.</returns>
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


    /// <summary>
    /// Checks if the active scanner calibration package is valid by validating the package directory.
    /// </summary>
    /// <returns>True if the active package is valid; otherwise, false.</returns>
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

    /// <summary>
    /// Validates the scanner calibration package in the specified directory by checking for required files, verifying the SHA-256 manifest,
    /// </summary>
    /// <param name="directory">The directory containing the calibration package to validate.</param>
    /// <param name="expectedCalibrationId">The expected calibration identifier, or null to skip this check.</param>
    /// <exception cref="FileNotFoundException">Thrown if a required file is missing.</exception>
    /// <exception cref="InvalidDataException">Thrown if the calibration package contains invalid data.</exception>
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
    
    /// <summary>
    /// Verifies that the metadata in the specified JSON file matches the provided scanner lookup table.
    /// </summary>
    /// <param name="metadataPath">The path to the metadata JSON file.</param>
    /// <param name="table">The scanner lookup table to compare against.</param>
    /// <exception cref="InvalidDataException">Thrown if the metadata does not match the lookup table.</exception>
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

    /// <summary>
    /// Verifies that the SHA-256 hashes of the provided head and tail camera configuration snapshots match the hashes recorded in the scanner lookup table.
    /// </summary>
    /// <param name="table">The scanner lookup table containing the expected hashes.</param>
    /// <param name="head">The head camera configuration snapshot.</param>
    /// <param name="tail">The tail camera configuration snapshot.</param>
    /// <exception cref="InvalidDataException">Thrown if the snapshot hashes do not match the expected values.</exception>
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

    /// <summary>
    /// Replaces a file at the destination path with the source file atomically. The source file is copied to a temporary
    /// file, and then the temporary file is moved to the destination path, overwriting any existing file.
    /// If the source file does not exist, a FileNotFoundException is thrown.
    /// </summary>
    /// <param name="sourcePath">The path to the source file.</param>
    /// <param name="destinationPath">The path to the destination file.</param>
    /// <exception cref="FileNotFoundException">Thrown if the source file does not exist.</exception>
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
    

    /// <summary>
    /// Removes any temporary ".new" files in the active calibration directory that may have been left over from
    /// a previous activation attempt.
    /// </summary>
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


    /// <summary>
    /// Deletes all files in the active calibration directory that are part of the scanner calibration package.
    /// </summary>
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


    /// <summary>
    /// Loads and validates a scanner lookup table from the specified JSON file path. If the
    /// loaded table is invalid, an InvalidDataException is thrown.
    /// </summary>
    /// <param name="path">The path to the JSON file containing the scanner lookup table.</param>
    /// <returns>The loaded and validated scanner lookup table.</returns>
    /// <exception cref="InvalidDataException">Thrown if the loaded table is invalid.</exception>
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


    /// <summary>
    /// Writes a value to a JSON file atomically. The value is serialized to JSON and written to a temporary file,
    /// which is then moved to the specified path, overwriting any existing
    /// file. If an error occurs during writing or moving, the temporary file is deleted.
    /// </summary>
    /// <typeparam name="T">The type of the value to serialize to JSON.</typeparam>
    /// <param name="path">The path to the JSON file to write.</param>
    /// <param name="value">The value to serialize and write to the JSON file.</param>
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

    
    /// <summary>
    /// Writes a SHA-256 manifest file for the specified directory. The manifest contains the SHA-256 hash of each file in the directory, excluding the manifest file itself.
    /// </summary>
    /// <param name="directory">The directory for which to write the manifest.</param>
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
    
    /// <summary>
    /// Verifies a SHA-256 manifest file for the specified directory. The manifest contains the SHA-256 hash of each file in the directory, excluding the manifest file itself.
    /// </summary>
    /// <param name="directory">The directory for which to verify the manifest.</param>
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

        string[] expectedManifestFiles = [.. PackageFileNames
            .Where(fileName => !string.Equals(
                fileName,
                "sha256-manifest.txt",
                StringComparison.OrdinalIgnoreCase))];

        if (expectedManifestFiles.Any(fileName => !seenFileNames.Contains(fileName)))
        {
            throw new InvalidDataException(
                "The calibration SHA-256 manifest does not list every required package file.");
        }
    }
}
