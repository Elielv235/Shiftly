using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shiftly.Server.Data;
using Shiftly.Shared.DTOs;
using Shiftly.Shared.Interfaces;

namespace Shiftly.Server.Controllers
{
    [ApiController]
    [Route("api/admin/users")]
    public class AdminUsersController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ShiftlyDbContext _context;

        public AdminUsersController(IAuthService authService, ShiftlyDbContext context)
        {
            _authService = authService;
            _context = context;
        }

        /// <summary>
        /// שליפת רשימת כל העובדים - שמור ל-HRAdmin בלבד (SEC-M08)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllUsers([FromHeader(Name = "X-User-Role")] string? role)
        {
            // אכיפת הרשאות בשרת בלבד: אורח או עובד מקבלים חסימת 403
            if (role != "HRAdmin")
            {
                return StatusCode(403, "גישה נדחתה: פעולה זו מורשית למנהל כוח אדם (HRAdmin) בלבד.");
            }

            var users = await _authService.GetAllUsersAsync();
            return Ok(users);
        }

        /// <summary>
        /// עדכון הרשאת משתמש (Worker/ShiftManager/HRAdmin) - שמור ל-HRAdmin בלבד (SEC-M13)
        /// </summary>
        [HttpPut("{id}/role")]
        public async Task<IActionResult> UpdateUserRole(int id, [FromBody] UpdateRoleDTO dto, [FromHeader(Name = "X-User-Role")] string? role)
        {
            // אכיפת SEC-M13: משתמש לא יכול לשנות Role לעצמו או לאחר ללא הרשאת HRAdmin
            if (role != "HRAdmin")
            {
                return StatusCode(403, "גישה נדחתה: שינוי תפקיד שמור לאדמין בלבד.");
            }

            var success = await _authService.UpdateUserRoleAsync(id, dto.RoleId);
            if (!success)
            {
                return NotFound("המשתמש המבוקש לא נמצא במסד הנתונים.");
            }

            return Ok("תפקיד המשתמש עודכן בהצלחה במסד הנתונים.");
        }

        /// <summary>
        /// שינוי סטטוס פעיל/מושעה של עובד (FR-06/FR-07)
        /// </summary>
        [HttpPut("{id}/status")]
        public async Task<IActionResult> ToggleUserStatus(int id, [FromBody] bool isActive)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.IsActive = isActive;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Status updated successfully", isActive = user.IsActive });
        }
    }
}
