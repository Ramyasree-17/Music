-- Fix: sp_CreateArtist was inserting 'Unclaimed' (varchar) into ClaimStatus (tinyint)
-- Run this in your SQL Server database.
--
-- Suggested mapping:
--   0 = Unclaimed
--   1 = Claimed
--   2 = Rejected (optional)
--
-- Adjust mapping as per your application rules / constraints.

IF OBJECT_ID('dbo.sp_CreateArtist', 'P') IS NULL
BEGIN
    RAISERROR('dbo.sp_CreateArtist not found', 16, 1);
    RETURN;
END
GO

ALTER PROCEDURE dbo.sp_CreateArtist
(
    @ArtistName NVARCHAR(150) = NULL,
    @PublicProfileName NVARCHAR(150) = NULL,
    @Country NVARCHAR(100) = NULL,
    @Genre NVARCHAR(200) = NULL,
    @Bio NVARCHAR(MAX) = NULL,
    @ImageUrl NVARCHAR(4000) = NULL,
    @DateOfBirth DATETIME2 = NULL,
    @Email NVARCHAR(200) = NULL,
    @LabelId INT = NULL,
    @SoundCloudUrl NVARCHAR(500) = NULL,
    @SpotifyUrl NVARCHAR(500) = NULL,
    @AppleMusicUrl NVARCHAR(500) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Artists
    (
        ArtistName,
        PublicProfileName,
        Country,
        Genre,
        Bio,
        ImageUrl,
        DateOfBirth,
        Email,
        LabelId,
        SoundCloudUrl,
        SpotifyUrl,
        AppleMusicUrl,
        CreatedAt,
        ClaimStatus,
        IsDefaultForUser
    )
    VALUES
    (
        @ArtistName,
        @PublicProfileName,
        @Country,
        @Genre,
        @Bio,
        @ImageUrl,
        @DateOfBirth,
        @Email,
        @LabelId,
        @SoundCloudUrl,
        @SpotifyUrl,
        @AppleMusicUrl,
        GETDATE(),
        0,   -- Unclaimed (tinyint)
        0
    );

    SELECT SCOPE_IDENTITY() AS ArtistId;
END
GO




