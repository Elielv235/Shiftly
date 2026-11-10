namespace Shiftly.Shared.DTOs
{
    /// <summary>
    /// Request payload for creating a branch supply order (Cycle 3).
    /// </summary>
    public class SupplyOrderCreateDto
    {
        public int CreatedByUserId { get; set; }
        public string ItemsSummary { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}
