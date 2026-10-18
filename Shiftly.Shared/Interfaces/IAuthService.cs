using System.Collections.Generic;
using System.Threading.Tasks;
using Shiftly.Shared.DTOs;

namespace Shiftly.Shared.Interfaces
{
    public interface IAuthService
    {
        // כניסה למערכת ואימות סיסמה מול מסד הנתונים
        Task<LoginResponseDTO?> AuthenticateAsync(LoginRequestDTO request);

        // הרשמת עובד חדש למערכת
        Task<bool> RegisterUserAsync(UserRegisterDTO request);

        // שליפת כל המשתמשים (עבור דשבורד מנהל בלבד - אכיפת SEC-M08 בשרת)
        Task<IEnumerable<UserListDTO>> GetAllUsersAsync();

        // עדכון הרשאת משתמש (אדמין בלבד - אכיפת SEC-M13)
        Task<bool> UpdateUserRoleAsync(int targetUserId, int newRoleId);
    }
}