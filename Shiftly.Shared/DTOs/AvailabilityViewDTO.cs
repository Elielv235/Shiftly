using System;

namespace Shiftly.Shared.DTOs
{
    public class AvailabilityViewDTO
    {
        public int AvailabilityId { get; set; }
        public int UserId { get; set; }
        public string WorkerName { get; set; } = string.Empty;
        public DateTime TargetDate { get; set; }
        public bool CanWorkMorning { get; set; }
        public bool CanWorkEvening { get; set; }
        public string? Notes { get; set; }
        public string Status { get; set; } = "Pending";
    }
}

