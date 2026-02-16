-- Add social media URL fields to Artists table
-- Run this in your SQL Server database.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Artists') AND name = 'SoundCloudUrl')
BEGIN
    ALTER TABLE dbo.Artists
    ADD SoundCloudUrl NVARCHAR(500) NULL;
    PRINT 'Added SoundCloudUrl column to Artists table';
END
ELSE
BEGIN
    PRINT 'SoundCloudUrl column already exists';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Artists') AND name = 'SpotifyUrl')
BEGIN
    ALTER TABLE dbo.Artists
    ADD SpotifyUrl NVARCHAR(500) NULL;
    PRINT 'Added SpotifyUrl column to Artists table';
END
ELSE
BEGIN
    PRINT 'SpotifyUrl column already exists';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Artists') AND name = 'AppleMusicUrl')
BEGIN
    ALTER TABLE dbo.Artists
    ADD AppleMusicUrl NVARCHAR(500) NULL;
    PRINT 'Added AppleMusicUrl column to Artists table';
END
ELSE
BEGIN
    PRINT 'AppleMusicUrl column already exists';
END
GO






