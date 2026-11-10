using System;

namespace Shiftly.Shared.DTOs
{
    /// <summary>
    /// Read model representing a branch supply order (Cycle 3).
    /// </summary>
    public class SupplyOrderItemDto
    {
        public int OrderId { get; set; }
        public int CreatedByUserId { get; set; }
        public string ManagerName { get; set; } = string.Empty;
        public string ItemsSummary { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
