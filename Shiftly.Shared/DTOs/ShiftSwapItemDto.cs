using System;

namespace Shiftly.Shared.DTOs
{
    /// <summary>
    /// Read model representing a shift swap request for managers and dashboards (Cycle 3).
    /// </summary>
    public class ShiftSwapItemDto
    {
        public int SwapId { get; set; }
        public int AssignmentId { get; set; }
        public string RequesterName { get; set; } = string.Empty;
        public string TargetWorkerName { get; set; } = string.Empty;
        public DateTime ShiftDate { get; set; }
        public string ShiftType { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
    }
}
