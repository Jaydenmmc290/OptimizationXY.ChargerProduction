using System.Globalization;
using Microsoft.Data.SqlClient;

namespace OptimizationXY.Charger.Services.Configuration;

/// <summary>
/// Provides methods to set up and validate the SQL Server database schema for the OptimizationXY application.
/// </summary>
public sealed class SqlDatabaseSetupService
{
    public const int ExpectedSchemaVersion = 1;
    public const string DefaultConnectionString =
        "Server=.\\SQLEXPRESS;Database=OptimizationXY;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10;";


    /// <summary>
    /// Tests the SQL Server connection and retrieves information about the server, database, and schema version.
    /// </summary>
    /// <param name="connectionString">The connection string to the SQL Server database.</param>
    /// <returns>A <see cref="DatabaseSetupResult"/> containing information about the server, database, and schema version.</returns>
    public static DatabaseSetupResult TestConnection(string connectionString)
    {
        SqlConnectionStringBuilder targetBuilder = ValidateAndNormalize(
            connectionString,
            requireDatabase: false);

        string targetDatabase = string.IsNullOrWhiteSpace(targetBuilder.InitialCatalog)
            ? "OptimizationXY"
            : targetBuilder.InitialCatalog;

        SqlConnectionStringBuilder masterBuilder = new(targetBuilder.ConnectionString)
        {
            InitialCatalog = "master"
        };

        string machine;
        string instance;
        string version;
        string identity;
        bool databaseExists;

        using (var masterConnection = new SqlConnection(masterBuilder.ConnectionString))
        {
            masterConnection.Open();

            using var command = new SqlCommand(
                "SELECT CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)), " +
                "CAST(SERVERPROPERTY('InstanceName') AS nvarchar(128)), " +
                "CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)), " +
                "SUSER_SNAME(), CASE WHEN DB_ID(@DatabaseName) IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END;",
                masterConnection);
            command.Parameters.AddWithValue("@DatabaseName", targetDatabase);

            using SqlDataReader reader = command.ExecuteReader();
            reader.Read();

            machine = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            instance = reader.IsDBNull(1) ? "MSSQLSERVER" : reader.GetString(1);
            version = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            identity = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            databaseExists = reader.GetBoolean(4);
        }

        int? schemaVersion = null;
        if (databaseExists)
        {
            targetBuilder.InitialCatalog = targetDatabase;
            using var targetConnection = new SqlConnection(targetBuilder.ConnectionString);
            targetConnection.Open();
            schemaVersion = TryReadSchemaVersion(targetConnection);
        }

        return new DatabaseSetupResult
        {
            Success = true,
            ServerDescription = $"{machine}\\{instance}",
            SqlVersion = version,
            LoginIdentity = identity,
            DatabaseName = targetDatabase,
            SchemaVersion = schemaVersion
        };
    }
    
