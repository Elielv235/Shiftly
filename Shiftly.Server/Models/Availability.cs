using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shiftly.Server.Models
{
    [Table("Availabilities")]
    public class Availability
    {
        [Key]
        public int AvailabilityId { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }

        [Required]
        public DateTime TargetDate { get; set; }

        public bool CanWorkMorning { get; set; }

        public bool CanWorkEvening { get; set; }

        [MaxLength(255)]
        public string? Notes { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    }
}

