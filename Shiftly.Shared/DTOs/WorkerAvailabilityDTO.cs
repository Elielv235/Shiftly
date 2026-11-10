using System;

namespace Shiftly.Shared.DTOs
{
    public class WorkerAvailabilityDTO
    {
        public int UserId { get; set; }
        public DateTime TargetDate { get; set; }
        public bool CanWorkMorning { get; set; }
        public bool CanWorkEvening { get; set; }
        public string? Notes { get; set; }
    }
}