    /// <summary>
    /// Creates or upgrades the SQL Server database schema for the OptimizationXY application.
    /// </summary>
    /// <param name="connectionString">The connection string to the SQL Server database.</param>
    /// <returns>A <see cref="DatabaseSetupResult"/> containing information about the server, database, and schema version.</returns>
    public static DatabaseSetupResult CreateOrUpgrade(string connectionString)
    {
        SqlConnectionStringBuilder targetBuilder = ValidateAndNormalize(
            connectionString,
            requireDatabase: true);

        string databaseName = targetBuilder.InitialCatalog;

        SqlConnectionStringBuilder masterBuilder = new(targetBuilder.ConnectionString)
        {
            InitialCatalog = "master"
        };

        using (var masterConnection = new SqlConnection(masterBuilder.ConnectionString))
        {
            masterConnection.Open();

            const string createDatabaseSql = """
IF DB_ID(@DatabaseName) IS NULL
BEGIN
    DECLARE @Sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@DatabaseName) + N';';
    EXEC sys.sp_executesql @Sql;
END;
""";

            using var command = new SqlCommand(createDatabaseSql, masterConnection);
            command.Parameters.AddWithValue("@DatabaseName", databaseName);
            command.ExecuteNonQuery();
        }

        using var connection = new SqlConnection(targetBuilder.ConnectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            using (var schemaCommand = new SqlCommand(
                DatabaseSchemaDefinition.Script,
                connection,
                transaction))
            {
                schemaCommand.CommandTimeout = 60;
                schemaCommand.ExecuteNonQuery();
            }

            const string grantSql = """
DECLARE @Login sysname = SUSER_SNAME();
IF @Login IS NULL OR LEN(@Login) = 0
    THROW 51000, 'Unable to resolve the current Windows login.', 1;

-- The login that creates a local SQL Express database commonly maps to dbo.
-- In that case it already has full database access and CREATE USER would fail
-- because the login is already represented by dbo under a different name.
IF USER_NAME() <> N'dbo'
BEGIN
    IF DATABASE_PRINCIPAL_ID(@Login) IS NULL
    BEGIN
        DECLARE @CreateUser nvarchar(max) =
            N'CREATE USER ' + QUOTENAME(@Login) + N' FOR LOGIN ' + QUOTENAME(@Login) + N';';
        EXEC sys.sp_executesql @CreateUser;
    END;

    DECLARE @ReaderSql nvarchar(max) =
        N'ALTER ROLE [db_datareader] ADD MEMBER ' + QUOTENAME(@Login) + N';';
    DECLARE @WriterSql nvarchar(max) =
        N'ALTER ROLE [db_datawriter] ADD MEMBER ' + QUOTENAME(@Login) + N';';

    IF IS_ROLEMEMBER(N'db_datareader', @Login) <> 1
        EXEC sys.sp_executesql @ReaderSql;
    IF IS_ROLEMEMBER(N'db_datawriter', @Login) <> 1
        EXEC sys.sp_executesql @WriterSql;
END;
""";

            using (var grantCommand = new SqlCommand(
                grantSql,
                connection,
                transaction))
            {
                grantCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        DatabaseSetupResult result = TestConnection(
            targetBuilder.ConnectionString);

        if (result.SchemaVersion != ExpectedSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Database schema setup completed, but version {result.SchemaVersion?.ToString(CultureInfo.InvariantCulture) ?? "none"} was found instead of {ExpectedSchemaVersion}.");
        }

        return result;
    }

    /// <summary>
    /// Validates the SQL Server database schema for the OptimizationXY application.
    /// </summary>
    /// <param name="connectionString">The connection string to the SQL Server database.</param>
    /// <returns>The schema version if it matches the expected version; otherwise, an exception is thrown.</returns>
    public static int? ValidateSchema(string connectionString)
    {
        SqlConnectionStringBuilder builder = ValidateAndNormalize(
            connectionString,
            requireDatabase: true);

        using var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();

        int? version = TryReadSchemaVersion(connection);

        if (version != ExpectedSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Database schema version is {version?.ToString(CultureInfo.InvariantCulture) ?? "missing"}; expected {ExpectedSchemaVersion}.");
        }

        return version;
    }

    /// <summary>
    /// Validates and normalizes the provided SQL Server connection string.
    /// </summary>
    /// <param name="connectionString">The connection string to validate and normalize.</param>
    /// <param name="requireDatabase">Whether a database must be specified in the connection string.</param>
    /// <returns>A <see cref="SqlConnectionStringBuilder"/> representing the validated and normalized connection string.</returns>
    /// <exception cref="ArgumentException">Thrown when the connection string is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the connection string is missing required components or does not meet security requirements.</exception>
    private static SqlConnectionStringBuilder ValidateAndNormalize(
        string connectionString,
        bool requireDatabase)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "A SQL Server connection string is required.",
                nameof(connectionString));
        }

        var builder = new SqlConnectionStringBuilder(connectionString.Trim());

        if (string.IsNullOrWhiteSpace(builder.DataSource))
        {
            throw new InvalidOperationException(
                "The SQL connection string must specify Server/Data Source.");
        }

        if (requireDatabase && string.IsNullOrWhiteSpace(builder.InitialCatalog))
        {
            throw new InvalidOperationException(
                "The SQL connection string must specify Database/Initial Catalog.");
        }

        if (!builder.IntegratedSecurity)
        {
            throw new InvalidOperationException(
                "Production setup requires Windows Integrated Security. SQL usernames and passwords are not stored by this wizard.");
        }

        builder["Encrypt"] = true;
        builder["TrustServerCertificate"] = true;
        builder.ConnectTimeout = Math.Clamp(builder.ConnectTimeout, 5, 30);

