using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shiftly.Shared.DTOs
{
    // בקשת כניסה
    public class LoginRequestDTO
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    // תשובת כניסה מהשרת ללקוח
    public class LoginResponseDTO
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }

    // טופס הרשמה
    // SEC-M11: אין שדה Role בטופס! השרת קובע תמיד עובד רגיל (Worker) כברירת מחדל
    public class UserRegisterDTO
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }

    // פריט ברשימת עובדים לאדמין
    public class UserListDTO
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    // עדכון תפקיד על ידי אדמין בלבד
    public class UpdateRoleDTO
    {
        public int RoleId { get; set; }
    }
}
