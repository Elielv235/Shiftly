namespace Shiftly.Shared.DTOs
{
    /// <summary>
    /// Request payload for a worker submitting a shift swap request (Cycle 3).
    /// </summary>
    public class ShiftSwapRequestCreateDto
    {
        public int AssignmentId { get; set; }
        public int RequesterUserId { get; set; }
        public int TargetUserId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
