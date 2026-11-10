using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shiftly.Server.Services;
using Shiftly.Shared.DTOs;

namespace Shiftly.Server.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [SupportedOSPlatform("windows")]
    public class AdminDashboardController : ControllerBase
    {
        private readonly BranchService _branchService;

        public AdminDashboardController(BranchService branchService)
        {
            _branchService = branchService;
        }

        [HttpGet("stats")]
        public async Task<ActionResult<DashboardStatsDTO>> GetDashboardStats([FromHeader(Name = "X-User-Role")] string? userRole)
        {
            // SEC-M08: Strict backend enforcement. Non-HRAdmin requests return 403 Forbidden.
            if (userRole != "HRAdmin")
            {
                return StatusCode(403, "Access denied. HRAdmin role required.");
            }

            var stats = await _branchService.GetDashboardStatsAsync();
            return Ok(stats);
        }
    }
}
