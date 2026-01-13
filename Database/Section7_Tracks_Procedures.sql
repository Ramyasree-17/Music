-- =============================================
-- Section 7 Stored Procedures (Tracks)
-- =============================================
-- These procedures encapsulate the core CRUD logic for tracks and ensure
-- release-state validation before mutating data.

IF OBJECT_ID('[dbo].[sp_CreateTrack]') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_CreateTrack];
GO

CREATE PROCEDURE [dbo].[sp_CreateTrack]
    @ReleaseId               INT,
    @TrackNumber             INT,
    @Title                   NVARCHAR(200),
    @DurationSeconds         INT            = NULL,
    @ExplicitFlag            BIT            = NULL,
    @ISRC                    NVARCHAR(50)   = NULL,
    @Language                NVARCHAR(50)   = NULL,
    @TrackVersion            NVARCHAR(100)  = NULL,
    @ArtistId                INT            = NULL,
    @AudioFileId             INT            = NULL,
    @Lyrics                  NVARCHAR(MAX)  = NULL,
    @IsExplicit              BIT            = NULL,
    @IsInstrumental          BIT            = NULL,
    @PreviewStartSeconds     INT            = NULL,
    @PreviewStartTimeSeconds INT            = NULL,
    @TrackGenre              NVARCHAR(100)  = NULL,
    @AudioUrl                NVARCHAR(500)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM Releases WHERE ReleaseId = @ReleaseId AND (Status = 'DRAFT'))
    BEGIN
        RAISERROR('Release is not editable. Track cannot be created.', 16, 1);
        RETURN;
    END;

    IF EXISTS (
        SELECT 1 FROM Tracks WHERE ReleaseId = @ReleaseId AND TrackNumber = @TrackNumber AND (IsDeleted = 0 OR IsDeleted IS NULL)
    )
    BEGIN
        RAISERROR('TrackNumber already exists for this release.', 16, 1);
        RETURN;
    END;

    INSERT INTO Tracks
    (
        ReleaseId,
        TrackNumber,
        Title,
        DurationSeconds,
        ExplicitFlag,
        ISRC,
        Language,
        TrackVersion,
        ArtistID,
        AudioFileId,
        Lyrics,
        IsExplicit,
        IsInstrumental,
        PreviewStartSeconds,
        PreviewStartTimeSeconds,
        TrackGenre,
        AudioUrl,
        Status,
        CreatedAt,
        IsDeleted
    )
    VALUES
    (
        @ReleaseId,
        @TrackNumber,
        @Title,
        @DurationSeconds,
        @ExplicitFlag,
        @ISRC,
        @Language,
        @TrackVersion,
        @ArtistId,
        @AudioFileId,
        @Lyrics,
        @IsExplicit,
        @IsInstrumental,
        @PreviewStartSeconds,
        @PreviewStartTimeSeconds,
        @TrackGenre,
        @AudioUrl,
        'Active',
        SYSUTCDATETIME(),
        0
    );

    SELECT SCOPE_IDENTITY() AS TrackId;
END;
GO

IF OBJECT_ID('[dbo].[sp_UpdateTrack]') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_UpdateTrack];
GO

