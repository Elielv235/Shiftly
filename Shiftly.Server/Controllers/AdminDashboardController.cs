using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shiftly.Server.Data;
using Shiftly.Shared.DTOs;
using System.Linq;
using System.Threading.Tasks;

namespace Shiftly.Server.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminDashboardController : ControllerBase
    {
        private readonly ShiftlyDbContext _context;

        public AdminDashboardController(ShiftlyDbContext context)
        {
            _context = context;
        }

        [HttpGet("stats")]
        public async Task<ActionResult<DashboardStatsDTO>> GetDashboardStats([FromHeader(Name = "X-User-Role")] string? userRole)
        {
            // SEC-M08: Strict backend enforcement. Non-HRAdmin requests return 403 Forbidden.
            if (userRole != "HRAdmin")
            {
                return StatusCode(403, "Access denied. HRAdmin role required.");
            }

            var stats = new DashboardStatsDTO
            {
                ActiveWorkersCount = await _context.Users.CountAsync(u => u.IsActive),
                PendingAvailabilitiesCount = await _context.Availabilities.CountAsync(a => a.Status == "Pending"),
                MorningRequestsCount = await _context.Availabilities.CountAsync(a => a.CanWorkMorning),
                EveningRequestsCount = await _context.Availabilities.CountAsync(a => a.CanWorkEvening),
                DefinedShiftsCount = await _context.Shifts.CountAsync()
            };

            return Ok(stats);
        }
    }
}

