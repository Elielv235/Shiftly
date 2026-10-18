-- =========================================================================
-- פרויקט גמר: Shiftly (שיפטלי) - מחזור 1 (Cycle 1)
-- תלמיד: אליאל וולקונסקי | מנחה: זכי חן | תיכון ש"י עגנון, נתניה
-- סקריפט הקמת מסד נתונים וטבלאות ליבה למחזור 1: Roles ו-Users
-- ארכיטקטורה: שמות טבלאות ושדות באנגלית וב-PascalCase
-- =========================================================================

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = N'ShiftlyDB')
BEGIN
    CREATE DATABASE ShiftlyDB;
END
GO

USE ShiftlyDB;
GO

-- 1. טבלת תפקידים (Roles)
-- מכילה את ארבעת התפקידים המוגדרים באפיון המערכת
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Roles]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Roles] (
        [RoleId] INT IDENTITY(1,1) NOT NULL,
        [RoleName] NVARCHAR(50) NOT NULL,
        [Description] NVARCHAR(200) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED ([RoleId] ASC),
        CONSTRAINT [UQ_Roles_RoleName] UNIQUE NONCLUSTERED ([RoleName] ASC)
    );
END
GO

-- 2. טבלת משתמשים (Users)
-- מכילה את פרטי המשתמש, שדות האבטחה (גיבוב ומלח), ושיוך לתפקיד
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Users] (
        [UserId] INT IDENTITY(1,1) NOT NULL,
        [Username] NVARCHAR(50) NOT NULL,
        [PasswordHash] VARBINARY(64) NOT NULL,    -- גיבוב סיסמה באורך 64 בייטים (PBKDF2-SHA512)
        [PasswordSalt] VARBINARY(128) NOT NULL,   -- מלח אקראי באורך 128 בייטים להגנה מפני Rainbow Tables
        [FullName] NVARCHAR(100) NOT NULL,
        [Email] NVARCHAR(100) NOT NULL,
        [PhoneNumber] NVARCHAR(20) NOT NULL,
        [RoleId] INT NOT NULL,                    -- מפתח זר לטבלת Roles
        [IsActive] BIT NOT NULL CONSTRAINT [DF_Users_IsActive] DEFAULT (1),
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Users_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([UserId] ASC),
        CONSTRAINT [UQ_Users_Username] UNIQUE NONCLUSTERED ([Username] ASC),
        CONSTRAINT [FK_Users_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles] ([RoleId])
    );
END
GO

-- 3. הזנת נתוני ברירת מחדל לטבלת Roles
-- מזהים קבועים התואמים ל-Enum בספרייה המשותפת (UserRoleType)
SET IDENTITY_INSERT [dbo].[Roles] ON;

IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleId] = 1)
    INSERT INTO [dbo].[Roles] ([RoleId], [RoleName], [Description]) VALUES (1, N'Guest', N'אורח - גישה למסך הדמו בלבד ללא הרשאות עריכה');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleId] = 2)
    INSERT INTO [dbo].[Roles] ([RoleId], [RoleName], [Description]) VALUES (2, N'Worker', N'עובד סניף - הגשת זמינות וצפייה בסידור עבודה אישי');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleId] = 3)
    INSERT INTO [dbo].[Roles] ([RoleId], [RoleName], [Description]) VALUES (3, N'ShiftManager', N'מנהל משמרת - ניהול משמרת בזמן אמת ובקרת עובדים ומלאי');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleId] = 4)
    INSERT INTO [dbo].[Roles] ([RoleId], [RoleName], [Description]) VALUES (4, N'HRAdmin', N'מנהל סניף / כוח אדם - ניהול משתמשים מלא, סידור עבודה שבועי והזמנות רכש');

SET IDENTITY_INSERT [dbo].[Roles] OFF;
GO

PRINT 'ShiftlyDB Cycle 1 Schema & Seed data successfully prepared!';
GO
