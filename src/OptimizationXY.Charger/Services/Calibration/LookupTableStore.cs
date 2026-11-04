using System.Text;
using System.Text.Json;
using OptimizationXY.Charger.Infrastructure;
using OptimizationXY.Charger.Models.Calibration;

// LookupTableStore.cs - loads the ACTIVE scanner calibration for a scan.
namespace OptimizationXY.Charger.Services.Calibration;

/// <summary>
/// Loads the active scanner lookup table (Config\Calibration\scanner-lookup-table.json)
/// once per scan. Missing or invalid calibration is a hard failure - no
/// estimated table is ever made up.
/// </summary>
public static class LookupTableStore
{
    // One load at a time (the calibration wizard may be activating a package).
    private static readonly Lock Sync = new();


    /// <summary>
    /// JSON serialization options for reading and writing the scanner lookup table.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// Gets the file path for the scanner lookup table JSON file.
    /// </summary>
    public static string FilePath => ApplicationPaths.CalibrationLookupTablePath;

    /// <summary>
    /// Loads the scanner lookup table from the JSON file.
    /// </summary>
    /// <returns>The loaded <see cref="ScannerLookupTable"/>.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the JSON file does not exist.</exception>
    /// <exception cref="InvalidDataException">Thrown if the JSON file is invalid or the table is not valid.</exception>
    public static ScannerLookupTable Load()
    {
        lock (Sync)
        {
            ApplicationPaths.EnsureCreated();
            // Check the active package's files and hashes are intact before reading them.
            CalibrationPackageStore.EnsureActivePackageValid();

            if (!File.Exists(FilePath))
            {
                throw new FileNotFoundException(
                    $"Scanner calibration is required before processing. Run Production Setup / Calibration to generate and activate a complete calibration package at '{ApplicationPaths.CalibrationDirectory}'.",
                    FilePath);
            }

            try
            {
                string json = File.ReadAllText(FilePath, Encoding.UTF8);
                ScannerLookupTable? table = JsonSerializer.Deserialize<ScannerLookupTable>(json, JsonOptions);

                ValidateProductionTable(table);
                return table!;
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(
                    $"Scanner calibration at '{FilePath}' is not valid JSON.",
                    ex);
            }
        }
    }


    // ------------------------------------------------------------------
    // Per-scan cache. Load() hashes every package file and parses the JSON;
    // done on every scan that sits between ScanComplete and the PLC write.
    // LoadForScan() does the same full Load() the first time and whenever any
    // file in the calibration folder changes (name, size or write time), and
    // otherwise hands back the table it already validated. The calibration
    // itself is never changed here; the table is only read by the pipeline.
    // ------------------------------------------------------------------
    private static ScannerLookupTable? _cachedTable;
    private static string? _cachedFingerprint;

    /// <summary>
    /// The active lookup table for a production scan: the full validated
    /// <see cref="Load"/> when the calibration folder has changed since the last
    /// call, otherwise the table already validated. The caller must not modify it.
    /// </summary>
    public static ScannerLookupTable LoadForScan()
    {
        lock (Sync)
        {
            string? fingerprint = TryFingerprintCalibrationFolder();

            if (fingerprint is not null &&
                _cachedTable is not null &&
                string.Equals(fingerprint, _cachedFingerprint, StringComparison.Ordinal))
            {
                return _cachedTable;
            }

            ScannerLookupTable table = Load();

            // Taken after Load(): a damaged package may just have been restored.
            _cachedFingerprint = TryFingerprintCalibrationFolder();
            _cachedTable = _cachedFingerprint is null ? null : table;
            return table;
        }
    }

    /// <summary>Forgets the cached table (after a calibration is activated or restored).</summary>
    public static void InvalidateScanCache()
    {
        lock (Sync)
        {
            _cachedTable = null;
            _cachedFingerprint = null;
        }
    }

    /// <summary>Name, size and write time of every file in the calibration folder; null if it cannot be read.</summary>
    private static string? TryFingerprintCalibrationFolder()
    {
        try
        {
            var directory = new DirectoryInfo(ApplicationPaths.CalibrationDirectory);

            if (!directory.Exists)
                return null;

            var sb = new StringBuilder();

            foreach (FileInfo file in directory
                .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append(file.Name)
                  .Append('|').Append(file.Length)
                  .Append('|').Append(file.LastWriteTimeUtc.Ticks)
                  .Append(';');
            }

            return sb.ToString();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Validates the loaded scanner lookup table (<see cref="ScannerLookupTable.IsValid"/>)
    /// and rejects tables made by the old "estimated scanner-orientation grid" fallback.
    /// </summary>
    /// <param name="table">The scanner lookup table to validate.</param>
    /// <exception cref="InvalidDataException">Thrown if the table is null or not valid for production use.</exception>
    private static void ValidateProductionTable(
        ScannerLookupTable? table)
    {
        if (table is null || !table.IsValid)
        {
            throw new InvalidDataException(
                "Scanner calibration must contain a positive finite LengthPixelsPerMM value, " +
                "at least two distinct physical radius bands, and finite non-negative radius points.");
        }

        if (table.Points.Any(point =>
            (point.SourceName ?? string.Empty).Contains(
                "Estimated scanner-orientation grid",
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                "The installed lookup table was generated by the legacy estimated-calibration fallback and is not approved for production use.");
        }
    }

}
