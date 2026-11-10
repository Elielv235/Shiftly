using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Shiftly.Shared.DTOs;
using Shiftly.Shared.Enums;

namespace Shiftly.Wpf.Services
{
    /// <summary>
    /// שירות התקשורת של הלקוח מול שרת ה-API (ASP.NET Core REST API)
    /// ארכיטקטורה שכבתית: הלקוח לעולם אינו פונה ישירות למסד הנתונים!
    /// </summary>
    public class ShiftlyApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        public static ShiftlyApiClient Instance { get; } = new ShiftlyApiClient();

        // כתובת שרת ה-API ברירת מחדל
        public string BaseUrl { get; set; } = "http://localhost:5148";

        // פרטי המשתמש המחובר כעת
        public LoginResponseDTO? CurrentUser { get; private set; }

        public ShiftlyApiClient()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl.TrimEnd('/') + "/")
            };
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public void SetCurrentUser(LoginResponseDTO? user)
        {
            CurrentUser = user;
        }

        public void Logout()
        {
            CurrentUser = null;
        }

        /// <summary>
        /// שליחת בקשת התחברות לשרת (POST /api/auth/login)
        /// </summary>
        public async Task<(bool Success, string Message, LoginResponseDTO? Data)> LoginAsync(LoginRequestDTO dto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/login", dto);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResponseDTO>(_jsonOptions);
                    CurrentUser = result;
                    return (true, "ההתחברות הצליחה!", result);
                }

                var errorMsg = await response.Content.ReadAsStringAsync();
                return (false, string.IsNullOrWhiteSpace(errorMsg) ? $"שגיאת שרת ({response.StatusCode})" : errorMsg, null);
            }
            catch (Exception ex)
            {
                return (false, $"שגיאת תקשורת עם השרת: {ex.Message}. ודא שהשרת פועל בכתובת {BaseUrl}", null);
            }
        }

        /// <summary>
        /// שליחת בקשת הרשמת עובד חדש לשרת (POST /api/auth/register)
        /// </summary>
        public async Task<(bool Success, string Message)> RegisterAsync(UserRegisterDTO dto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/register", dto);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return (true, content);
                }

                return (false, string.IsNullOrWhiteSpace(content) ? $"שגיאה בהרשמה ({response.StatusCode})" : content);
            }
            catch (Exception ex)
            {
                return (false, $"שגיאת תקשורת עם השרת: {ex.Message}");
            }
        }

        /// <summary>
        /// שליפת רשימת עובדים
        /// </summary>
        public async Task<List<UserListDTO>> GetUsersAsync()
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "api/admin/users");
                request.Headers.Add("X-User-Role", CurrentUser?.RoleName ?? "HRAdmin");
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<UserListDTO>>(json, _jsonOptions) ?? new();
                }
            }
            catch { }
            return new List<UserListDTO>();
        }

        public async Task<(bool Success, string Message, List<UserListDTO>? Data)> GetAllUsersAsync(string roleHeader)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "api/admin/users");
                request.Headers.Add("X-User-Role", roleHeader);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var users = await response.Content.ReadFromJsonAsync<List<UserListDTO>>(_jsonOptions);
                    return (true, "הרשימה נטענה בהצלחה", users ?? new List<UserListDTO>());
                }

                var error = await response.Content.ReadAsStringAsync();
                return (false, $"קוד שגיאה {((int)response.StatusCode)}: {error}", null);
            }
            catch (Exception ex)
            {
                return (false, $"שגיאת תקשורת: {ex.Message}", null);
            }
        }

        public async Task<bool> UpdateUserRoleAsync(int userId, string roleName)
        {
            try
            {
                int roleId = roleName switch
                {
                    "Guest" => 1,
                    "Worker" => 2,
                    "ShiftManager" => 3,
                    "HRAdmin" => 4,
                    _ => 2
                };

                using var request = new HttpRequestMessage(HttpMethod.Put, $"api/admin/users/{userId}/role");
                request.Headers.Add("X-User-Role", CurrentUser?.RoleName ?? "HRAdmin");
                request.Content = JsonContent.Create(new UpdateRoleDTO { RoleId = roleId });

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<(bool Success, string Message)> UpdateUserRoleAsync(int targetUserId, int newRoleId, string roleHeader)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Put, $"api/admin/users/{targetUserId}/role");
                request.Headers.Add("X-User-Role", roleHeader);
                request.Content = JsonContent.Create(new UpdateRoleDTO { RoleId = newRoleId });

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return (true, content);
                }

                return (false, $"קוד שגיאה {((int)response.StatusCode)}: {content}");
            }
            catch (Exception ex)
            {
                return (false, $"שגיאת תקשורת: {ex.Message}");
            }
        }

        public async Task<string> Test403ForbiddenAsync()
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "api/admin/users");
                // שולחים בקשת ניהול עובדים עם Role מזויף Worker כדי להוכיח שהשרת חוסם ב-403
                request.Headers.Add("X-User-Role", "Worker");

                var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var msg = await response.Content.ReadAsStringAsync();
                    return $"השרת חסם את הבקשה בהצלחה (403 Forbidden)!\n\nתגובת השרת:\n{msg}\n\nזהו יישום מדויק של חוק SEC-M08: חסימת עקיפת ממשק בשרת בלבד.";
                }

                return $"השרת החזיר קוד {response.StatusCode}.";
            }
            catch (Exception ex)
            {
                return $"שגיאת תקשורת: {ex.Message}";
            }
        }

        public async Task<DashboardStatsDTO?> GetDashboardStatsAsync()
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "api/admin/stats");
                request.Headers.Add("X-User-Role", CurrentUser?.RoleName ?? "HRAdmin");
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<DashboardStatsDTO>(json, _jsonOptions);
                }
            }
            catch { }
            return null;
        }

        public async Task<bool> UpdateUserStatusAsync(int userId, bool isActive)
        {
            try
            {
                var content = new StringContent(JsonSerializer.Serialize(isActive), Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"api/admin/users/{userId}/status", content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> SubmitAvailabilityAsync(WorkerAvailabilityDTO dto)
        {
            try
            {
                var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("api/availabilities/submit", content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<List<AvailabilityViewDTO>> GetBranchAvailabilitiesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/availabilities/branch");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<AvailabilityViewDTO>>(json, _jsonOptions) ?? new();
                }
            }
            catch { }
            return new List<AvailabilityViewDTO>();
        }

        // =========================================================================
        // CYCLE 3 REST API METHODS (ShiftSwapRequests & SupplyOrders)
        // Strict REST JSON only - zero direct DB access in WPF!
        // =========================================================================

        private void AttachUserIdentityHeader(HttpRequestMessage request)
        {
            if (CurrentUser != null && CurrentUser.UserId > 0)
            {
                request.Headers.Add("X-User-Id", CurrentUser.UserId.ToString());
            }
        }

        private static string FormatServerError(int statusCode, string rawBody)
        {
            string trimmed = rawBody.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(trimmed))
                return trimmed;

            return statusCode switch
            {
                401 => "שגיאה 401 (Unauthorized): יש להתחבר למערכת עם חשבון פעיל.",
                403 => "שגיאה 403 (Forbidden): אין לך הרשאה לביצוע פעולה זו.",
                404 => "שגיאה 404: המשאב המבוקש לא נמצא בשרת.",
                _ => $"שגיאת שרת ({statusCode})."
            };
        }

        public async Task<OperationResultDto<List<WorkerShiftAssignmentDto>>> GetWorkerAssignmentsAsync()
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "api/shiftswaps/my-assignments");
                AttachUserIdentityHeader(request);

                var response = await _httpClient.SendAsync(request);
                int statusCode = (int)response.StatusCode;
                string body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var data = JsonSerializer.Deserialize<List<WorkerShiftAssignmentDto>>(body, _jsonOptions) ?? new();
                    return OperationResultDto<List<WorkerShiftAssignmentDto>>.Ok(data);
                }

                return OperationResultDto<List<WorkerShiftAssignmentDto>>.Fail(statusCode, FormatServerError(statusCode, body));
            }
            catch (Exception ex)
            {
                return OperationResultDto<List<WorkerShiftAssignmentDto>>.Fail(500, $"שגיאת תקשורת עם השרת: {ex.Message}");
            }
        }

        public async Task<OperationResultDto<List<CoworkerOptionDto>>> GetSwapTargetWorkersAsync()
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "api/shiftswaps/coworkers");
                AttachUserIdentityHeader(request);

                var response = await _httpClient.SendAsync(request);
                int statusCode = (int)response.StatusCode;
                string body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var data = JsonSerializer.Deserialize<List<CoworkerOptionDto>>(body, _jsonOptions) ?? new();
                    return OperationResultDto<List<CoworkerOptionDto>>.Ok(data);
                }

                return OperationResultDto<List<CoworkerOptionDto>>.Fail(statusCode, FormatServerError(statusCode, body));
            }
            catch (Exception ex)
            {
                return OperationResultDto<List<CoworkerOptionDto>>.Fail(500, $"שגיאת תקשורת עם השרת: {ex.Message}");
            }
        }

        public async Task<OperationResultDto> SubmitShiftSwapAsync(ShiftSwapRequestCreateDto dto)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "api/shiftswaps");
                AttachUserIdentityHeader(request);
                request.Content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                int statusCode = (int)response.StatusCode;
                string body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return OperationResultDto.Ok("בקשת החלפת המשמרת נשלחה בהצלחה וממתינה לאישור מנהל!");
                }

                return OperationResultDto.Fail(statusCode, FormatServerError(statusCode, body));
            }
            catch (Exception ex)
            {
                return OperationResultDto.Fail(500, $"שגיאת תקשורת עם השרת: {ex.Message}");
            }
        }

        public async Task<OperationResultDto<List<ShiftSwapItemDto>>> GetPendingShiftSwapsAsync()
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "api/shiftswaps/pending");
                AttachUserIdentityHeader(request);

                var response = await _httpClient.SendAsync(request);
                int statusCode = (int)response.StatusCode;
                string body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var data = JsonSerializer.Deserialize<List<ShiftSwapItemDto>>(body, _jsonOptions) ?? new();
                    return OperationResultDto<List<ShiftSwapItemDto>>.Ok(data);
                }

                return OperationResultDto<List<ShiftSwapItemDto>>.Fail(statusCode, FormatServerError(statusCode, body));
            }
            catch (Exception ex)
            {
                return OperationResultDto<List<ShiftSwapItemDto>>.Fail(500, $"שגיאת תקשורת עם השרת: {ex.Message}");
            }
        }

        public async Task<OperationResultDto> ReviewShiftSwapAsync(ShiftSwapReviewDto dto)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "api/shiftswaps/review");
                AttachUserIdentityHeader(request);
                request.Content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                int statusCode = (int)response.StatusCode;
                string body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return OperationResultDto.Ok(dto.IsApproved
                        ? "בקשת ההחלפה אושרה והשיבוץ עודכן במסד הנתונים."
                        : "בקשת ההחלפה נדחתה.");
                }

                return OperationResultDto.Fail(statusCode, FormatServerError(statusCode, body));
            }
            catch (Exception ex)
            {
                return OperationResultDto.Fail(500, $"שגיאת תקשורת עם השרת: {ex.Message}");
            }
        }

        public async Task<OperationResultDto<List<SupplyOrderItemDto>>> GetSupplyOrdersAsync()
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "api/supplyorders");
                AttachUserIdentityHeader(request);

                var response = await _httpClient.SendAsync(request);
                int statusCode = (int)response.StatusCode;
                string body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var data = JsonSerializer.Deserialize<List<SupplyOrderItemDto>>(body, _jsonOptions) ?? new();
                    return OperationResultDto<List<SupplyOrderItemDto>>.Ok(data);
                }

                return OperationResultDto<List<SupplyOrderItemDto>>.Fail(statusCode, FormatServerError(statusCode, body));
            }
            catch (Exception ex)
            {
                return OperationResultDto<List<SupplyOrderItemDto>>.Fail(500, $"שגיאת תקשורת עם השרת: {ex.Message}");
            }
        }

        public async Task<OperationResultDto<SupplyOrderItemDto>> CreateSupplyOrderAsync(SupplyOrderCreateDto dto)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "api/supplyorders");
                AttachUserIdentityHeader(request);
                request.Content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                int statusCode = (int)response.StatusCode;
                string body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var created = JsonSerializer.Deserialize<SupplyOrderItemDto>(body, _jsonOptions);
                    return OperationResultDto<SupplyOrderItemDto>.Ok(created!, "הזמנת המלאי נוצרה ונשמרה בהצלחה!");
                }

                return OperationResultDto<SupplyOrderItemDto>.Fail(statusCode, FormatServerError(statusCode, body));
            }
            catch (Exception ex)
            {
                return OperationResultDto<SupplyOrderItemDto>.Fail(500, $"שגיאת תקשורת עם השרת: {ex.Message}");
            }
        }
    }
}
