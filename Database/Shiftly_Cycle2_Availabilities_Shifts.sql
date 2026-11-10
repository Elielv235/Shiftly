-- =========================================================================
-- פרויקט גמר: Shiftly (שיפטלי) - מחזור 2 (Cycle 2)
-- תלמיד: אליאל וולקונסקי | מנחה: זכי חן | תיכון ש"י עגנון, נתניה
-- סקריפט הקמת טבלאות ליבה למחזור 2: Shifts ו-Availabilities
-- ארכיטקטורה: שמות טבלאות ושדות באנגלית וב-PascalCase
-- =========================================================================

USE ShiftlyDB;
GO

-- 1. טבלת משמרות מוגדרות לסניף (Shifts)
-- מגדירה תקני עובדים למשמרת בוקר / ערב / לילה
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Shifts]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Shifts] (
        [ShiftId] INT IDENTITY(1,1) NOT NULL,
        [ShiftDate] DATETIME2 NOT NULL,
        [ShiftType] NVARCHAR(20) NOT NULL CONSTRAINT [DF_Shifts_ShiftType] DEFAULT (N'Morning'),
        [RequiredWorkers] INT NOT NULL CONSTRAINT [DF_Shifts_RequiredWorkers] DEFAULT (3),
        [CreatedByUserId] INT NOT NULL,
        CONSTRAINT [PK_Shifts] PRIMARY KEY CLUSTERED ([ShiftId] ASC),
        CONSTRAINT [FK_Shifts_Users] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[Users] ([UserId])
    );
END
GO

-- 2. טבלת אילוצי זמינות עובדים (Availabilities)
-- מכילה את הבקשות שהגישו העובדים למשמרות בוקר וערב לשבוע הקרוב
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Availabilities]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Availabilities] (
        [AvailabilityId] INT IDENTITY(1,1) NOT NULL,
        [UserId] INT NOT NULL,
        [TargetDate] DATETIME2 NOT NULL,
        [CanWorkMorning] BIT NOT NULL CONSTRAINT [DF_Availabilities_CanWorkMorning] DEFAULT (0),
        [CanWorkEvening] BIT NOT NULL CONSTRAINT [DF_Availabilities_CanWorkEvening] DEFAULT (0),
        [Notes] NVARCHAR(255) NULL,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT [DF_Availabilities_Status] DEFAULT (N'Pending'),
        CONSTRAINT [PK_Availabilities] PRIMARY KEY CLUSTERED ([AvailabilityId] ASC),
        CONSTRAINT [FK_Availabilities_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId])
    );
END
GO

-- 3. הזנת נתוני בדיקה ראשוניים (Seed Data)
IF NOT EXISTS (SELECT 1 FROM [dbo].[Shifts] WHERE [ShiftId] = 1)
BEGIN
    SET IDENTITY_INSERT [dbo].[Shifts] ON;
    INSERT INTO [dbo].[Shifts] ([ShiftId], [ShiftDate], [ShiftType], [RequiredWorkers], [CreatedByUserId])
    VALUES 
        (1, DATEADD(DAY, 1, CAST(GETDATE() AS DATE)), N'Morning', 4, 1),
        (2, DATEADD(DAY, 1, CAST(GETDATE() AS DATE)), N'Evening', 3, 1);
    SET IDENTITY_INSERT [dbo].[Shifts] OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[Availabilities] WHERE [AvailabilityId] = 1)
BEGIN
    SET IDENTITY_INSERT [dbo].[Availabilities] ON;
    INSERT INTO [dbo].[Availabilities] ([AvailabilityId], [UserId], [TargetDate], [CanWorkMorning], [CanWorkEvening], [Notes], [Status])
    VALUES 
        (1, 2, DATEADD(DAY, 2, CAST(GETDATE() AS DATE)), 1, 0, N'זמין בוקר בלבד', N'Pending'),
        (2, 2, DATEADD(DAY, 3, CAST(GETDATE() AS DATE)), 0, 1, N'לימודים בבוקר', N'Pending');
    SET IDENTITY_INSERT [dbo].[Availabilities] OFF;
END
GO

PRINT 'ShiftlyDB Cycle 2 Schema & Seed data successfully prepared!';
GO
