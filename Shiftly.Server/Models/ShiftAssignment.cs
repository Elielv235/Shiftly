using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shiftly.Server.Models
{
    [Table("ShiftAssignments")]
    public class ShiftAssignment
    {
        [Key]
        public int AssignmentId { get; set; }

        [Required]
        public int ShiftId { get; set; }

        [ForeignKey(nameof(ShiftId))]
        public virtual Shift? Shift { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }

        [Required]
        public int AssignedByUserId { get; set; }

        [ForeignKey(nameof(AssignedByUserId))]
        public virtual User? AssignedByUser { get; set; }

        [Required]
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        public bool IsConfirmed { get; set; }
    }
}