        return builder;
    }

    /// <summary>
    /// Attempts to read the schema version from the database. Returns null if the SchemaVersion table does not exist or if no version is found.
    /// </summary>
    /// <param name="connection">The open SQL connection to use for the query.</param>
    /// <returns>The schema version number, or null if the SchemaVersion table does not exist or no version is found.</returns>
    private static int? TryReadSchemaVersion(SqlConnection connection)
    {
        const string sql = """
IF OBJECT_ID(N'dbo.SchemaVersion', N'U') IS NULL
    SELECT CAST(NULL AS int);
ELSE
    SELECT MAX(VersionNumber) FROM dbo.SchemaVersion;
""";

        using var command = new SqlCommand(sql, connection);
        object? value = command.ExecuteScalar();

        return value is null or DBNull
            ? null
            : Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Represents the result of a database setup operation, including success status, server description, SQL version,
/// login identity, database name, and schema version.
/// </summary>
public sealed class DatabaseSetupResult
{
    public bool Success { get; set; }
    public string ServerDescription { get; set; } = string.Empty;
    public string SqlVersion { get; set; } = string.Empty;
    public string LoginIdentity { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public int? SchemaVersion { get; set; }
}

internal static class DatabaseSchemaDefinition
{
    public const string Script = """
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.SchemaVersion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaVersion
    (
        VersionNumber INT NOT NULL CONSTRAINT PK_SchemaVersion PRIMARY KEY,
        VersionName NVARCHAR(100) NOT NULL,
        AppliedUtc DATETIME2(3) NOT NULL
            CONSTRAINT DF_SchemaVersion_AppliedUtc DEFAULT SYSUTCDATETIME()
    );
END;

IF OBJECT_ID(N'dbo.ScanBlock', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScanBlock
    (
        BlockId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScanBlock PRIMARY KEY,
        ScanYear INT NOT NULL,
        ScanMonth INT NOT NULL,
        ScanDay INT NOT NULL,
        BlockNumber INT NOT NULL,
        BlockName NVARCHAR(100) NOT NULL,
        BlockFolderPath NVARCHAR(MAX) NOT NULL,
        CreatedUtc DATETIME2(3) NOT NULL
            CONSTRAINT DF_ScanBlock_CreatedUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_ScanBlock_Date_Block
            UNIQUE (ScanYear, ScanMonth, ScanDay, BlockNumber)
    );
END;

IF OBJECT_ID(N'dbo.ScanSession', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScanSession
    (
        ScanId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScanSession PRIMARY KEY,
        BlockId INT NOT NULL,
        ScanNumber INT NOT NULL,
        ScanName NVARCHAR(100) NOT NULL,
        FullFolderPath NVARCHAR(MAX) NOT NULL,
        FrameCount INT NOT NULL,
        ValidFrameCount INT NOT NULL,
        AngleStepDegrees FLOAT NULL,
        LogLengthIn FLOAT NULL,
        AverageDiameterIn FLOAT NULL,
        CenterlineSweepIn FLOAT NULL,
        WorstOutOfRoundIn FLOAT NULL,
        LowWoodPercent FLOAT NULL,
        SevereLowWoodPercent FLOAT NULL,
        HighSpotPercent FLOAT NULL,
        RecommendedAction NVARCHAR(500) NULL,
        DiagnosticsReport NVARCHAR(MAX) NULL,
        CreatedUtc DATETIME2(3) NOT NULL
            CONSTRAINT DF_ScanSession_CreatedUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_ScanSession_ScanBlock
            FOREIGN KEY (BlockId) REFERENCES dbo.ScanBlock(BlockId),
        CONSTRAINT UQ_ScanSession_Block_Scan
            UNIQUE (BlockId, ScanNumber)
    );
END;

IF OBJECT_ID(N'dbo.ScanFrame', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScanFrame
    (
        ScanFrameId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScanFrame PRIMARY KEY,
        ScanId INT NOT NULL,
        FrameIndex INT NOT NULL,
        AngleDegrees FLOAT NOT NULL,
        FileName NVARCHAR(260) NOT NULL,
        FullFilePath NVARCHAR(MAX) NULL,
        IsValid BIT NOT NULL,
        ProfilePointCount INT NOT NULL,
        CONSTRAINT FK_ScanFrame_ScanSession
            FOREIGN KEY (ScanId) REFERENCES dbo.ScanSession(ScanId) ON DELETE CASCADE,
        CONSTRAINT UQ_ScanFrame_Scan_Index
            UNIQUE (ScanId, FrameIndex)
    );
END;

IF OBJECT_ID(N'dbo.OptimizationResult', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OptimizationResult
    (
        OptimizationResultId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_OptimizationResult PRIMARY KEY,
        ScanId INT NOT NULL,
        RecommendedAction NVARCHAR(500) NULL,
        TailXIn FLOAT NULL,
        TailYIn FLOAT NULL,
        HeadXIn FLOAT NULL,
        HeadYIn FLOAT NULL,
        MaxSpindleMovementIn FLOAT NULL,
        UniformRiskScore FLOAT NULL,
        BaselineUniformRiskScore FLOAT NULL,
        RiskImprovementPercent FLOAT NULL,
        InternalDefectPenalty FLOAT NULL,
        InternalSearchScore FLOAT NULL,
        TestedCandidates INT NULL,
        CreatedUtc DATETIME2(3) NOT NULL
            CONSTRAINT DF_OptimizationResult_CreatedUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_OptimizationResult_ScanSession
            FOREIGN KEY (ScanId) REFERENCES dbo.ScanSession(ScanId) ON DELETE CASCADE,
        CONSTRAINT UQ_OptimizationResult_Scan UNIQUE (ScanId)
    );
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ScanSession_CreatedUtc'
      AND object_id = OBJECT_ID(N'dbo.ScanSession')
)
BEGIN
    CREATE INDEX IX_ScanSession_CreatedUtc
        ON dbo.ScanSession(CreatedUtc DESC);
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_ScanFrame_ScanId'
      AND object_id = OBJECT_ID(N'dbo.ScanFrame')
)
BEGIN
    CREATE INDEX IX_ScanFrame_ScanId
        ON dbo.ScanFrame(ScanId);
END;

MERGE dbo.SchemaVersion AS target
USING (SELECT 1 AS VersionNumber, N'Initial Production Schema' AS VersionName) AS source
ON target.VersionNumber = source.VersionNumber
WHEN NOT MATCHED THEN
    INSERT (VersionNumber, VersionName)
    VALUES (source.VersionNumber, source.VersionName);
""";
}
