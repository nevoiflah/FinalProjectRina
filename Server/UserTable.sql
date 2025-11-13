-- ============================================
-- SQL Script: Create Users Table
-- Database: igroup117_test2
-- Table: NLA_Users
-- ============================================

-- Drop table if exists (for clean setup)
IF OBJECT_ID('dbo.NLA_Users', 'U') IS NOT NULL
    DROP TABLE dbo.NLA_Users;
GO

-- Create Users table
CREATE TABLE dbo.NLA_Users (
    UserId NVARCHAR(50) PRIMARY KEY,                    -- Unique identifier (GUID)
    Name NVARCHAR(100) NOT NULL,                        -- User's full name
    Email NVARCHAR(255) NOT NULL UNIQUE,                -- User's email (unique)
    Organization NVARCHAR(200) NOT NULL,                -- Company/team name
    PasswordHash NVARCHAR(255) NOT NULL,                -- Hashed password
    IsAdmin BIT NOT NULL DEFAULT 0,                     -- Admin flag (0 = No, 1 = Yes)
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),  -- Registration timestamp
    LastLoginAt DATETIME2 NULL,                         -- Last login timestamp
    IsActive BIT NOT NULL DEFAULT 1                     -- Account status (1 = Active, 0 = Inactive)
);
GO

-- Create indexes for better query performance
CREATE NONCLUSTERED INDEX IX_NLA_Users_Email 
    ON dbo.NLA_Users(Email);
GO

CREATE NONCLUSTERED INDEX IX_NLA_Users_CreatedAt 
    ON dbo.NLA_Users(CreatedAt DESC);
GO

CREATE NONCLUSTERED INDEX IX_NLA_Users_IsAdmin 
    ON dbo.NLA_Users(IsAdmin) 
    WHERE IsAdmin = 1;
GO

-- Insert a default admin user (optional)
-- Password: Admin123! (This is the SHA256 hash)
INSERT INTO dbo.NLA_Users (UserId, Name, Email, Organization, PasswordHash, IsAdmin, CreatedAt)
VALUES (
    NEWID(),
    'System Administrator',
    'admin@example.com',
    'System',
    '8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918', -- SHA256 hash of 'admin'
    1,
    GETUTCDATE()
);
GO

-- Verify table creation
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'NLA_Users'
ORDER BY ORDINAL_POSITION;
GO

-- Display sample data
SELECT * FROM dbo.NLA_Users;
GO

PRINT 'NLA_Users table created successfully!';
GO