using System.ComponentModel.DataAnnotations;

namespace InventoryTracker.Web.Models;

public enum MovementType
{
    In = 1,
    Out = 2
}

public class StockMovement
{
    [Key]
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Required]
    public MovementType Type { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be a positive integer.")]
    public int Quantity { get; set; }

    [StringLength(250)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

