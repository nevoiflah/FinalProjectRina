-- ============================================
-- SQL Script: Create Chat Sessions Table
-- Database: igroup117_test2
-- Table: NLA_ChatSessions
-- ============================================

-- Drop table if exists (for clean setup)
IF OBJECT_ID('dbo.NLA_ChatSessions', 'U') IS NOT NULL
    DROP TABLE dbo.NLA_ChatSessions;
GO

-- Create Chat Sessions table
CREATE TABLE dbo.NLA_ChatSessions (
    SessionId INT IDENTITY(1,1) PRIMARY KEY,            -- Auto-incrementing Session ID
    UserId NVARCHAR(50) NOT NULL,                       -- Foreign Key (Logic) to NLA_Users
    InitialQuestion NVARCHAR(MAX) NULL,                 -- The question that started the chat
    FinalResult NVARCHAR(MAX) NULL,                     -- The final advice given to the user
    StartedAt DATETIME2 DEFAULT GETUTCDATE(),           -- When the chat started
    EndedAt DATETIME2 NULL                              -- When the chat ended
);
GO

-- Create indexes for performance on RAG queries
CREATE NONCLUSTERED INDEX IX_NLA_ChatSessions_UserId 
    ON dbo.NLA_ChatSessions(UserId);
GO

CREATE NONCLUSTERED INDEX IX_NLA_ChatSessions_FinalResult 
    ON dbo.NLA_ChatSessions(StartedAt DESC)
    WHERE FinalResult IS NOT NULL;
GO

PRINT 'NLA_ChatSessions table created successfully!';
GO
