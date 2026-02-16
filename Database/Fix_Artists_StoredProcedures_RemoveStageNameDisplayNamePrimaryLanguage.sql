-- Fix stored procedures to remove StageName, DisplayName, PrimaryLanguage
-- and add SoundCloudUrl, SpotifyUrl, AppleMusicUrl

-- Fix sp_GetAllArtists
IF OBJECT_ID('dbo.sp_GetAllArtists', 'P') IS NOT NULL
BEGIN
    DROP PROCEDURE dbo.sp_GetAllArtists;
END
GO

CREATE PROCEDURE dbo.sp_GetAllArtists
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        ArtistID,
        ArtistName,
        PublicProfileName,
        Bio,
        ImageUrl,
        DateOfBirth,
        Country,
        Genre,
        LabelId,
        Status,
        SoundCloudUrl,
        SpotifyUrl,
        AppleMusicUrl,
        CreatedAt
    FROM Artists
    ORDER BY CreatedAt DESC;
END
GO

-- Fix sp_GetArtistById
IF OBJECT_ID('dbo.sp_GetArtistById', 'P') IS NOT NULL
BEGIN
    DROP PROCEDURE dbo.sp_GetArtistById;
END
GO

CREATE PROCEDURE dbo.sp_GetArtistById
    @ArtistId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        ArtistID,
        ArtistName,
        PublicProfileName,
        Bio,
        ImageUrl,
        DateOfBirth,
        Country,
        Genre,
        LabelId,
        Status,
        SoundCloudUrl,
        SpotifyUrl,
        AppleMusicUrl,
        CreatedAt
    FROM Artists
    WHERE ArtistID = @ArtistId;
END
GO

-- Fix sp_UpdateArtist
IF OBJECT_ID('dbo.sp_UpdateArtist', 'P') IS NOT NULL
BEGIN
    DROP PROCEDURE dbo.sp_UpdateArtist;
END
GO

CREATE PROCEDURE dbo.sp_UpdateArtist
    @ArtistId INT,
    @ArtistName NVARCHAR(150) = NULL,
    @PublicProfileName NVARCHAR(150) = NULL,
    @Bio NVARCHAR(MAX) = NULL,
    @ImageUrl NVARCHAR(4000) = NULL,
    @DateOfBirth DATETIME2 = NULL,
    @Country NVARCHAR(100) = NULL,
    @Genre NVARCHAR(200) = NULL,
    @SoundCloudUrl NVARCHAR(500) = NULL,
    @SpotifyUrl NVARCHAR(500) = NULL,
    @AppleMusicUrl NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE Artists
    SET 
        ArtistName = CASE WHEN @ArtistName IS NOT NULL THEN @ArtistName ELSE ArtistName END,
        PublicProfileName = CASE WHEN @PublicProfileName IS NOT NULL THEN @PublicProfileName ELSE PublicProfileName END,
        Bio = CASE WHEN @Bio IS NOT NULL THEN @Bio ELSE Bio END,
        ImageUrl = CASE WHEN @ImageUrl IS NOT NULL THEN @ImageUrl ELSE ImageUrl END,
        DateOfBirth = CASE WHEN @DateOfBirth IS NOT NULL THEN @DateOfBirth ELSE DateOfBirth END,
        Country = CASE WHEN @Country IS NOT NULL THEN @Country ELSE Country END,
        Genre = CASE WHEN @Genre IS NOT NULL THEN @Genre ELSE Genre END,
        SoundCloudUrl = CASE WHEN @SoundCloudUrl IS NOT NULL THEN @SoundCloudUrl ELSE SoundCloudUrl END,
        SpotifyUrl = CASE WHEN @SpotifyUrl IS NOT NULL THEN @SpotifyUrl ELSE SpotifyUrl END,
        AppleMusicUrl = CASE WHEN @AppleMusicUrl IS NOT NULL THEN @AppleMusicUrl ELSE AppleMusicUrl END
    WHERE ArtistID = @ArtistId;
    
    SELECT @@ROWCOUNT;
END
GO

PRINT 'Stored procedures sp_GetAllArtists, sp_GetArtistById, and sp_UpdateArtist have been updated.';

