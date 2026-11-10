-- =============================================================================
-- Shiftly (שיפטלי) - Stage 7 / Cycle 3 Database Schema (Microsoft Access .accdb)
-- Student: Eliel Valkonsky | Course Lead: Zaky Khen | Shai Agnon High School, Netanya
-- Storage Engine: Microsoft Access (.accdb) via System.Data.OleDb
-- Provider: Microsoft.ACE.OLEDB.12.0 / Microsoft.ACE.OLEDB.16.0
-- Naming Convention: Strict PascalCase matching approved Shiftly DSD
-- =============================================================================

-- -----------------------------------------------------------------------------
-- SECTION A: CORE EXISTING TABLES (Cycle 1 & Cycle 2 Verification)
-- -----------------------------------------------------------------------------

-- 1. Roles
CREATE TABLE [Roles] (
    [RoleId] COUNTER CONSTRAINT [PK_Roles] PRIMARY KEY,
    [RoleName] TEXT(50) NOT NULL CONSTRAINT [UQ_Roles_RoleName] UNIQUE
);

-- 2. Users (Guest MUST NOT have an entry in this table - SEC-M07 / SEC-M10)
CREATE TABLE [Users] (
    [UserId] COUNTER CONSTRAINT [PK_Users] PRIMARY KEY,
    [Username] TEXT(50) NOT NULL CONSTRAINT [UQ_Users_Username] UNIQUE,
    [PasswordHash] TEXT(255) NOT NULL,
    [Salt] TEXT(100) NOT NULL,
    [FullName] TEXT(100) NOT NULL,
    [Email] TEXT(100) NOT NULL,
    [PhoneNumber] TEXT(20),
    [RoleId] INTEGER NOT NULL,
    [IsActive] YESNO DEFAULT True,
    CONSTRAINT [FK_Users_Roles] FOREIGN KEY ([RoleId]) REFERENCES [Roles]([RoleId])
);

-- 3. Shifts
CREATE TABLE [Shifts] (
    [ShiftId] COUNTER CONSTRAINT [PK_Shifts] PRIMARY KEY,
    [ShiftDate] DATETIME NOT NULL,
    [ShiftType] TEXT(20) NOT NULL,
    [RequiredWorkers] INTEGER NOT NULL,
    [CreatedByUserId] INTEGER NOT NULL,
    CONSTRAINT [FK_Shifts_CreatedByUser] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users]([UserId]),
    CONSTRAINT [CK_Shifts_ShiftType] CHECK ([ShiftType] IN ('Morning', 'Evening', 'Night'))
);

-- 4. ShiftAssignments
CREATE TABLE [ShiftAssignments] (
    [AssignmentId] COUNTER CONSTRAINT [PK_ShiftAssignments] PRIMARY KEY,
    [ShiftId] INTEGER NOT NULL,
    [UserId] INTEGER NOT NULL,
    [AssignedByUserId] INTEGER NOT NULL,
    [AssignedAt] DATETIME NOT NULL,
    [IsConfirmed] YESNO DEFAULT False,
    CONSTRAINT [FK_ShiftAssignments_Shifts] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts]([ShiftId]),
    CONSTRAINT [FK_ShiftAssignments_Users] FOREIGN KEY ([UserId]) REFERENCES [Users]([UserId]),
    CONSTRAINT [FK_ShiftAssignments_AssignedByUser] FOREIGN KEY ([AssignedByUserId]) REFERENCES [Users]([UserId])
);

-- 5. Availabilities
CREATE TABLE [Availabilities] (
    [AvailabilityId] COUNTER CONSTRAINT [PK_Availabilities] PRIMARY KEY,
    [UserId] INTEGER NOT NULL,
    [TargetDate] DATETIME NOT NULL,
    [CanWorkMorning] YESNO DEFAULT False,
    [CanWorkEvening] YESNO DEFAULT False,
    [Notes] TEXT(255),
    [Status] TEXT(20) NOT NULL DEFAULT 'Pending',
    CONSTRAINT [FK_Availabilities_Users] FOREIGN KEY ([UserId]) REFERENCES [Users]([UserId]),
    CONSTRAINT [CK_Availabilities_Status] CHECK ([Status] IN ('Pending', 'Approved', 'Rejected'))
);

-- -----------------------------------------------------------------------------
-- SECTION B: CYCLE 3 CONTENT TABLES (Strictly per DSD)
-- -----------------------------------------------------------------------------

-- 6. ShiftSwapRequests
-- Allowed Status values: 'Pending', 'Approved', 'Rejected'
CREATE TABLE [ShiftSwapRequests] (
    [SwapId] COUNTER CONSTRAINT [PK_ShiftSwapRequests] PRIMARY KEY,
    [AssignmentId] INTEGER NOT NULL,
    [TargetUserId] INTEGER NOT NULL,
    [Reason] TEXT(255),
    [Status] TEXT(20) NOT NULL DEFAULT 'Pending',
    [ReviewedByUserId] INTEGER,
    [CreatedAt] DATETIME NOT NULL,
    CONSTRAINT [FK_ShiftSwapRequests_ShiftAssignments] FOREIGN KEY ([AssignmentId]) REFERENCES [ShiftAssignments]([AssignmentId]),
    CONSTRAINT [FK_ShiftSwapRequests_TargetUser] FOREIGN KEY ([TargetUserId]) REFERENCES [Users]([UserId]),
    CONSTRAINT [FK_ShiftSwapRequests_ReviewedByUser] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [Users]([UserId]),
    CONSTRAINT [CK_ShiftSwapRequests_Status] CHECK ([Status] IN ('Pending', 'Approved', 'Rejected'))
);

-- 7. SupplyOrders
-- Allowed Status values: 'Pending', 'Delivered', 'Cancelled'
-- Note: ItemsSummary uses MEMO (Long Text) in Microsoft Access to support >= 500 chars
--       (Access Short Text / TEXT(n) is capped at 255 characters).
CREATE TABLE [SupplyOrders] (
    [OrderId] COUNTER CONSTRAINT [PK_SupplyOrders] PRIMARY KEY,
    [CreatedByUserId] INTEGER NOT NULL,
    [ItemsSummary] MEMO NOT NULL,
    [Status] TEXT(30) NOT NULL DEFAULT 'Pending',
    [Notes] TEXT(255),
    [CreatedAt] DATETIME NOT NULL,
    CONSTRAINT [FK_SupplyOrders_CreatedByUser] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users]([UserId]),
    CONSTRAINT [CK_SupplyOrders_Status] CHECK ([Status] IN ('Pending', 'Delivered', 'Cancelled'))
);

