using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shiftly.Server.Models
{
    [Table("Shifts")]
    public class Shift
    {
        [Key]
        public int ShiftId { get; set; }

        [Required]
        public DateTime ShiftDate { get; set; }

        [Required]
        [MaxLength(20)]
        public string ShiftType { get; set; } = "Morning"; // Morning, Evening, Night

        public int RequiredWorkers { get; set; } = 3;

        public int CreatedByUserId { get; set; }

        [ForeignKey(nameof(CreatedByUserId))]
        public virtual User? CreatedByUser { get; set; }
    }
}

