using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Extensions.Configuration;
using Shiftly.Server.Security;

namespace Shiftly.Server.Data
{
    /// <summary>
    /// Standalone Microsoft Access (.accdb) database schema initializer, seeder, and verifier
    /// for Shiftly (Cycles 1, 2 & 3).
    ///
    /// Architectural Mandate:
    /// - Lives strictly inside Shiftly.Server (never exposed to Shiftly.Wpf or Shiftly.Shared).
    /// - Uses System.Data.OleDb exclusively (Microsoft.ACE.OLEDB.12.0 / 16.0).
    /// - Enforces SEC-M05 (salted SHA-256 hashes, zero plaintext passwords) and
    ///   SEC-M07 / SEC-M10 (Guest has NO entry in Users).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class AccessDbInitializer
    {
        private static readonly string[] RequiredTables =
        {
            "Roles",
            "Users",
            "Shifts",
            "ShiftAssignments",
            "Availabilities",
            "ShiftSwapRequests",
            "SupplyOrders"
        };

        /// <summary>
        /// Configures AppDomain's |DataDirectory| so Access connection strings referencing
        /// "|DataDirectory|\ShiftlyDatabase.accdb" resolve consistently.
        /// </summary>
        public static string ConfigureDataDirectory(string? customDirectory = null)
        {
            string dataDirectory = string.IsNullOrWhiteSpace(customDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "App_Data")
                : customDirectory;

            Directory.CreateDirectory(dataDirectory);
            AppDomain.CurrentDomain.SetData("DataDirectory", dataDirectory);
            return dataDirectory;
        }

        /// <summary>
        /// Initializes and seeds the Access database using AccessConnectionFactory (auto-creates .accdb if missing).
        /// </summary>
        public static void Initialize(AccessConnectionFactory factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            using var connection = factory.CreateOpenConnection();
            InitializeConnection(connection);
        }

        /// <summary>
        /// Initializes and seeds the Access database using IConfiguration.
        /// </summary>
        public static void Initialize(IConfiguration configuration, string? dataDirectory = null)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            string? connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? configuration.GetConnectionString("AccessConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Access connection string is missing from Shiftly.Server configuration.");

            Initialize(connectionString, dataDirectory);
        }

        /// <summary>
        /// Ensures all 7 Shiftly DSD tables exist in ShiftlyDatabase.accdb, seeds Roles and
        /// the 4 hashed test users (SEC-M05), and inserts realistic operational seed data if empty.
        /// </summary>
        public static void Initialize(string connectionString, string? dataDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Access connection string cannot be empty.", nameof(connectionString));

            ConfigureDataDirectory(dataDirectory);

            using var connection = new OleDbConnection(connectionString);
            connection.Open();
            InitializeConnection(connection);
        }

        private static void InitializeConnection(OleDbConnection connection)
        {
            EnsureTablesExist(connection);
            SeedRoles(connection);
            SeedUsers(connection);
            SeedCycleData(connection);
        }

        /// <summary>
        /// Verifies that all 7 DSD tables exist, checks row counts, and validates security invariants
        /// (SEC-M05 non-plaintext hashes and SEC-M07/SEC-M10 no Guest row in Users).
        /// </summary>
        public static AccessDbVerificationReport Verify(string connectionString, string? dataDirectory = null)
        {
            ConfigureDataDirectory(dataDirectory);

            var report = new AccessDbVerificationReport();
            using var connection = new OleDbConnection(connectionString);
            connection.Open();

            HashSet<string> existingTables = GetExistingTableNames(connection);

            foreach (string tableName in RequiredTables)
            {
                bool exists = existingTables.Contains(tableName);
                int rowCount = exists ? GetRowCount(connection, tableName) : 0;
                report.TableRowCounts[tableName] = exists ? rowCount : -1;
            }

            if (existingTables.Contains("Users"))
            {
                report.GuestExcludedFromUsers = !RecordExists(
                    connection,
                    "SELECT COUNT(*) FROM [Users] WHERE LCase([Username]) = ?",
                    new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = "guest" });

                report.AllPasswordsHashedAndSalted = VerifyAllUsersHaveSaltedHashes(connection);
            }

