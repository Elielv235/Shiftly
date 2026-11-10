using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shiftly.Server.Data;
using Shiftly.Server.Models;
using Shiftly.Shared.DTOs;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shiftly.Server.Controllers
{
    [ApiController]
    [Route("api/availabilities")]
    public class AvailabilitiesController : ControllerBase
    {
        private readonly ShiftlyDbContext _context;

        public AvailabilitiesController(ShiftlyDbContext context)
        {
            _context = context;
        }

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitAvailability([FromBody] WorkerAvailabilityDTO dto)
        {
            if (dto == null || dto.UserId <= 0)
                return BadRequest("Invalid availability payload.");

            if (!dto.CanWorkMorning && !dto.CanWorkEvening)
                return BadRequest("At least one shift option must be selected.");

            var record = new Availability
            {
                UserId = dto.UserId,
                TargetDate = dto.TargetDate,
                CanWorkMorning = dto.CanWorkMorning,
                CanWorkEvening = dto.CanWorkEvening,
                Notes = dto.Notes,
                Status = "Pending"
            };

            _context.Availabilities.Add(record);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Availability submitted successfully.", id = record.AvailabilityId });
        }

        [HttpGet("branch")]
        public async Task<ActionResult<List<AvailabilityViewDTO>>> GetBranchAvailabilities()
        {
            var list = await _context.Availabilities
                .Include(a => a.User)
                .Select(a => new AvailabilityViewDTO
                {
                    AvailabilityId = a.AvailabilityId,
                    UserId = a.UserId,
                    WorkerName = a.User != null ? a.User.FullName : "עובד",
                    TargetDate = a.TargetDate,
                    CanWorkMorning = a.CanWorkMorning,
                    CanWorkEvening = a.CanWorkEvening,
                    Notes = a.Notes,
                    Status = a.Status
                })
                .ToListAsync();

            return Ok(list);
        }
    }
}

