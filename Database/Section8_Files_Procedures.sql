-- =============================================
-- Section 8 Stored Procedures (Files)
-- =============================================
-- These procedures manage file upload lifecycle metadata.

IF OBJECT_ID('[dbo].[sp_FileRequestUpload]') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_FileRequestUpload];
GO

CREATE PROCEDURE [dbo].[sp_FileRequestUpload]
    @ReleaseId      INT            = NULL,
    @TrackId        INT            = NULL,
    @FileTypeId     TINYINT,
    @FileName       NVARCHAR(260),
    @ContentType    NVARCHAR(100),
    @ExpectedSize   BIGINT         = NULL,
    @CreatedBy      INT            = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @TrackId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Tracks WHERE TrackId = @TrackId AND (IsDeleted = 0 OR IsDeleted IS NULL))
    BEGIN
        RAISERROR('Track not found.', 16, 1);
        RETURN;
    END;

    IF @ReleaseId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Releases WHERE ReleaseId = @ReleaseId)
    BEGIN
        RAISERROR('Release not found.', 16, 1);
        RETURN;
    END;

    DECLARE @S3Key NVARCHAR(1000) =
        CONCAT(
            'labels/',
            COALESCE((SELECT LabelId FROM Releases WHERE ReleaseId = @ReleaseId), 0),
            '/releases/',
            COALESCE(@ReleaseId, 0),
            '/tracks/',
            COALESCE(@TrackId, 0),
            '/',
            CONVERT(NVARCHAR(40), NEWID()),
            '_',
            @FileName
        );

    INSERT INTO Files
    (
        ReleaseId,
        TrackId,
        FileTypeId,
        S3Key,
        CloudfrontUrl,
        BackupUrl,
        Checksum,
        FileSizeBytes,
        Status,
        CreatedByUserId,
        CreatedAt
    )
    VALUES
    (
        @ReleaseId,
        @TrackId,
        @FileTypeId,
        @S3Key,
        NULL,
        NULL,
        NULL,
        @ExpectedSize,
        'UPLOADING',
        @CreatedBy,
        SYSUTCDATETIME()
    );

    SELECT
        FileId,
        @S3Key AS S3Key
    FROM Files
    WHERE FileId = SCOPE_IDENTITY();
END;
GO

IF OBJECT_ID('[dbo].[sp_FileCompleteUpload]') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_FileCompleteUpload];
GO

CREATE PROCEDURE [dbo].[sp_FileCompleteUpload]
    @FileId         INT,
    @Checksum       NVARCHAR(128),
    @FileSize       BIGINT,
    @CloudfrontUrl  NVARCHAR(1000) = NULL,
    @BackupUrl      NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM Files WHERE FileId = @FileId)
    BEGIN
        RAISERROR('File not found.', 16, 1);
        RETURN;
    END;

    -- Validate status is UPLOADING or VERIFYING (Section 8.2)
    DECLARE @CurrentStatus NVARCHAR(50);
    SELECT @CurrentStatus = Status FROM Files WHERE FileId = @FileId;
    
    IF @CurrentStatus NOT IN ('UPLOADING', 'VERIFYING')
    BEGIN
        RAISERROR('File already completed or in wrong state. Current status: %s', 16, 1, @CurrentStatus);
        RETURN;
    END;

    UPDATE Files
    SET
        Status = 'AVAILABLE',
        Checksum = @Checksum,
        FileSizeBytes = @FileSize,
        CloudfrontUrl = COALESCE(@CloudfrontUrl, CloudfrontUrl),
        BackupUrl = COALESCE(@BackupUrl, BackupUrl)
    WHERE FileId = @FileId;
END;
GO

IF OBJECT_ID('[dbo].[sp_FileGetStatus]') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_FileGetStatus];
GO

CREATE PROCEDURE [dbo].[sp_FileGetStatus]
    @FileId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        FileId,
        ReleaseId,
        TrackId,
        FileTypeId,
        S3Key,
        CloudfrontUrl,
        BackupUrl,
        Checksum,
        FileSizeBytes,
        Status,
        CreatedAt
    FROM Files
    WHERE FileId = @FileId;
END;
GO

IF OBJECT_ID('[dbo].[sp_FileReplace]') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_FileReplace];
GO

CREATE PROCEDURE [dbo].[sp_FileReplace]
    @OldFileId      INT,
    @NewFileId      INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TrackId INT;
    SELECT @TrackId = TrackId FROM Files WHERE FileId = @OldFileId;

    IF @TrackId IS NULL
    BEGIN
        RAISERROR('Original file not associated to a track.', 16, 1);
        RETURN;
    END;

    UPDATE Files
    SET Status = 'REPLACED',
        ReplacedByFileId = @NewFileId
    WHERE FileId = @OldFileId;

    UPDATE Files
    SET TrackId = @TrackId
    WHERE FileId = @NewFileId;

    UPDATE Tracks
    SET AudioFileId = @NewFileId,
        ModifiedAt = SYSUTCDATETIME()
    WHERE TrackId = @TrackId;
END;
GO

