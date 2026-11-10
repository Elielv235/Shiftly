using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shiftly.Server.Services;
using Shiftly.Shared.DTOs;

namespace Shiftly.Server.Controllers
{
    [ApiController]
    [Route("api/availabilities")]
    [SupportedOSPlatform("windows")]
    public class AvailabilitiesController : ControllerBase
    {
        private readonly BranchService _branchService;

        public AvailabilitiesController(BranchService branchService)
        {
            _branchService = branchService;
        }

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitAvailability([FromBody] WorkerAvailabilityDTO dto)
        {
            if (dto == null || dto.UserId <= 0)
                return BadRequest("Invalid availability payload.");

            if (!dto.CanWorkMorning && !dto.CanWorkEvening)
                return BadRequest("At least one shift option must be selected.");

            int newId = await _branchService.SubmitAvailabilityAsync(dto);
            return Ok(new { message = "Availability submitted successfully.", id = newId });
        }

        [HttpGet("branch")]
        public async Task<ActionResult<List<AvailabilityViewDTO>>> GetBranchAvailabilities()
        {
            var list = await _branchService.GetBranchAvailabilitiesAsync();
            return Ok(list);
        }
    }
}