CREATE PROCEDURE [dbo].[sp_UpdateTrack]
    @TrackId                 INT,
    @Title                   NVARCHAR(200)  = NULL,
    @DurationSeconds         INT            = NULL,
    @ExplicitFlag            BIT            = NULL,
    @ISRC                    NVARCHAR(50)   = NULL,
    @Language                NVARCHAR(50)   = NULL,
    @TrackVersion            NVARCHAR(100)  = NULL,
    @ArtistId                INT            = NULL,
    @Lyrics                  NVARCHAR(MAX)  = NULL,
    @IsExplicit              BIT            = NULL,
    @IsInstrumental          BIT            = NULL,
    @PreviewStartSeconds     INT            = NULL,
    @PreviewStartTimeSeconds INT            = NULL,
    @TrackGenre              NVARCHAR(100)  = NULL,
    @AudioUrl                NVARCHAR(500)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ReleaseId INT;

    SELECT TOP 1
        @ReleaseId = ReleaseId
    FROM Tracks
    WHERE TrackId = @TrackId AND (IsDeleted = 0 OR IsDeleted IS NULL);

    IF @ReleaseId IS NULL
    BEGIN
        RAISERROR('Track not found.', 16, 1);
        RETURN;
    END;

    IF NOT EXISTS (SELECT 1 FROM Releases WHERE ReleaseId = @ReleaseId AND Status = 'DRAFT')
    BEGIN
        RAISERROR('Release is locked. Track cannot be updated.', 16, 1);
        RETURN;
    END;

    IF @ISRC IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM Tracks WHERE ISRC = @ISRC AND TrackId <> @TrackId AND (IsDeleted = 0 OR IsDeleted IS NULL))
        BEGIN
            RAISERROR('ISRC already exists on another track.', 16, 1);
            RETURN;
        END;
    END;

    UPDATE Tracks
    SET
        Title = COALESCE(@Title, Title),
        DurationSeconds = COALESCE(@DurationSeconds, DurationSeconds),
        ExplicitFlag = COALESCE(@ExplicitFlag, ExplicitFlag),
        ISRC = COALESCE(@ISRC, ISRC),
        Language = COALESCE(@Language, Language),
        TrackVersion = COALESCE(@TrackVersion, TrackVersion),
        ArtistID = COALESCE(@ArtistId, ArtistID),
        Lyrics = COALESCE(@Lyrics, Lyrics),
        IsExplicit = COALESCE(@IsExplicit, IsExplicit),
        IsInstrumental = COALESCE(@IsInstrumental, IsInstrumental),
        PreviewStartSeconds = COALESCE(@PreviewStartSeconds, PreviewStartSeconds),
        PreviewStartTimeSeconds = COALESCE(@PreviewStartTimeSeconds, PreviewStartTimeSeconds),
        TrackGenre = COALESCE(@TrackGenre, TrackGenre),
        AudioUrl = COALESCE(@AudioUrl, AudioUrl),
        UpdatedAt = SYSUTCDATETIME()
    WHERE TrackId = @TrackId;
END;
GO

IF OBJECT_ID('[dbo].[sp_DeleteTrack]') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_DeleteTrack];
GO

CREATE PROCEDURE [dbo].[sp_DeleteTrack]
    @TrackId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ReleaseId INT;
    SELECT TOP 1 @ReleaseId = ReleaseId FROM Tracks WHERE TrackId = @TrackId AND (IsDeleted = 0 OR IsDeleted IS NULL);

    IF @ReleaseId IS NULL
    BEGIN
        RAISERROR('Track not found or already deleted.', 16, 1);
        RETURN;
    END;

    IF NOT EXISTS (SELECT 1 FROM Releases WHERE ReleaseId = @ReleaseId AND Status = 'DRAFT')
    BEGIN
        RAISERROR('Release is not in DRAFT state. Track cannot be deleted.', 16, 1);
        RETURN;
    END;

    UPDATE Tracks
    SET
        IsDeleted = 1,
        DeletedAt = SYSUTCDATETIME()
    WHERE TrackId = @TrackId;

    DELETE FROM ReleaseContributors WHERE TrackId = @TrackId;

    UPDATE Files
    SET TrackId = NULL
    WHERE TrackId = @TrackId;
END;
GO

IF OBJECT_ID('[dbo].[sp_AttachTrackAudio]') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_AttachTrackAudio];
GO

CREATE PROCEDURE [dbo].[sp_AttachTrackAudio]
    @TrackId    INT,
    @FileId     INT,
    @UserId     INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ReleaseId INT;
    SELECT @ReleaseId = ReleaseId FROM Tracks WHERE TrackId = @TrackId AND (IsDeleted = 0 OR IsDeleted IS NULL);

    IF @ReleaseId IS NULL
    BEGIN
        RAISERROR('Track not found.', 16, 1);
        RETURN;
    END;

    IF NOT EXISTS (SELECT 1 FROM Files WHERE FileId = @FileId AND (Status = 'AVAILABLE' OR Status = 'VERIFYING'))
    BEGIN
        RAISERROR('File does not exist or is not available.', 16, 1);
        RETURN;
    END;

    DECLARE @PreviousFileId INT;
    SELECT @PreviousFileId = AudioFileId FROM Tracks WHERE TrackId = @TrackId;

    -- Note: Some schemas may not have UpdatedAt columns on Tracks/Files.
    -- To keep this script compatible, we only update columns that are widely present.
    UPDATE Tracks
    SET AudioFileId = @FileId
    WHERE TrackId = @TrackId;

    UPDATE Files
    SET TrackId = @TrackId
    WHERE FileId = @FileId;

    IF @PreviousFileId IS NOT NULL AND @PreviousFileId <> @FileId
    BEGIN
        UPDATE Files
        SET Status = 'REPLACED',
            ReplacedByFileId = @FileId
        WHERE FileId = @PreviousFileId;
    END;
END;
GO

