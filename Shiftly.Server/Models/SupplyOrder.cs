using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shiftly.Server.Models
{
    [Table("SupplyOrders")]
    public class SupplyOrder
    {
        [Key]
        public int OrderId { get; set; }

        [Required]
        public int CreatedByUserId { get; set; }

        [ForeignKey(nameof(CreatedByUserId))]
        public virtual User? CreatedByUser { get; set; }

        [Required]
        [MaxLength(500)]
        public string ItemsSummary { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "Pending"; // Pending, Delivered, Cancelled

        [MaxLength(255)]
        public string? Notes { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
