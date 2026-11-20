using System.ComponentModel.DataAnnotations;

namespace InventoryTracker.Web.Models;

public class Product
{
    [Key]
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Range(0, int.MaxValue)]
    public int ReorderLevel { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // SQL Server Optimistic Concurrency Token (simultaneous requests pakadne ke liye)
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();
}