using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shiftly.Shared.DTOs;
using Shiftly.Shared.Interfaces;

namespace Shiftly.Server.Controllers
{
    /// <summary>
    /// REST API controller for Cycle 3 SupplyOrders.
    /// Delegates all DB and role-authorization logic to IShiftlyService (zero direct DB access in controller).
    /// </summary>
    [ApiController]
    [Route("api/supplyorders")]
    public class SupplyOrdersController : ControllerBase
    {
        private readonly IShiftlyService _shiftlyService;

        public SupplyOrdersController(IShiftlyService shiftlyService)
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

        [HttpGet]
        public async Task<IActionResult> GetSupplyOrders()
        {
            if (!TryGetAuthenticatedUserId(out int userId))
                return Unauthorized("גישה לא מאומתת (401): יש להתחבר למערכת.");

            var result = await _shiftlyService.GetSupplyOrdersAsync(userId);
            return result.Success ? Ok(result.Data) : StatusCode(result.StatusCode, result.Message);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSupplyOrder([FromBody] SupplyOrderCreateDto dto)
        {
            if (!TryGetAuthenticatedUserId(out int userId))
                return Unauthorized("גישה לא מאומתת (401): יש להתחבר למערכת.");

            var result = await _shiftlyService.CreateSupplyOrderAsync(userId, dto);
            return result.Success
                ? Ok(result.Data)
                : StatusCode(result.StatusCode, result.Message);
        }
    }
}
