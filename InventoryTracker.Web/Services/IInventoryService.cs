using InventoryTracker.Web.Models;

namespace InventoryTracker.Web.Services
{

    public record ServiceResult(bool Success, string? ErrorMessage = null);

    public interface IInventoryService
    {
        Task<List<ProductSummaryDto>> GetProductsAsync(string? searchTerm, bool includeInactive = true);
        Task<ProductDetailDto?> GetProductDetailAsync(int id);
        Task<string> GenerateNextSkuAsync();
        Task<ServiceResult> CreateProductAsync(ProductFormDto dto);
        Task<ServiceResult> UpdateProductAsync(int id, ProductFormDto dto);
        Task<ServiceResult> ToggleActiveAsync(int id);
        Task<ServiceResult> RecordMovementAsync(int productId, MovementType type, int quantity, string? note);
    }


    public class ProductSummaryDto
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int ReorderLevel { get; set; }
        public bool IsActive { get; set; }
        public bool IsLowStock => CurrentStock <= ReorderLevel;
    }


    public class ProductDetailDto : ProductSummaryDto
    {
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<StockMovement> Movements { get; set; } = new();
    }


    public class ProductFormDto
    {
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ReorderLevel { get; set; }
    }

}
