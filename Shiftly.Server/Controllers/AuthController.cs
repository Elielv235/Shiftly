using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shiftly.Shared.DTOs;
using Shiftly.Shared.Interfaces;

namespace Shiftly.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        // הזרקת תלות (Dependency Injection) של חוזה השירות מהספרייה המשותפת
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// אימות משתמש והתחברות למערכת (SEC-M01, SEC-M04, SEC-M06)
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO request)
        {
            // בדיקת קלט קפדנית ברמת השרת (גם אם הלקוח עוקף את המסך)
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("שם משתמש וסיסמה הם שדות חובה.");
            }

            var result = await _authService.AuthenticateAsync(request);
            if (result == null)
            {
                // SEC-M04: החזרת HTTP 401 במקרה של פרטים שגויים
                return Unauthorized("שם משתמש או סיסמה שגויים.");
            }

            return Ok(result);
        }

        /// <summary>
        /// הרשמת עובד חדש למערכת (SEC-M11)
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRegisterDTO request)
        {
            // SEC-M01, SEC-M02: בדיקת תקינות הקלט בשרת
            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                request.Password.Length < 6)
            {
                return BadRequest("קלט לא תקין או סיסמה קצרה מדי (מינימום 6 תווים).");
            }

            var success = await _authService.RegisterUserAsync(request);
            if (!success)
            {
                return Conflict("שם המשתמש כבר קיים במערכת.");
            }

            return Ok("המשתמש נרשם בהצלחה כעובד (Worker).");
        }
    }
}