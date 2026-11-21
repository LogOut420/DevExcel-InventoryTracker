using InventoryTracker.Web.DataContext;
using InventoryTracker.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryTracker.Web.Services;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _db;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(AppDbContext db, ILogger<InventoryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync()
    {
        var activeProducts = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new
            {
                p.ReorderLevel,
                Stock = p.Movements.Sum(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity)
            })
            .ToListAsync();

        return new DashboardStatsDto(
            TotalActiveSkus: activeProducts.Count,
            BelowReorderCount: activeProducts.Count(p => p.Stock <= p.ReorderLevel),
            TotalUnitsOnHand: activeProducts.Sum(p => p.Stock)
        );
    }


    public async Task<PagedResult<ProductSummaryDto>> GetProductsAsync(
        string? searchTerm, bool includeInactive = true, bool lowStockOnly = false, int page = 1, int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Products.AsNoTracking().AsQueryable();

        if (!includeInactive)
            query = query.Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(p => p.Sku.Contains(term) || p.Name.Contains(term));
        }

        var projected = query.Select(p => new ProductSummaryDto
        {
            Id = p.Id,
            Sku = p.Sku,
            Name = p.Name,
            ReorderLevel = p.ReorderLevel,
            IsActive = p.IsActive,
            CurrentStock = p.Movements.Sum(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity)
        });

        if (lowStockOnly)
            projected = projected.Where(p => p.IsActive && p.CurrentStock <= p.ReorderLevel);

        var totalCount = await projected.CountAsync();
        var items = await projected
            .OrderBy(p => p.Sku)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<ProductSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }


    public async Task<ProductDetailDto?> GetProductDetailAsync(int id, int movementPage = 1, int movementPageSize = 10)
    {
        movementPage = Math.Max(1, movementPage);
        movementPageSize = Math.Clamp(movementPageSize, 1, 100);

        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null) return null;

        var currentStock = await _db.StockMovements
            .Where(m => m.ProductId == id)
            .SumAsync(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity);

        var movementsQuery = _db.StockMovements
            .AsNoTracking()
            .Where(m => m.ProductId == id);

        var totalMovements = await movementsQuery.CountAsync();
        var pagedItems = await movementsQuery
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Skip((movementPage - 1) * movementPageSize)
            .Take(movementPageSize)
            .ToListAsync();

        return new ProductDetailDto
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            Description = product.Description,
            ReorderLevel = product.ReorderLevel,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            CurrentStock = currentStock,
            PagedMovements = new PagedResult<StockMovement>
            {
                Items = pagedItems,
                Page = movementPage,
                PageSize = movementPageSize,
                TotalCount = totalMovements
            }
        };
    }

    public async Task<List<StockMovement>> GetAllMovementsForExportAsync(int productId)
    {
        return await _db.StockMovements
            .AsNoTracking()
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .ToListAsync();
    }

    public async Task<string> GenerateNextSkuAsync()
    {
        var count = await _db.Products.CountAsync();
        var nextNumber = 1001 + count;
        var candidate = $"PRD-{nextNumber}";

        while (await _db.Products.AnyAsync(p => p.Sku == candidate))
        {
            nextNumber++;
            candidate = $"PRD-{nextNumber}";
        }

        return candidate;
    }


    public async Task<ServiceResult> CreateProductAsync(ProductFormDto dto)
    {
        var normalizedSku = dto.Sku.Trim().ToUpperInvariant();

        if (await _db.Products.AnyAsync(p => p.Sku == normalizedSku))
            return new ServiceResult(false, $"A product with SKU '{normalizedSku}' already exists.");

        var product = new Product
        {
            Sku = normalizedSku,
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            ReorderLevel = dto.ReorderLevel,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return new ServiceResult(true);
    }


    public async Task<ServiceResult> UpdateProductAsync(int id, ProductFormDto dto)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
            return new ServiceResult(false, "Product not found.");

        var normalizedSku = dto.Sku.Trim().ToUpperInvariant();

        if (await _db.Products.AnyAsync(p => p.Sku == normalizedSku && p.Id != id))
            return new ServiceResult(false, $"Another product already uses SKU '{normalizedSku}'.");

        product.Sku = normalizedSku;
        product.Name = dto.Name.Trim();
        product.Description = dto.Description?.Trim();
        product.ReorderLevel = dto.ReorderLevel;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return new ServiceResult(true);
    }


    public async Task<ServiceResult> ToggleActiveAsync(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
            return new ServiceResult(false, "Product not found.");

        product.IsActive = !product.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return new ServiceResult(true);
    }



    public async Task<ServiceResult> RecordMovementAsync(int productId, MovementType type, int quantity, string? note)
    {
        if (quantity <= 0)
            return new ServiceResult(false, "Quantity must be a positive integer greater than zero.");

        await using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null)
                return new ServiceResult(false, "Product not found.");

            if (!product.IsActive)
                return new ServiceResult(false, "Cannot record stock movements for an inactive product.");

            var currentStock = await _db.StockMovements
                .Where(m => m.ProductId == productId)
                .SumAsync(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity);

            if (type == MovementType.Out && quantity > currentStock)
            {
                return new ServiceResult(false,
                    $"Insufficient stock. You tried to issue {quantity} unit(s), but only {currentStock} unit(s) are currently available.");
            }

            var movement = new StockMovement
            {
                ProductId = productId,
                Type = type,
                Quantity = quantity,
                Note = note?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _db.StockMovements.Add(movement);
            _db.Entry(product).State = EntityState.Modified;

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Stock movement recorded: ProductId={ProductId}, SKU={Sku}, Type={Type}, Qty={Quantity}",
    product.Id, product.Sku, type, quantity);

            return new ServiceResult(true);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return new ServiceResult(false,
                "Stock was modified by another request. Please review the updated stock level and try again.");
        }
    }


}