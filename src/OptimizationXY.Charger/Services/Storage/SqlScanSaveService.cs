using Microsoft.Data.SqlClient;
using OptimizationXY.Charger.Models.Core;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Configuration;
using OptimizationXY.Charger.Services.Core;
using System.Globalization;

namespace OptimizationXY.Charger.Services.Storage;

/// <summary>
/// Saves the scan results to a SQL database. This service handles the insertion of scan session data, scan frames, and optimization results into the appropriate database tables. It ensures that all related data is saved within a single transaction, providing atomicity and consistency.
/// If any part of the save operation fails, the transaction is rolled back to maintain data integrity.
/// </summary>
/// <param name="settings">The database settings used to configure the connection.</param>
public sealed class SqlScanSaveService(DatabaseSettings settings)
{
    private const double MM_PER_INCH = 25.4;

    private readonly DatabaseSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));


    /// <summary>
    /// Saves the scan results from the provided PipelineState to the SQL database. This method performs the following steps:
    /// </summary>
    /// <param name="state">The pipeline state containing the scan results to be saved.</param>
    /// <returns>The ID of the inserted scan record.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the database connection string is not configured or if the last saved run folder is missing.</exception>
    /// <exception cref="AggregateException">Thrown if saving the scan fails and the SQL transaction could not be rolled back cleanly.</exception>
    public int SaveScan(PipelineState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        string connectionString =
            DatabaseConnectionStringResolver.Resolve(_settings);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database saving is enabled, but no connection string is configured.");
        }

        if (string.IsNullOrWhiteSpace(state.LastSavedRunFolder) ||
            !Directory.Exists(state.LastSavedRunFolder))
        {
            throw new InvalidOperationException(
                "LastSavedRunFolder is missing. Save the scan artifacts before saving to SQL.");
        }

        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            var folderInfo = ParseScanFolderInfo(state.LastSavedRunFolder);

            int blockId = GetOrCreateScanBlock(
                connection,
                transaction,
                folderInfo);

            int scanId = InsertScanRecord(
                connection,
                transaction,
                state,
                blockId,
                folderInfo.ScanNumber, CalculateAngleStepDegrees(state.ScanFrames));

            InsertScanFrames(connection, transaction, state, scanId);

            InsertOptimizationResults(connection, transaction, state, scanId);

            transaction.Commit();

            return scanId;
        }
        catch (Exception saveException)
        {
            try
            {
                transaction.Rollback();
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    "Saving the scan failed and the SQL transaction could not be rolled back cleanly.",
                    saveException,
                    rollbackException);
            }

            throw;
        }
    }

    /// <summary>
    /// Inserts a new scan record into the ScanSession table in the database. This method collects relevant information from the PipelineState,
    /// such as scan frames, point cloud data, and
    /// optimization results, and saves them to the database. It returns the ID of the newly inserted scan record.
    /// </summary>
    /// <param name="connection">The SQL connection to use for the operation.</param>
    /// <param name="transaction">The SQL transaction to use for the operation.</param>
    /// <param name="state">The pipeline state containing the scan results to be saved.</param>
    /// <param name="blockId">The ID of the scan block.</param>
    /// <param name="scanNumber">The scan number.</param>
    /// <param name="angleStepDegrees">The angle step in degrees.</param>
    /// <returns>The ID of the newly inserted scan record.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the scan record could not be inserted.</exception>
    private static int InsertScanRecord(
        SqlConnection connection,
        SqlTransaction transaction,
        PipelineState state,
        int blockId,
        int scanNumber, double? angleStepDegrees)
    {
        string scanName = new DirectoryInfo(state.LastSavedRunFolder).Name;

        int frameCount = state.ScanFrames?.Count ?? 0;
        int validFrameCount = state.ScanFrames?.Count(f => f.IsValid) ?? 0;
        double? lengthIn = null;
        double? averageDiameterIn = null;

        if (state.PointCloud != null && state.PointCloud.Count > 4)
        {
            double minZ = state.PointCloud.Min(p => p.Z);
            double maxZ = state.PointCloud.Max(p => p.Z);
            lengthIn = (maxZ - minZ) / MM_PER_INCH;

            double avgRadius = state.PointCloud.Average(p =>
                Math.Sqrt(p.X * p.X + p.Y * p.Y));

            averageDiameterIn = (2 * avgRadius) / MM_PER_INCH;
        }

        const string sql = @"
INSERT INTO dbo.ScanSession
(
    BlockId,
    ScanNumber,
    ScanName,
    FullFolderPath,
    FrameCount,
    ValidFrameCount,
    AngleStepDegrees,
    LogLengthIn,
    AverageDiameterIn,
    CenterlineSweepIn,
    WorstOutOfRoundIn,
    LowWoodPercent,
    SevereLowWoodPercent,
    HighSpotPercent,
    RecommendedAction,
    DiagnosticsReport
)
OUTPUT INSERTED.ScanId
VALUES
(
    @BlockId,
    @ScanNumber,
    @ScanName,
    @FullFolderPath,
    @FrameCount,
    @ValidFrameCount,
    @AngleStepDegrees,
    @LogLengthIn,
    @AverageDiameterIn,
    @CenterlineSweepIn,
    @WorstOutOfRoundIn,
    @LowWoodPercent,
    @SevereLowWoodPercent,
    @HighSpotPercent,
    @RecommendedAction,
    @DiagnosticsReport
)";

        using var command = new SqlCommand(sql, connection, transaction);

        command.Parameters.AddWithValue("@BlockId", blockId);
        command.Parameters.AddWithValue("@ScanNumber", scanNumber);

        command.Parameters.AddWithValue("@ScanName", scanName);
        command.Parameters.AddWithValue("@FullFolderPath", state.LastSavedRunFolder);

        command.Parameters.AddWithValue("@FrameCount", frameCount);
        command.Parameters.AddWithValue("@ValidFrameCount", validFrameCount);

        command.Parameters.AddWithValue(
            "@AngleStepDegrees",
            ToDbNull(angleStepDegrees));

        command.Parameters.AddWithValue("@LogLengthIn", ToDbNull(lengthIn));
        command.Parameters.AddWithValue("@AverageDiameterIn", ToDbNull(averageDiameterIn));

        command.Parameters.AddWithValue(
            "@CenterlineSweepIn",
            state.CenterlineSweep != null
                ? state.CenterlineSweep.MaxBowSweepMM / MM_PER_INCH
                : DBNull.Value);

        command.Parameters.AddWithValue(
            "@WorstOutOfRoundIn",
            state.Model.OvalityAnalysis != null
                ? state.Model.OvalityAnalysis.OvalityMM / MM_PER_INCH
                : DBNull.Value);

        int total = state.RadiusDefectMap?.Points.Count ?? 0;

        double? lowWoodPercent = total > 0
            ? state.RadiusDefectMap!.LowCount * 100.0 / total
            : null;

        double? severeLowWoodPercent = total > 0
            ? state.RadiusDefectMap!.SevereLowCount * 100.0 / total
            : null;

        double? highSpotPercent = total > 0
            ? state.RadiusDefectMap!.HighCount * 100.0 / total
            : null;

        command.Parameters.AddWithValue("@LowWoodPercent", ToDbNull(lowWoodPercent));
        command.Parameters.AddWithValue("@SevereLowWoodPercent", ToDbNull(severeLowWoodPercent));
        command.Parameters.AddWithValue("@HighSpotPercent", ToDbNull(highSpotPercent));

        command.Parameters.AddWithValue("@RecommendedAction", ToDbNull(state.FinalSpindleRecommendation?.RecommendedAction));
        command.Parameters.AddWithValue("@DiagnosticsReport", ToDbNull(state.DiagnosticsReport));

        object? result = command.ExecuteScalar();

        if (result is null || result is DBNull)
        {
            throw new InvalidOperationException(
                "The database did not return a ScanId after inserting the scan record.");
        }

        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Inserts the scan frames associated with a scan session into the ScanFrame table in the database.
    /// Each frame's details, such as angle, file path, validity, and profile point count, are saved. This method
    /// iterates through the list of scan frames in the PipelineState and inserts them one by one within the provided SQL transaction.
    /// </summary>
    /// <param name="connection">The SQL connection to use for the operation.</param>
    /// <param name="transaction">The SQL transaction within which the operation should be performed.</param>
    /// <param name="state">The pipeline state containing the scan frames to be inserted.</param>
    /// <param name="scanId">The ID of the scan to which the frames belong.</param>
    private static void InsertScanFrames(
        SqlConnection connection,
        SqlTransaction transaction,
        PipelineState state,
        int scanId)
    {
        if (state.ScanFrames == null || state.ScanFrames.Count == 0)
        {
            return;
        }

        const string sql = @"
INSERT INTO dbo.ScanFrame
(
    ScanId,
    FrameIndex,
    AngleDegrees,
    FileName,
    FullFilePath,
    IsValid,
    ProfilePointCount
)
VALUES
(
    @ScanId,
    @FrameIndex,
    @AngleDegrees,
    @FileName,
    @FullFilePath,
    @IsValid,
    @ProfilePointCount
)";

        using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@ScanId", System.Data.SqlDbType.Int);
        command.Parameters.Add("@FrameIndex", System.Data.SqlDbType.Int);
        command.Parameters.Add("@AngleDegrees", System.Data.SqlDbType.Float);
        command.Parameters.Add("@FileName", System.Data.SqlDbType.NVarChar, 260);
        command.Parameters.Add("@FullFilePath", System.Data.SqlDbType.NVarChar, -1);
        command.Parameters.Add("@IsValid", System.Data.SqlDbType.Bit);
        command.Parameters.Add("@ProfilePointCount", System.Data.SqlDbType.Int);

        for (int index = 0; index < state.ScanFrames.Count; index++)
        {
            var frame = state.ScanFrames[index];
            int sequenceIndex = index + 1;

            string persistedPath = string.IsNullOrWhiteSpace(frame.SavedImagePath)
                ? frame.ImagePath
                : frame.SavedImagePath;

            string fileName = string.IsNullOrWhiteSpace(persistedPath)
                ? $"Frame_{sequenceIndex:0000}"
                : Path.GetFileName(persistedPath) ?? $"Frame_{sequenceIndex:0000}";

            command.Parameters["@ScanId"].Value = scanId;
            command.Parameters["@FrameIndex"].Value = sequenceIndex;
            command.Parameters["@AngleDegrees"].Value = frame.AngleDeg;
            command.Parameters["@FileName"].Value = fileName;
            command.Parameters["@FullFilePath"].Value =
                string.IsNullOrWhiteSpace(persistedPath)
                    ? DBNull.Value
                    : persistedPath;
            command.Parameters["@IsValid"].Value = frame.IsValid;
            command.Parameters["@ProfilePointCount"].Value =
                frame.Profile?.Count ?? 0;

            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Inserts the optimization results associated with a scan session into the OptimizationResult table in the database.
    /// </summary>
    /// <param name="connection">The SQL connection to use for the operation.</param>
    /// <param name="transaction">The SQL transaction within which the operation should be performed.</param>
    /// <param name="state">The pipeline state containing the optimization results to be inserted.</param>
    /// <param name="scanId">The ID of the scan to which the optimization results belong.</param>
    private static void InsertOptimizationResults(
        SqlConnection connection,
        SqlTransaction transaction,
        PipelineState state,
        int scanId)
    {
        if (state.FinalSpindleRecommendation == null)
        {
            return;
        }

        var finalSetup = state.FinalSpindleRecommendation;
        const string sql = @"
INSERT INTO dbo.OptimizationResult
(
    ScanId,
    RecommendedAction,
    TailXIn,
    TailYIn,
    HeadXIn,
    HeadYIn,
    MaxSpindleMovementIn,
    UniformRiskScore,
    BaselineUniformRiskScore,
    RiskImprovementPercent,
    InternalDefectPenalty,
    InternalSearchScore,
    TestedCandidates
)
VALUES
(
    @ScanId,
    @RecommendedAction,
    @TailXIn,
    @TailYIn,
    @HeadXIn,
    @HeadYIn,
    @MaxSpindleMovementIn,
    @UniformRiskScore,
    @BaselineUniformRiskScore,
    @RiskImprovementPercent,
    @InternalDefectPenalty,
    @InternalSearchScore,
    @TestedCandidates
)";

        using var command = new SqlCommand(
            sql,
            connection,
            transaction);

        command.Parameters.AddWithValue("@ScanId", scanId);

        command.Parameters.AddWithValue("@RecommendedAction", ToDbNull(finalSetup.RecommendedAction));

        command.Parameters.AddWithValue("@TailXIn", ToDbNull(finalSetup.BestTestedTailXMM / MM_PER_INCH));
        command.Parameters.AddWithValue("@TailYIn", ToDbNull(finalSetup.BestTestedTailYMM / MM_PER_INCH));
        command.Parameters.AddWithValue("@HeadXIn", ToDbNull(finalSetup.BestTestedHeadXMM / MM_PER_INCH));
        command.Parameters.AddWithValue("@HeadYIn", ToDbNull(finalSetup.BestTestedHeadYMM / MM_PER_INCH));

        command.Parameters.AddWithValue("@MaxSpindleMovementIn", ToDbNull(finalSetup.MaxSpindleMovementMM / MM_PER_INCH));

        command.Parameters.AddWithValue("@UniformRiskScore", ToDbNull(finalSetup.RiskScore));
        command.Parameters.AddWithValue("@BaselineUniformRiskScore", ToDbNull(finalSetup.BaselineRiskScore));
        command.Parameters.AddWithValue("@RiskImprovementPercent", ToDbNull(finalSetup.RiskScoreReductionPercent));

        command.Parameters.AddWithValue("@InternalDefectPenalty", ToDbNull(finalSetup.OptimizedDefectPenalty));

        command.Parameters.AddWithValue("@InternalSearchScore", ToDbNull(finalSetup.OptimizedInternalSearchScore));

        command.Parameters.AddWithValue("@TestedCandidates", ToDbNull(finalSetup.TestedCandidates));

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Converts a given value to DBNull.Value if it is null or not a finite number (for double and float types). This is useful for
    /// preparing parameters for SQL commands, ensuring that invalid or missing values are correctly represented in the database.
    /// </summary>
    /// <param name="value">The value to be converted to DBNull.Value if it is null or not a finite number.</param>
    /// <returns>DBNull.Value if the value is null or not a finite number; otherwise, the original value.</returns>
    private static object ToDbNull(object? value) => value switch
    {
        null => DBNull.Value,
        double number when !double.IsFinite(number) => DBNull.Value,
        float number when !float.IsFinite(number) => DBNull.Value,
        _ => value
    };

    /// <summary>
    /// Calculates the median angle step in degrees from a list of ScanFrame objects. The method extracts the distinct angles from the frames, sorts them, and computes the differences between consecutive angles to find the positive steps.
    /// It then returns the median of these positive steps, which represents the typical angular increment between frames in the scan.
    /// </summary>
    /// <param name="frames">The list of ScanFrame objects from which to calculate the median angle step.</param>
    /// <returns>The median angle step in degrees, or null if it cannot be calculated.</returns>
    private static double? CalculateAngleStepDegrees(
        List<ScanFrame> frames)
    {
        if (frames == null || frames.Count < 2)
        {
            return null;
        }

        List<double> angles = [.. frames
            .Select(frame => frame.AngleDeg)
            .Where(double.IsFinite)
            .Distinct()
            .OrderBy(angle => angle)];

        if (angles.Count < 2)
        {
            return null;
        }

        List<double> positiveSteps = new(angles.Count - 1);
        for (int index = 1; index < angles.Count; index++)
        {
            double step = angles[index] - angles[index - 1];
            if (step > 0)
            {
                positiveSteps.Add(step);
            }
        }

        if (positiveSteps.Count == 0)
        {
            return null;
        }

        positiveSteps.Sort();
        int middle = positiveSteps.Count / 2;

        return positiveSteps.Count % 2 == 1
            ? positiveSteps[middle]
            : (positiveSteps[middle - 1] + positiveSteps[middle]) / 2.0;
    }

    /// <summary>
    /// Retrieves the BlockId for a given scan block based on the provided ScanFolderInfo. If the block does not exist in
    /// the database, it creates a new entry and returns the newly generated BlockId. This method ensures that each scan block
    /// is uniquely identified by its year, month, day, and block number.
    /// </summary>
    /// <param name="connection">The SQL connection to use for the operation.</param>
    /// <param name="transaction">The SQL transaction to use for the operation.</param>
    /// <param name="info">The ScanFolderInfo containing details about the scan block.</param>
    /// <returns>The BlockId of the existing or newly created scan block.</returns>
    private static int GetOrCreateScanBlock(
        SqlConnection connection,
        SqlTransaction transaction,
        ScanFolderInfo info)
    {
        string selectSql = @"
SELECT BlockId
FROM dbo.ScanBlock
WHERE ScanYear = @ScanYear
  AND ScanMonth = @ScanMonth
  AND ScanDay = @ScanDay
  AND BlockNumber = @BlockNumber;";

        using (var select = new SqlCommand(selectSql, connection, transaction))
        {
            select.Parameters.AddWithValue("@ScanYear", info.Year);
            select.Parameters.AddWithValue("@ScanMonth", info.Month);
            select.Parameters.AddWithValue("@ScanDay", info.Day);
            select.Parameters.AddWithValue("@BlockNumber", info.BlockNumber);

            object? existing = select.ExecuteScalar();

            if (existing != null && existing != DBNull.Value)
                return Convert.ToInt32(existing, CultureInfo.InvariantCulture);
        }

        string insertSql = @"
INSERT INTO dbo.ScanBlock
(
    ScanYear,
    ScanMonth,
    ScanDay,
    BlockNumber,
    BlockName,
    BlockFolderPath
)
OUTPUT INSERTED.BlockId
VALUES
(
    @ScanYear,
    @ScanMonth,
    @ScanDay,
    @BlockNumber,
    @BlockName,
    @BlockFolderPath
)";

        using var insert = new SqlCommand(insertSql, connection, transaction);

        insert.Parameters.AddWithValue("@ScanYear", info.Year);
        insert.Parameters.AddWithValue("@ScanMonth", info.Month);
        insert.Parameters.AddWithValue("@ScanDay", info.Day);
        insert.Parameters.AddWithValue("@BlockNumber", info.BlockNumber);
        insert.Parameters.AddWithValue("@BlockName", info.BlockName);
        insert.Parameters.AddWithValue("@BlockFolderPath", info.BlockFolderPath);

        return Convert.ToInt32(insert.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses the folder path to extract scan and block information, returning a ScanFolderInfo object containing the year, month, day,
    /// block number, scan number, block name, and block folder path. The method expects the folder structure to follow a specific naming convention
    /// (e.g., "Block_####" and "Scan_####") and will throw an InvalidOperationException if the expected structure is not found.
    /// </summary>
    /// <param name="folderPath">The path to the folder to parse.</param>
    /// <returns>A ScanFolderInfo object containing the parsed information.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the folder structure does not match the expected naming convention.</exception>
    private static ScanFolderInfo ParseScanFolderInfo(string folderPath)
    {
        var currentDir = new DirectoryInfo(folderPath);

        DirectoryInfo scanDir;
        DirectoryInfo blockDir;

        if (currentDir.Name.StartsWith("Scan_", StringComparison.OrdinalIgnoreCase))
        {
            scanDir = currentDir;
            blockDir = currentDir.Parent
                ?? throw new InvalidOperationException("Scan folder has no parent block folder.");
        }
        else if (currentDir.Name.StartsWith("Block_", StringComparison.OrdinalIgnoreCase))
        {
            blockDir = currentDir;

            var scanFolders = blockDir
                .GetDirectories("Scan_*")
                .OrderByDescending(d => d.Name)
                .ToList();

            scanDir = scanFolders.FirstOrDefault()
                ?? throw new InvalidOperationException("Block folder does not contain a Scan_#### folder.");
        }
        else
        {
            throw new InvalidOperationException(
                $"Expected a Block_#### or Scan_#### folder, but got: {currentDir.Name}");
        }

        var dayDir = blockDir.Parent
            ?? throw new InvalidOperationException("Block folder has no day folder.");

        var monthDir = dayDir.Parent
            ?? throw new InvalidOperationException("Day folder has no month folder.");

        var yearDir = monthDir.Parent
            ?? throw new InvalidOperationException("Month folder has no year folder.");

        return new ScanFolderInfo
        {
            Year = int.Parse(yearDir.Name, CultureInfo.InvariantCulture),
            Month = int.Parse(monthDir.Name, CultureInfo.InvariantCulture),
            Day = int.Parse(dayDir.Name, CultureInfo.InvariantCulture),

            BlockNumber = ParseTrailingNumber(blockDir.Name),
            ScanNumber = ParseTrailingNumber(scanDir.Name),

            BlockName = blockDir.Name,
            BlockFolderPath = blockDir.FullName
        };
    }

    /// <summary>
    /// Parses the trailing number from a folder name that follows the pattern "Name_####". It looks for the
    /// last underscore in the string and attempts to parse the substring after it as an integer.
    /// If the format is incorrect or the number cannot be parsed, it throws an InvalidOperationException.
    /// </summary>
    /// <param name="text">The folder name to parse.</param>
    /// <returns>The parsed integer.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the folder name does not contain a valid trailing number.</exception>
    private static int ParseTrailingNumber(string text)
    {
        int underscoreIndex = text.LastIndexOf('_');

        if (underscoreIndex < 0 || underscoreIndex == text.Length - 1)
            throw new InvalidOperationException($"Could not read number from folder name: {text}");

        return int.Parse(text[(underscoreIndex + 1)..], CultureInfo.InvariantCulture);
    }
}