            return report;
        }

        // =========================================================================
        // 1. SCHEMA CREATION (7 DSD TABLES - ACCESS DDL)
        // =========================================================================

        private static void EnsureTablesExist(OleDbConnection connection)
        {
            HashSet<string> existingTables = GetExistingTableNames(connection);

            var tableDefinitions = new (string TableName, string Ddl)[]
            {
                ("Roles", @"
                    CREATE TABLE [Roles] (
                        [RoleId] COUNTER CONSTRAINT [PK_Roles] PRIMARY KEY,
                        [RoleName] TEXT(50) NOT NULL CONSTRAINT [UQ_Roles_RoleName] UNIQUE
                    )"),

                ("Users", @"
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
                    )"),

                ("Shifts", @"
                    CREATE TABLE [Shifts] (
                        [ShiftId] COUNTER CONSTRAINT [PK_Shifts] PRIMARY KEY,
                        [ShiftDate] DATETIME NOT NULL,
                        [ShiftType] TEXT(20) NOT NULL,
                        [RequiredWorkers] INTEGER NOT NULL,
                        [CreatedByUserId] INTEGER NOT NULL,
                        CONSTRAINT [FK_Shifts_CreatedByUser] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users]([UserId]),
                        CONSTRAINT [CK_Shifts_ShiftType] CHECK ([ShiftType] IN ('Morning', 'Evening', 'Night'))
                    )"),

                ("ShiftAssignments", @"
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
                    )"),

                ("Availabilities", @"
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
                    )"),

                ("ShiftSwapRequests", @"
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
                    )"),

                ("SupplyOrders", @"
                    CREATE TABLE [SupplyOrders] (
                        [OrderId] COUNTER CONSTRAINT [PK_SupplyOrders] PRIMARY KEY,
                        [CreatedByUserId] INTEGER NOT NULL,
                        [ItemsSummary] MEMO NOT NULL,
                        [Status] TEXT(30) NOT NULL DEFAULT 'Pending',
                        [Notes] TEXT(255),
                        [CreatedAt] DATETIME NOT NULL,
                        CONSTRAINT [FK_SupplyOrders_CreatedByUser] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users]([UserId]),
                        CONSTRAINT [CK_SupplyOrders_Status] CHECK ([Status] IN ('Pending', 'Delivered', 'Cancelled'))
                    )")
            };

            foreach (var (tableName, ddl) in tableDefinitions)
            {
                if (!existingTables.Contains(tableName))
                {
                    using var cmd = new OleDbCommand(ddl, connection);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // =========================================================================
        // 2. SEED ROLES & SECURE USERS (SEC-M05, SEC-M07, SEC-M10)
        // =========================================================================

        private static void SeedRoles(OleDbConnection connection)
        {
            // Canonical UserRoleType enum order:
            // 1 = Guest, 2 = Worker, 3 = ShiftManager, 4 = HRAdmin
            string[] roles = { "Guest", "Worker", "ShiftManager", "HRAdmin" };

            foreach (string roleName in roles)
            {
                bool exists = RecordExists(
                    connection,
                    "SELECT COUNT(*) FROM [Roles] WHERE [RoleName] = ?",
                    new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = roleName });

                if (!exists)
                {
                    using var insertCmd = new OleDbCommand(
                        "INSERT INTO [Roles] ([RoleName]) VALUES (?)",
                        connection);
                    insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = roleName });
                    insertCmd.ExecuteNonQuery();
                }
            }
        }

        private static void SeedUsers(OleDbConnection connection)
        {
            // SEC-M07 / SEC-M10: Guest has NO entry in Users table.
            var seedUsers = new (string Username, string PlaintextPassword, string FullName, string Email, string PhoneNumber, string RoleName)[]
            {
                ("admin",    "Admin123!",   "אליאל וולקונסקי (מנהל ראשי)", "admin@shiftly.co.il",   "050-1234567", "HRAdmin"),
                ("shiftmgr", "Manager123!", "נועה לוי (מנהלת משמרת)",       "manager@shiftly.co.il", "054-9876543", "ShiftManager"),
                ("worker1",  "Worker123!",  "דניאל כהן (עובד סניף)",        "worker1@shiftly.co.il", "052-7654321", "Worker"),
                ("worker2",  "Worker123!",  "תומר אברהם (עובד סניף)",       "worker2@shiftly.co.il", "053-4567890", "Worker")
            };

            foreach (var user in seedUsers)
            {
                bool exists = RecordExists(
                    connection,
                    "SELECT COUNT(*) FROM [Users] WHERE [Username] = ?",
                    new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = user.Username });

                if (exists)
                    continue;

                int roleId = GetRoleIdByName(connection, user.RoleName);
                var (passwordHash, salt) = PasswordHasher.CreateHash(user.PlaintextPassword);

                using var insertCmd = new OleDbCommand(
                    @"INSERT INTO [Users]
                        ([Username], [PasswordHash], [Salt], [FullName], [Email], [PhoneNumber], [RoleId], [IsActive])
                      VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    connection);

                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = user.Username });
                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = passwordHash });
                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = salt });
                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = user.FullName });
                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = user.Email });
                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = user.PhoneNumber });
                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer,  Value = roleId });
                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Boolean,  Value = true });

                insertCmd.ExecuteNonQuery();
            }
        }

        // =========================================================================
        // 3. MOCK OPERATIONAL DATA FOR CYCLES 2 & 3
        // =========================================================================

        private static void SeedCycleData(OleDbConnection connection)
        {
            int adminId    = GetUserIdByUsername(connection, "admin");
            int shiftMgrId = GetUserIdByUsername(connection, "shiftmgr");
            int worker1Id  = GetUserIdByUsername(connection, "worker1");
            int worker2Id  = GetUserIdByUsername(connection, "worker2");

            DateTime now = StripMilliseconds(DateTime.Now);
            DateTime tomorrow = DateTime.Today.AddDays(1);
            DateTime dayAfter = DateTime.Today.AddDays(2);

            // 3.1 Seed Shifts
            if (GetRowCount(connection, "Shifts") == 0)
            {
                InsertShift(connection, tomorrow, "Morning", 4, adminId);
                InsertShift(connection, tomorrow, "Evening", 3, adminId);
                InsertShift(connection, dayAfter, "Morning", 4, adminId);
            }

            // 3.2 Seed Availabilities
            if (GetRowCount(connection, "Availabilities") == 0)
            {
                InsertAvailability(connection, worker1Id, dayAfter, true,  false, "זמין למשמרת בוקר בלבד", "Pending");
                InsertAvailability(connection, worker2Id, dayAfter, true,  true,  "גמיש לבוקר או ערב",    "Approved");
            }

            // 3.3 Seed ShiftAssignments
            if (GetRowCount(connection, "ShiftAssignments") == 0)
            {
                List<int> shiftIds = GetAllIds(connection, "SELECT [ShiftId] FROM [Shifts] ORDER BY [ShiftId] ASC");
                if (shiftIds.Count >= 3)
                {
                    InsertShiftAssignment(connection, shiftIds[0], worker1Id, adminId,    now, true);
                    InsertShiftAssignment(connection, shiftIds[1], worker2Id, shiftMgrId, now, true);
                    InsertShiftAssignment(connection, shiftIds[2], worker1Id, shiftMgrId, now, true);
                }
            }

            // 3.4 Seed ShiftSwapRequests (2 Pending requests ready for ManagerHubWindow)
            if (GetRowCount(connection, "ShiftSwapRequests") == 0)
            {
                List<int> assignmentIds = GetAllIds(connection, "SELECT [AssignmentId] FROM [ShiftAssignments] ORDER BY [AssignmentId] ASC");
                if (assignmentIds.Count >= 2)
                {
                    InsertShiftSwapRequest(
                        connection,
                        assignmentId: assignmentIds[0],
                        targetUserId: worker2Id,
                        reason: "יש לי שיעור לפני הטסט",
                        status: "Pending",
                        reviewedByUserId: null,
                        createdAt: now);

                    InsertShiftSwapRequest(
                        connection,
                        assignmentId: assignmentIds[1],
                        targetUserId: worker1Id,
                        reason: "יש לי טסט",
                        status: "Pending",
                        reviewedByUserId: null,
                        createdAt: now.AddMinutes(-15));
                }
            }

            // 3.5 Seed SupplyOrders
            if (GetRowCount(connection, "SupplyOrders") == 0)
            {
                InsertSupplyOrder(
                    connection,
                    createdByUserId: shiftMgrId,
                    itemsSummary: "10 ארגזי לחמניות המבורגר, 5 קרטוני צ'יפס קפוא, 4 גלוני שמן טיגון, 200 כוסות שתייה קרה",
                    status: "Pending",
                    notes: "דחוף לקראת עומס סוף השבוע",
                    createdAt: now);

                InsertSupplyOrder(
                    connection,
                    createdByUserId: adminId,
                    itemsSummary: "6 חבילות מפיות נייר, 3 מארזי שקיות Take-Away, 8 בקבוקי קטשופ תעשייתי",
                    status: "Delivered",
                    notes: "התקבל ונבדק במחסן הסניף",
                    createdAt: now.AddDays(-1));
            }
        }

        private static void InsertShift(OleDbConnection conn, DateTime shiftDate, string shiftType, int requiredWorkers, int createdByUserId)
        {
            using var cmd = new OleDbCommand(
                "INSERT INTO [Shifts] ([ShiftDate], [ShiftType], [RequiredWorkers], [CreatedByUserId]) VALUES (?, ?, ?, ?)",
                conn);
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date,     Value = StripMilliseconds(shiftDate) });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = shiftType });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer,  Value = requiredWorkers });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer,  Value = createdByUserId });
            cmd.ExecuteNonQuery();
        }

        private static void InsertAvailability(OleDbConnection conn, int userId, DateTime targetDate, bool morning, bool evening, string? notes, string status)
        {
            using var cmd = new OleDbCommand(
                "INSERT INTO [Availabilities] ([UserId], [TargetDate], [CanWorkMorning], [CanWorkEvening], [Notes], [Status]) VALUES (?, ?, ?, ?, ?, ?)",
                conn);
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer,  Value = userId });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date,     Value = StripMilliseconds(targetDate) });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Boolean,  Value = morning });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Boolean,  Value = evening });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = (object?)notes ?? DBNull.Value });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = status });
            cmd.ExecuteNonQuery();
        }

        private static void InsertShiftAssignment(OleDbConnection conn, int shiftId, int userId, int assignedByUserId, DateTime assignedAt, bool isConfirmed)
        {
            using var cmd = new OleDbCommand(
                "INSERT INTO [ShiftAssignments] ([ShiftId], [UserId], [AssignedByUserId], [AssignedAt], [IsConfirmed]) VALUES (?, ?, ?, ?, ?)",
                conn);
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = shiftId });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = userId });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = assignedByUserId });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date,    Value = StripMilliseconds(assignedAt) });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Boolean, Value = isConfirmed });
            cmd.ExecuteNonQuery();
        }

        private static void InsertShiftSwapRequest(OleDbConnection conn, int assignmentId, int targetUserId, string? reason, string status, int? reviewedByUserId, DateTime createdAt)
        {
            using var cmd = new OleDbCommand(
                "INSERT INTO [ShiftSwapRequests] ([AssignmentId], [TargetUserId], [Reason], [Status], [ReviewedByUserId], [CreatedAt]) VALUES (?, ?, ?, ?, ?, ?)",
                conn);
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer,  Value = assignmentId });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer,  Value = targetUserId });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = (object?)reason ?? DBNull.Value });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = status });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer,  Value = reviewedByUserId.HasValue ? reviewedByUserId.Value : DBNull.Value });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date,     Value = StripMilliseconds(createdAt) });
            cmd.ExecuteNonQuery();
        }

        private static void InsertSupplyOrder(OleDbConnection conn, int createdByUserId, string itemsSummary, string status, string? notes, DateTime createdAt)
        {
            using var cmd = new OleDbCommand(
                "INSERT INTO [SupplyOrders] ([CreatedByUserId], [ItemsSummary], [Status], [Notes], [CreatedAt]) VALUES (?, ?, ?, ?, ?)",
                conn);
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer,      Value = createdByUserId });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.LongVarWChar, Value = itemsSummary });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar,     Value = status });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar,     Value = (object?)notes ?? DBNull.Value });
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date,         Value = StripMilliseconds(createdAt) });
            cmd.ExecuteNonQuery();
        }

        private static HashSet<string> GetExistingTableNames(OleDbConnection connection)
        {
            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DataTable schemaTable = connection.GetSchema("Tables", new[] { null, null, null, "TABLE" });

            foreach (DataRow row in schemaTable.Rows)
            {
                string? tableName = row["TABLE_NAME"]?.ToString();
                if (!string.IsNullOrWhiteSpace(tableName))
                    tables.Add(tableName);
            }

            return tables;
        }

        private static bool RecordExists(OleDbConnection connection, string countSql, params OleDbParameter[] parameters)
        {
            using var cmd = new OleDbCommand(countSql, connection);
            cmd.Parameters.AddRange(parameters);
            object? result = cmd.ExecuteScalar();
            return result != null && Convert.ToInt32(result) > 0;
        }

        private static int GetRowCount(OleDbConnection connection, string tableName)
        {
            using var cmd = new OleDbCommand($"SELECT COUNT(*) FROM [{tableName}]", connection);
            object? result = cmd.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        private static int GetRoleIdByName(OleDbConnection connection, string roleName)
        {
            using var cmd = new OleDbCommand("SELECT [RoleId] FROM [Roles] WHERE [RoleName] = ?", connection);
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = roleName });
            object? result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                throw new InvalidOperationException($"Required role '{roleName}' was not found in [Roles].");
            return Convert.ToInt32(result);
        }

        private static int GetUserIdByUsername(OleDbConnection connection, string username)
        {
            using var cmd = new OleDbCommand("SELECT [UserId] FROM [Users] WHERE [Username] = ?", connection);
            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = username });
            object? result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                throw new InvalidOperationException($"Required seed user '{username}' was not found in [Users].");
            return Convert.ToInt32(result);
        }

        private static List<int> GetAllIds(OleDbConnection connection, string sql)
        {
            var ids = new List<int>();
            using var cmd = new OleDbCommand(sql, connection);
            using OleDbDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ids.Add(reader.GetInt32(0));
            }
            return ids;
        }

        private static bool VerifyAllUsersHaveSaltedHashes(OleDbConnection connection)
        {
            using var cmd = new OleDbCommand("SELECT [PasswordHash], [Salt] FROM [Users]", connection);
            using OleDbDataReader reader = cmd.ExecuteReader();
            int count = 0;

            while (reader.Read())
            {
                count++;
                string? hash = reader.IsDBNull(0) ? null : reader.GetString(0);
                string? salt = reader.IsDBNull(1) ? null : reader.GetString(1);

                if (string.IsNullOrWhiteSpace(hash) || hash.Length != 44 ||
                    string.IsNullOrWhiteSpace(salt) || salt.Length < 16)
                {
                    return false;
                }
            }

            return count >= 4;
        }

        private static DateTime StripMilliseconds(DateTime dt) =>
            new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Kind);
    }

    public sealed class AccessDbVerificationReport
    {
        public Dictionary<string, int> TableRowCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool GuestExcludedFromUsers { get; set; }
        public bool AllPasswordsHashedAndSalted { get; set; }

        public bool IsValid
        {
            get
            {
                if (!GuestExcludedFromUsers || !AllPasswordsHashedAndSalted)
                    return false;

                foreach (int count in TableRowCounts.Values)
                {
                    if (count < 0)
                        return false;
                }

                return TableRowCounts.Count == 7;
            }
        }
    }
}
