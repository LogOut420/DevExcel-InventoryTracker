using InventoryTracker.Web.DataContext;
using InventoryTracker.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryTracker.Web.Services;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _db;

    public InventoryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ProductSummaryDto>> GetProductsAsync(string? searchTerm, bool includeInactive = true)
    {
        var query = _db.Products.AsNoTracking().AsQueryable();

        if (!includeInactive)
            query = query.Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(p => p.Sku.Contains(term) || p.Name.Contains(term));
        }

        return await query
            .OrderBy(p => p.Sku)
            .Select(p => new ProductSummaryDto
            {
                Id = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                ReorderLevel = p.ReorderLevel,
                IsActive = p.IsActive,
                CurrentStock = p.Movements.Sum(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity)
            })
            .ToListAsync();
    }


    public async Task<ProductDetailDto?> GetProductDetailAsync(int id)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Movements)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null) return null;

        var movements = product.Movements
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .ToList();

        var currentStock = movements.Sum(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity);

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
            Movements = movements
        };
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

        await _db.SaveChangesAsync();
        return new ServiceResult(true);
    }


    public async Task<ServiceResult> ToggleActiveAsync(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
            return new ServiceResult(false, "Product not found.");

        product.IsActive = !product.IsActive;
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

            // Product.RowVersion so concurrent requests detect conflicts
            _db.Entry(product).State = EntityState.Modified;

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return new ServiceResult(true);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return new ServiceResult(false,
                "Stock was modified by another concurrent request. Please review the updated stock level and try again.");
        }
    }


}