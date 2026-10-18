using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Shiftly.Shared.DTOs;

namespace Shiftly.Wpf.Services
{
    /// <summary>
    /// שירות התקשורת של הלקוח מול שרת ה-API (ASP.NET Core REST API)
    /// ארכיטקטורה שכבתית: הלקוח לעולם אינו פונה ישירות למסד הנתונים!
    /// </summary>
    public class ShiftlyApiClient
    {
        private readonly HttpClient _httpClient;
        public static ShiftlyApiClient Instance { get; } = new ShiftlyApiClient();

        // כתובת שרת ה-API ברירת מחדל
        public string BaseUrl { get; set; } = "http://localhost:5148";

        // פרטי המשתמש המחובר כעת
        public LoginResponseDTO? CurrentUser { get; private set; }

        public ShiftlyApiClient()
        {
            _httpClient = new HttpClient();
        }

        public void SetCurrentUser(LoginResponseDTO? user)
        {
            CurrentUser = user;
        }

        /// <summary>
        /// שליחת בקשת התחברות לשרת (POST /api/auth/login)
        /// </summary>
        public async Task<(bool Success, string Message, LoginResponseDTO? Data)> LoginAsync(LoginRequestDTO dto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/auth/login", dto);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResponseDTO>();
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
                var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/auth/register", dto);
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
        /// שליפת כל העובדים - שמור ל-HRAdmin בלבד (GET /api/admin/users)
        /// מדגים אכיפת הרשאות ברמת השרת (SEC-M08)
        /// </summary>
        public async Task<(bool Success, string Message, List<UserListDTO>? Data)> GetAllUsersAsync(string roleHeader)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/admin/users");
                // שליחת ה-Role של המשתמש ב-Header (השרת הוא זה שאוכף 403 Forbidden אם אינו מנהל!)
                request.Headers.Add("X-User-Role", roleHeader);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var users = await response.Content.ReadFromJsonAsync<List<UserListDTO>>();
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

        /// <summary>
        /// עדכון תפקיד של עובד על ידי אדמין (PUT /api/admin/users/{id}/role)
        /// </summary>
        public async Task<(bool Success, string Message)> UpdateUserRoleAsync(int targetUserId, int newRoleId, string roleHeader)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Put, $"{BaseUrl}/api/admin/users/{targetUserId}/role");
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
    }
}
