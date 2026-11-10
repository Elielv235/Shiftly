namespace Shiftly.Shared.DTOs
{
    /// <summary>
    /// Manager decision payload for approving or rejecting a shift swap request (Cycle 3).
    /// </summary>
    public class ShiftSwapReviewDto
    {
        public int SwapId { get; set; }
        public bool IsApproved { get; set; }
        public int ReviewerUserId { get; set; }
    }
}
