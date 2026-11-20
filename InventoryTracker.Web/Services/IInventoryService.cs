using InventoryTracker.Web.Models;

namespace InventoryTracker.Web.Services
{

    public record ServiceResult(bool Success, string? ErrorMessage = null);

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }

    public interface IInventoryService
    {
        Task<PagedResult<ProductSummaryDto>> GetProductsAsync(string? searchTerm, bool includeInactive = true, bool lowStockOnly = false, int page = 1, int pageSize = 10);
        Task<ProductDetailDto?> GetProductDetailAsync(int id, int movementPage = 1, int movementPageSize = 10);
        Task<List<StockMovement>> GetAllMovementsForExportAsync(int productId);
        Task<string> GenerateNextSkuAsync();
        Task<ServiceResult> CreateProductAsync(ProductFormDto dto);
        Task<ServiceResult> UpdateProductAsync(int id, ProductFormDto dto);
        Task<ServiceResult> ToggleActiveAsync(int id);
        Task<ServiceResult> RecordMovementAsync(int productId, MovementType type, int quantity, string? note);
        Task<DashboardStatsDto> GetDashboardStatsAsync();
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
        public PagedResult<StockMovement> PagedMovements { get; set; } = new();
    }

    public class ProductFormDto
    {
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ReorderLevel { get; set; }
    }

    public record DashboardStatsDto(int TotalActiveSkus, int BelowReorderCount, int TotalUnitsOnHand);
}
