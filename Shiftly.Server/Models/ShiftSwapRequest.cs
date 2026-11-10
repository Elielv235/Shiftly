using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shiftly.Server.Models
{
    [Table("ShiftSwapRequests")]
    public class ShiftSwapRequest
    {
        [Key]
        public int SwapId { get; set; }

        [Required]
        public int AssignmentId { get; set; }

        [ForeignKey(nameof(AssignmentId))]
        public virtual ShiftAssignment? Assignment { get; set; }

        [Required]
        public int TargetUserId { get; set; }

        [ForeignKey(nameof(TargetUserId))]
        public virtual User? TargetUser { get; set; }

        [MaxLength(255)]
        public string? Reason { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

        public int? ReviewedByUserId { get; set; }

        [ForeignKey(nameof(ReviewedByUserId))]
        public virtual User? ReviewedByUser { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
