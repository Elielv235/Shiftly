using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shiftly.Shared.DTOs;
using Shiftly.Shared.Interfaces;

namespace Shiftly.Server.Controllers
{
    /// <summary>
    /// REST API controller for Cycle 3 ShiftSwapRequests and Worker Assignments.
    /// Delegates all DB and authorization logic to IShiftlyService (zero direct DB access in controller).
    /// </summary>
    [ApiController]
    [Route("api/shiftswaps")]
    public class ShiftSwapsController : ControllerBase
    {
        private readonly IShiftlyService _shiftlyService;

        public ShiftSwapsController(IShiftlyService shiftlyService)
        {
            _shiftlyService = shiftlyService;
        }

        private bool TryGetAuthenticatedUserId(out int userId)
        {
            userId = 0;
            if (!Request.Headers.TryGetValue("X-User-Id", out var rawHeader))
                return false;

            return int.TryParse(rawHeader.ToString(), out userId) && userId > 0;
        }

        [HttpGet("my-assignments")]
        public async Task<IActionResult> GetMyAssignments()
        {
            if (!TryGetAuthenticatedUserId(out int userId))
                return Unauthorized("גישה לא מאומתת (401): חסר מזהה משתמש מחובר.");

            var result = await _shiftlyService.GetWorkerAssignmentsAsync(userId);
            return result.Success ? Ok(result.Data) : StatusCode(result.StatusCode, result.Message);
        }

        [HttpGet("coworkers")]
        public async Task<IActionResult> GetSwapTargetWorkers()
        {
            if (!TryGetAuthenticatedUserId(out int userId))
                return Unauthorized("גישה לא מאומתת (401): חסר מזהה משתמש מחובר.");

            var result = await _shiftlyService.GetSwapTargetWorkersAsync(userId);
            return result.Success ? Ok(result.Data) : StatusCode(result.StatusCode, result.Message);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitSwapRequest([FromBody] ShiftSwapRequestCreateDto dto)
        {
            if (!TryGetAuthenticatedUserId(out int userId))
                return Unauthorized("גישה לא מאומתת (401): יש להתחבר למערכת לפני הגשת בקשת החלפה.");

            var result = await _shiftlyService.SubmitShiftSwapRequestAsync(userId, dto);
            return result.Success
                ? Ok(new { message = result.Message })
                : StatusCode(result.StatusCode, result.Message);
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingSwaps()
        {
            if (!TryGetAuthenticatedUserId(out int userId))
                return Unauthorized("גישה לא מאומתת (401): יש להתחבר למערכת.");

            var result = await _shiftlyService.GetPendingShiftSwapRequestsAsync(userId);
            return result.Success ? Ok(result.Data) : StatusCode(result.StatusCode, result.Message);
        }

        [HttpPost("review")]
        public async Task<IActionResult> ReviewSwapRequest([FromBody] ShiftSwapReviewDto dto)
        {
            if (!TryGetAuthenticatedUserId(out int userId))
                return Unauthorized("גישה לא מאומתת (401): יש להתחבר למערכת.");

            var result = await _shiftlyService.ReviewShiftSwapRequestAsync(userId, dto);
            return result.Success
                ? Ok(new { message = result.Message })
                : StatusCode(result.StatusCode, result.Message);
        }
    }
}
