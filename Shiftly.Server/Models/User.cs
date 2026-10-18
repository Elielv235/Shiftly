using System;

namespace Shiftly.Server.Models
{
    /// <summary>
    /// ישות משתמש התואמת לטבלת Users במסד הנתונים
    /// </summary>
    public class User
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;

        // שדות אבטחה: גיבוב ומלח בלבד - אין שמירה של סיסמה גלויה!
        public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
        public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();

        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;

        // מפתח זר לשיוך לתפקיד (Roles)
        public int RoleId { get; set; }
        public Role? Role { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
