using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shiftly.Server.Data;
using Shiftly.Server.Models;
using Shiftly.Server.Security;
using Shiftly.Shared.DTOs;
using Shiftly.Shared.Enums;
using Shiftly.Shared.Interfaces;

namespace Shiftly.Server.Services
{
    /// <summary>
    /// מימוש שכבת השירות (Service Layer) לאימות, הרשמה וניהול משתמשים
    /// מיישמת את החוזה IAuthService מהספרייה המשותפת Shiftly.Shared
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly ShiftlyDbContext _context;

        public AuthService(ShiftlyDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// אימות פרטי התחברות של משתמש מול מסד הנתונים (SEC-M01, SEC-M04, SEC-M06)
        /// </summary>
        public async Task<LoginResponseDTO?> AuthenticateAsync(LoginRequestDTO request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return null;
            }

            // שליפת המשתמש כולל התפקיד שלו
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.Trim().ToLower());

            if (user == null)
            {
                // משתמש לא קיים במערכת
                return null;
            }

            // בדיקה האם חשבון המשתמש פעיל
            if (!user.IsActive)
            {
                return null;
            }

            // אימות הסיסמה המוזנת באמצעות גיבוב ומלח
            bool isPasswordValid = PasswordSecurityHelper.VerifyPasswordHash(
                request.Password,
                user.PasswordHash,
                user.PasswordSalt);

            if (!isPasswordValid)
            {
                // סיסמה שגויה
                return null;
            }

            // החזרת DTO מותאם לקליינט - ללא חשיפת פרטי אבטחה או Hash
            return new LoginResponseDTO
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                RoleName = user.Role?.RoleName ?? nameof(UserRoleType.Worker)
            };
        }

        /// <summary>
        /// הרשמת משתמש חדש במערכת (SEC-M01, SEC-M02, SEC-M11)
        /// </summary>
        public async Task<bool> RegisterUserAsync(UserRegisterDTO request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                request.Password.Length < 6)
            {
                return false;
            }

            // בדיקת כפילות שם משתמש במסד הנתונים
            bool exists = await _context.Users
                .AnyAsync(u => u.Username.ToLower() == request.Username.Trim().ToLower());

            if (exists)
            {
                return false;
            }

            // יצירת גיבוב ומלח מאובטחים
            PasswordSecurityHelper.CreatePasswordHash(request.Password, out byte[] hash, out byte[] salt);

            // SEC-M11: ברירת המחדל תמיד תהיה עובד רגיל (Worker). המשתמש אינו יכול לקבוע את ה-Role שלו ברישום!
            var newUser = new User
            {
                Username = request.Username.Trim(),
                PasswordHash = hash,
                PasswordSalt = salt,
                FullName = request.FullName.Trim(),
                Email = request.Email.Trim(),
                PhoneNumber = request.PhoneNumber.Trim(),
                RoleId = (int)UserRoleType.Worker, // ברירת מחדל: Worker = 2
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return true;
        }

        /// <summary>
        /// שליפת רשימת כל העובדים עבור דשבורד מנהל (SEC-M08)
        /// </summary>
        public async Task<IEnumerable<UserListDTO>> GetAllUsersAsync()
        {
            return await _context.Users
                .Include(u => u.Role)
                .AsNoTracking()
                .OrderBy(u => u.UserId)
                .Select(u => new UserListDTO
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    FullName = u.FullName,
                    Email = u.Email,
                    RoleName = u.Role != null ? u.Role.RoleName : nameof(UserRoleType.Worker),
                    IsActive = u.IsActive
                })
                .ToListAsync();
        }

        /// <summary>
        /// עדכון תפקיד של משתמש - שמור לאדמין בלבד (SEC-M13)
        /// </summary>
        public async Task<bool> UpdateUserRoleAsync(int targetUserId, int newRoleId)
        {
            // בדיקה שהתפקיד המבוקש קיים בטבלת Roles
            bool roleExists = await _context.Roles.AnyAsync(r => r.RoleId == newRoleId);
            if (!roleExists)
            {
                return false;
            }

            var user = await _context.Users.FindAsync(targetUserId);
            if (user == null)
            {
                return false;
            }

            user.RoleId = newRoleId;
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
