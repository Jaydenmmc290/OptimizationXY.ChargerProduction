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
