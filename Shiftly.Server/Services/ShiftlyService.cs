using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Shiftly.Server.Data;
using Shiftly.Shared.DTOs;
using Shiftly.Shared.Enums;
using Shiftly.Shared.Interfaces;

namespace Shiftly.Server.Services
{
    /// <summary>
    /// Microsoft Access (.accdb / System.Data.OleDb) implementation of IShiftlyService for Cycle 3:
    /// ShiftSwapRequests and SupplyOrders.
    ///
    /// Security & Architecture:
    /// - Strictly parameterized OleDbCommand queries (positional ? parameters).
    /// - All authorization checks read the authoritative user & role directly from Access DB.
    /// - Swap approval runs inside an OleDbTransaction with automatic Rollback on failure.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class ShiftlyService : IShiftlyService
    {
        private readonly AccessConnectionFactory _connectionFactory;

        public ShiftlyService(AccessConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        private sealed record AuthenticatedUserInfo(int UserId, string FullName, string RoleName);

        private static AuthenticatedUserInfo? GetActiveUserFromDb(OleDbConnection connection, int authenticatedUserId, OleDbTransaction? tx = null)
        {
            if (authenticatedUserId <= 0)
                return null;

            using var cmd = new OleDbCommand(
                @"SELECT u.[UserId], u.[FullName], r.[RoleName]
                  FROM [Users] u INNER JOIN [Roles] r ON u.[RoleId] = r.[RoleId]
                  WHERE u.[UserId] = ? AND u.[IsActive] = True",
                connection,
                tx);

            cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = authenticatedUserId });

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return null;

            return new AuthenticatedUserInfo(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2));
        }

        private static bool IsManagerRole(AuthenticatedUserInfo user) =>
            user.RoleName == nameof(UserRoleType.ShiftManager) ||
            user.RoleName == nameof(UserRoleType.HRAdmin);

        private static DateTime StripMilliseconds(DateTime dt) =>
            new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Kind);

        // =========================================================================
        // SHIFT SWAP REQUESTS
        // =========================================================================

        public Task<OperationResultDto> SubmitShiftSwapRequestAsync(int authenticatedUserId, ShiftSwapRequestCreateDto request)
        {
            try
            {
                using var connection = _connectionFactory.CreateOpenConnection();

                var currentUser = GetActiveUserFromDb(connection, authenticatedUserId);
                if (currentUser == null)
                    return Task.FromResult(OperationResultDto.Fail(401, "גישה לא מאומתת: יש להתחבר למערכת עם חשבון פעיל."));

                if (currentUser.RoleName == nameof(UserRoleType.Guest))
                    return Task.FromResult(OperationResultDto.Fail(403, "אורח אינו מורשה להגיש בקשות החלפת משמרת."));

                if (request == null || request.AssignmentId <= 0 || request.TargetUserId <= 0)
                    return Task.FromResult(OperationResultDto.Fail(400, "נתוני בקשת ההחלפה חסרים או אינם תקינים."));

                string cleanReason = request.Reason?.Trim() ?? string.Empty;
                if (cleanReason.Length < 3 || cleanReason.Length > 255)
                    return Task.FromResult(OperationResultDto.Fail(400, "יש להזין סיבת החלפה תקינה (3 עד 255 תווים)."));

                // Verify assignment exists and belongs to currentUser
                int? assignmentOwnerId = null;
                using (var assignCmd = new OleDbCommand("SELECT [UserId] FROM [ShiftAssignments] WHERE [AssignmentId] = ?", connection))
                {
                    assignCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = request.AssignmentId });
                    object? ownerObj = assignCmd.ExecuteScalar();
                    if (ownerObj != null && ownerObj != DBNull.Value)
                        assignmentOwnerId = Convert.ToInt32(ownerObj);
                }

                if (assignmentOwnerId == null)
                    return Task.FromResult(OperationResultDto.Fail(404, "השיבוץ המבוקש לא נמצא במערכת."));

                if (assignmentOwnerId.Value != currentUser.UserId)
                    return Task.FromResult(OperationResultDto.Fail(403, "גישה נדחתה (403): ניתן להגיש בקשת החלפה רק עבור משמרת שמשובצת אליך."));

                if (request.TargetUserId == currentUser.UserId)
                    return Task.FromResult(OperationResultDto.Fail(400, "לא ניתן לבקש החלפת משמרת עם עצמך."));

                var targetUser = GetActiveUserFromDb(connection, request.TargetUserId);
                if (targetUser == null || targetUser.RoleName == nameof(UserRoleType.Guest))
                    return Task.FromResult(OperationResultDto.Fail(400, "העובד המחליף שנבחר אינו קיים או אינו פעיל."));

                using (var dupCmd = new OleDbCommand(
                    "SELECT COUNT(*) FROM [ShiftSwapRequests] WHERE [AssignmentId] = ? AND [Status] = 'Pending'",
                    connection))
                {
                    dupCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = request.AssignmentId });
                    if (Convert.ToInt32(dupCmd.ExecuteScalar()) > 0)
                        return Task.FromResult(OperationResultDto.Fail(409, "כבר קיימת בקשת החלפה ממתינה עבור משמרת זו."));
                }

                using var insertCmd = new OleDbCommand(
                    @"INSERT INTO [ShiftSwapRequests]
                        ([AssignmentId], [TargetUserId], [Reason], [Status], [ReviewedByUserId], [CreatedAt])
                      VALUES (?, ?, ?, ?, ?, ?)",
                    connection);

                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer,  Value = request.AssignmentId });
                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer,  Value = targetUser.UserId });
                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = cleanReason });
                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = "Pending" });
                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer,  Value = DBNull.Value });
                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date,     Value = StripMilliseconds(DateTime.Now) });
                insertCmd.ExecuteNonQuery();

                return Task.FromResult(OperationResultDto.Ok("בקשת החלפת המשמרת נשלחה בהצלחה וממתינה לאישור מנהל."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(OperationResultDto.Fail(500, $"שגיאת שרת בעת יצירת בקשת החלפה: {ex.Message}"));
            }
        }

        public Task<OperationResultDto<List<ShiftSwapItemDto>>> GetPendingShiftSwapRequestsAsync(int authenticatedUserId)
        {
            try
            {
                using var connection = _connectionFactory.CreateOpenConnection();

                var currentUser = GetActiveUserFromDb(connection, authenticatedUserId);
                if (currentUser == null)
                    return Task.FromResult(OperationResultDto<List<ShiftSwapItemDto>>.Fail(401, "גישה לא מאומתת: נדרש משתמש מחובר ופעיל."));

                if (!IsManagerRole(currentUser))
                    return Task.FromResult(OperationResultDto<List<ShiftSwapItemDto>>.Fail(403, "גישה נדחתה (403): צפייה בבקשות החלפה שמורה למנהל משמרת או HRAdmin בלבד."));

                var items = new List<ShiftSwapItemDto>();
                using var cmd = new OleDbCommand(
                    @"SELECT sr.[SwapId], sr.[AssignmentId], req.[FullName], tgt.[FullName], s.[ShiftDate], s.[ShiftType], sr.[Reason], sr.[Status]
                      FROM ((([ShiftSwapRequests] sr
                          INNER JOIN [ShiftAssignments] a ON sr.[AssignmentId] = a.[AssignmentId])
                          INNER JOIN [Shifts] s ON a.[ShiftId] = s.[ShiftId])
                          INNER JOIN [Users] req ON a.[UserId] = req.[UserId])
                          INNER JOIN [Users] tgt ON sr.[TargetUserId] = tgt.[UserId]
                      WHERE sr.[Status] = 'Pending'
                      ORDER BY sr.[CreatedAt] DESC",
                    connection);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    items.Add(new ShiftSwapItemDto
                    {
                        SwapId = reader.GetInt32(0),
                        AssignmentId = reader.GetInt32(1),
                        RequesterName = reader.GetString(2),
                        TargetWorkerName = reader.GetString(3),
                        ShiftDate = reader.GetDateTime(4),
                        ShiftType = reader.GetString(5),
                        Reason = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        Status = reader.GetString(7)
                    });
                }

                return Task.FromResult(OperationResultDto<List<ShiftSwapItemDto>>.Ok(items));
            }
            catch (Exception ex)
            {
                return Task.FromResult(OperationResultDto<List<ShiftSwapItemDto>>.Fail(500, $"שגיאת שרת בטעינת בקשות ההחלפה: {ex.Message}"));
            }
        }

        public Task<OperationResultDto> ReviewShiftSwapRequestAsync(int authenticatedUserId, ShiftSwapReviewDto review)
        {
            try
            {
                using var connection = _connectionFactory.CreateOpenConnection();

                var currentUser = GetActiveUserFromDb(connection, authenticatedUserId);
                if (currentUser == null)
                    return Task.FromResult(OperationResultDto.Fail(401, "גישה לא מאומתת: נדרש משתמש מחובר ופעיל."));

                if (!IsManagerRole(currentUser))
                    return Task.FromResult(OperationResultDto.Fail(403, "גישה נדחתה (403): אישור או דחיית החלפות שמורים למנהל משמרת או HRAdmin בלבד."));

                if (review == null || review.SwapId <= 0)
                    return Task.FromResult(OperationResultDto.Fail(400, "מזהה בקשת ההחלפה אינו תקין."));

                using var transaction = connection.BeginTransaction();
                try
                {
                    int assignmentId;
                    int targetUserId;
                    string currentStatus;

                    using (var selectCmd = new OleDbCommand(
                        "SELECT [AssignmentId], [TargetUserId], [Status] FROM [ShiftSwapRequests] WHERE [SwapId] = ?",
                        connection,
                        transaction))
                    {
                        selectCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = review.SwapId });
                        using var reader = selectCmd.ExecuteReader();
                        if (!reader.Read())
                        {
                            transaction.Rollback();
                            return Task.FromResult(OperationResultDto.Fail(404, "בקשת ההחלפה לא נמצאה."));
                        }

                        assignmentId = reader.GetInt32(0);
                        targetUserId = reader.GetInt32(1);
                        currentStatus = reader.GetString(2);
                    }

                    if (!currentStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                    {
                        transaction.Rollback();
                        return Task.FromResult(OperationResultDto.Fail(409, "בקשת ההחלפה כבר טופלה בעבר."));
                    }

                    if (review.IsApproved)
                    {
                        var targetUser = GetActiveUserFromDb(connection, targetUserId, transaction);
                        if (targetUser == null)
                        {
                            transaction.Rollback();
                            return Task.FromResult(OperationResultDto.Fail(400, "העובד המחליף אינו פעיל במערכת ולכן לא ניתן לאשר את ההחלפה."));
                        }

                        using (var updateSwapCmd = new OleDbCommand(
                            "UPDATE [ShiftSwapRequests] SET [Status] = ?, [ReviewedByUserId] = ? WHERE [SwapId] = ?",
                            connection,
                            transaction))
                        {
                            updateSwapCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = "Approved" });
                            updateSwapCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer,  Value = currentUser.UserId });
                            updateSwapCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer,  Value = review.SwapId });
                            updateSwapCmd.ExecuteNonQuery();
                        }

                        using (var updateAssignCmd = new OleDbCommand(
                            "UPDATE [ShiftAssignments] SET [UserId] = ?, [AssignedByUserId] = ?, [AssignedAt] = ? WHERE [AssignmentId] = ?",
                            connection,
                            transaction))
                        {
                            updateAssignCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = targetUserId });
                            updateAssignCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = currentUser.UserId });
                            updateAssignCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date,    Value = StripMilliseconds(DateTime.Now) });
                            updateAssignCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = assignmentId });
                            if (updateAssignCmd.ExecuteNonQuery() == 0)
                            {
                                transaction.Rollback();
                                return Task.FromResult(OperationResultDto.Fail(404, "השיבוץ המקושר לבקשה לא נמצא."));
                            }
                        }
                    }
                    else
                    {
                        using var rejectCmd = new OleDbCommand(
                            "UPDATE [ShiftSwapRequests] SET [Status] = ?, [ReviewedByUserId] = ? WHERE [SwapId] = ?",
                            connection,
                            transaction);
                        rejectCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = "Rejected" });
                        rejectCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer,  Value = currentUser.UserId });
                        rejectCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer,  Value = review.SwapId });
                        rejectCmd.ExecuteNonQuery();
                    }

                    transaction.Commit();

                    return Task.FromResult(OperationResultDto.Ok(review.IsApproved
                        ? "בקשת ההחלפה אושרה והשיבוץ הועבר בהצלחה לעובד המחליף."
                        : "בקשת ההחלפה נדחתה."));
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(OperationResultDto.Fail(500, $"שגיאת שרת בעדכון בקשת ההחלפה (בוצע Rollback): {ex.Message}"));
            }
        }

        // =========================================================================
        // WORKER ASSIGNMENTS & COWORKERS
        // =========================================================================

        public Task<OperationResultDto<List<WorkerShiftAssignmentDto>>> GetWorkerAssignmentsAsync(int authenticatedUserId)
        {
            try
            {
                using var connection = _connectionFactory.CreateOpenConnection();

                var currentUser = GetActiveUserFromDb(connection, authenticatedUserId);
                if (currentUser == null)
                    return Task.FromResult(OperationResultDto<List<WorkerShiftAssignmentDto>>.Fail(401, "גישה לא מאומתת: יש להתחבר עם חשבון פעיל."));

                var pendingAssignmentIds = new HashSet<int>();
                using (var pendingCmd = new OleDbCommand("SELECT [AssignmentId] FROM [ShiftSwapRequests] WHERE [Status] = 'Pending'", connection))
                using (var pendingReader = pendingCmd.ExecuteReader())
                {
                    while (pendingReader.Read())
                        pendingAssignmentIds.Add(pendingReader.GetInt32(0));
                }

                var list = new List<WorkerShiftAssignmentDto>();
                using var cmd = new OleDbCommand(
                    @"SELECT a.[AssignmentId], a.[ShiftId], s.[ShiftDate], s.[ShiftType], a.[IsConfirmed]
                      FROM [ShiftAssignments] a INNER JOIN [Shifts] s ON a.[ShiftId] = s.[ShiftId]
                      WHERE a.[UserId] = ?
                      ORDER BY s.[ShiftDate] ASC",
                    connection);

                cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = currentUser.UserId });

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int assignmentId = reader.GetInt32(0);
                    list.Add(new WorkerShiftAssignmentDto
                    {
                        AssignmentId = assignmentId,
                        ShiftId = reader.GetInt32(1),
                        ShiftDate = reader.GetDateTime(2),
                        ShiftType = reader.GetString(3),
                        IsConfirmed = reader.GetBoolean(4),
                        HasPendingSwap = pendingAssignmentIds.Contains(assignmentId)
                    });
                }

                return Task.FromResult(OperationResultDto<List<WorkerShiftAssignmentDto>>.Ok(list));
            }
            catch (Exception ex)
            {
                return Task.FromResult(OperationResultDto<List<WorkerShiftAssignmentDto>>.Fail(500, $"שגיאת שרת בטעינת המשמרות המשובצות: {ex.Message}"));
            }
        }

        public Task<OperationResultDto<List<CoworkerOptionDto>>> GetSwapTargetWorkersAsync(int authenticatedUserId)
        {
            try
            {
                using var connection = _connectionFactory.CreateOpenConnection();

                var currentUser = GetActiveUserFromDb(connection, authenticatedUserId);
                if (currentUser == null)
                    return Task.FromResult(OperationResultDto<List<CoworkerOptionDto>>.Fail(401, "גישה לא מאומתת."));

                var coworkers = new List<CoworkerOptionDto>();
                using var cmd = new OleDbCommand(
                    @"SELECT u.[UserId], u.[FullName], u.[Username]
                      FROM [Users] u INNER JOIN [Roles] r ON u.[RoleId] = r.[RoleId]
                      WHERE u.[IsActive] = True AND u.[UserId] <> ? AND r.[RoleName] <> 'Guest'
                      ORDER BY u.[FullName] ASC",
                    connection);

                cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = currentUser.UserId });

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    coworkers.Add(new CoworkerOptionDto
                    {
                        UserId = reader.GetInt32(0),
                        FullName = reader.GetString(1),
                        Username = reader.GetString(2)
                    });
                }

                return Task.FromResult(OperationResultDto<List<CoworkerOptionDto>>.Ok(coworkers));
            }
            catch (Exception ex)
            {
                return Task.FromResult(OperationResultDto<List<CoworkerOptionDto>>.Fail(500, $"שגיאת שרת בטעינת רשימת עובדים מחליפים: {ex.Message}"));
            }
        }

        // =========================================================================
        // SUPPLY ORDERS
        // =========================================================================

        public Task<OperationResultDto<SupplyOrderItemDto>> CreateSupplyOrderAsync(int authenticatedUserId, SupplyOrderCreateDto order)
        {
            try
            {
                using var connection = _connectionFactory.CreateOpenConnection();

                var currentUser = GetActiveUserFromDb(connection, authenticatedUserId);
                if (currentUser == null)
                    return Task.FromResult(OperationResultDto<SupplyOrderItemDto>.Fail(401, "גישה לא מאומתת: יש להתחבר עם חשבון פעיל."));

                if (!IsManagerRole(currentUser))
                    return Task.FromResult(OperationResultDto<SupplyOrderItemDto>.Fail(403, "גישה נדחתה (403): יצירת הזמנות מלאי שמורה למנהל משמרת או HRAdmin בלבד."));

                if (order == null || string.IsNullOrWhiteSpace(order.ItemsSummary))
                    return Task.FromResult(OperationResultDto<SupplyOrderItemDto>.Fail(400, "פירוט הפריטים להזמנה הוא שדה חובה."));

                string cleanItems = order.ItemsSummary.Trim();
                if (cleanItems.Length < 3 || cleanItems.Length > 500)
                    return Task.FromResult(OperationResultDto<SupplyOrderItemDto>.Fail(400, "פירוט הפריטים חייב להכיל בין 3 ל-500 תווים."));

                string? cleanNotes = string.IsNullOrWhiteSpace(order.Notes) ? null : order.Notes.Trim();
                if (cleanNotes != null && cleanNotes.Length > 255)
                    return Task.FromResult(OperationResultDto<SupplyOrderItemDto>.Fail(400, "הערות ההזמנה יכולות להכיל עד 255 תווים."));

                DateTime createdAt = StripMilliseconds(DateTime.Now);

                using (var insertCmd = new OleDbCommand(
                    @"INSERT INTO [SupplyOrders] ([CreatedByUserId], [ItemsSummary], [Status], [Notes], [CreatedAt])
                      VALUES (?, ?, ?, ?, ?)",
                    connection))
                {
                    insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer,      Value = currentUser.UserId });
                    insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.LongVarWChar, Value = cleanItems });
                    insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar,     Value = "Pending" });
                    insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar,     Value = (object?)cleanNotes ?? DBNull.Value });
                    insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date,         Value = createdAt });
                    insertCmd.ExecuteNonQuery();
                }

                int newOrderId;
                using (var idCmd = new OleDbCommand("SELECT @@IDENTITY", connection))
                {
                    newOrderId = Convert.ToInt32(idCmd.ExecuteScalar());
                }

                var dto = new SupplyOrderItemDto
                {
                    OrderId = newOrderId,
                    CreatedByUserId = currentUser.UserId,
                    ManagerName = currentUser.FullName,
                    ItemsSummary = cleanItems,
                    Status = "Pending",
                    Notes = cleanNotes,
                    CreatedAt = createdAt
                };

                return Task.FromResult(OperationResultDto<SupplyOrderItemDto>.Ok(dto, "הזמנת המלאי נוצרה ונשמרה בהצלחה."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(OperationResultDto<SupplyOrderItemDto>.Fail(500, $"שגיאת שרת ביצירת הזמנת המלאי: {ex.Message}"));
            }
        }

        public Task<OperationResultDto<List<SupplyOrderItemDto>>> GetSupplyOrdersAsync(int authenticatedUserId)
        {
            try
            {
                using var connection = _connectionFactory.CreateOpenConnection();

                var currentUser = GetActiveUserFromDb(connection, authenticatedUserId);
                if (currentUser == null)
                    return Task.FromResult(OperationResultDto<List<SupplyOrderItemDto>>.Fail(401, "גישה לא מאומתת: יש להתחבר עם חשבון פעיל."));

                if (!IsManagerRole(currentUser))
                    return Task.FromResult(OperationResultDto<List<SupplyOrderItemDto>>.Fail(403, "גישה נדחתה (403): צפייה בהזמנות מלאי שמורה למנהל משמרת או HRAdmin בלבד."));

                var orders = new List<SupplyOrderItemDto>();
                using var cmd = new OleDbCommand(
                    @"SELECT so.[OrderId], so.[CreatedByUserId], u.[FullName], so.[ItemsSummary], so.[Status], so.[Notes], so.[CreatedAt]
                      FROM [SupplyOrders] so INNER JOIN [Users] u ON so.[CreatedByUserId] = u.[UserId]
                      ORDER BY so.[CreatedAt] DESC",
                    connection);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    orders.Add(new SupplyOrderItemDto
                    {
                        OrderId = reader.GetInt32(0),
                        CreatedByUserId = reader.GetInt32(1),
                        ManagerName = reader.GetString(2),
                        ItemsSummary = reader.GetString(3),
                        Status = reader.GetString(4),
                        Notes = reader.IsDBNull(5) ? null : reader.GetString(5),
                        CreatedAt = reader.GetDateTime(6)
                    });
                }

                return Task.FromResult(OperationResultDto<List<SupplyOrderItemDto>>.Ok(orders));
            }
            catch (Exception ex)
            {
                return Task.FromResult(OperationResultDto<List<SupplyOrderItemDto>>.Fail(500, $"שגיאת שרת בטעינת הזמנות המלאי: {ex.Message}"));
            }
        }
    }
}
