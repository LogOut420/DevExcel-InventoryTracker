using InventoryTracker.Web.DataContext;
using InventoryTracker.Web.Models;
using InventoryTracker.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InventoryTracker.Tests;

public class InventoryServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly InventoryService _service;

    public InventoryServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _service = new InventoryService(_db, NullLogger<InventoryService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateSku_ReturnsFriendlyError()
    {
        var first = await _service.CreateProductAsync(new ProductFormDto
        {
            Sku = "PRD-1001",
            Name = "HP Laptop",
            ReorderLevel = 5
        });
        Assert.True(first.Success);

        var duplicate = await _service.CreateProductAsync(new ProductFormDto
        {
            Sku = "prd-1001",
            Name = "Duplicate Laptop",
            ReorderLevel = 3
        });

        Assert.False(duplicate.Success);
        Assert.Contains("already exists", duplicate.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateNextSku_IncrementsSequentially_AndSkipsExisting()
    {
        var sku1 = await _service.GenerateNextSkuAsync();
        Assert.Equal("PRD-1001", sku1);

        await _service.CreateProductAsync(new ProductFormDto { Sku = sku1, Name = "Item 1", ReorderLevel = 2 });

        var sku2 = await _service.GenerateNextSkuAsync();
        Assert.Equal("PRD-1002", sku2);
    }

    [Fact]
    public async Task RecordMovement_CalculatesDerivedStock_FromInAndOutLedger()
    {
        await _service.CreateProductAsync(new ProductFormDto { Sku = "ELEC-10", Name = "Monitor", ReorderLevel = 4 });
        var product = await _db.Products.SingleAsync(p => p.Sku == "ELEC-10");

        var r1 = await _service.RecordMovementAsync(product.Id, MovementType.In, 15, "Initial stock");
        var r2 = await _service.RecordMovementAsync(product.Id, MovementType.Out, 6, "Issued to QA");
        var r3 = await _service.RecordMovementAsync(product.Id, MovementType.In, 3, "Restock");

        Assert.True(r1.Success && r2.Success && r3.Success);

        var detail = await _service.GetProductDetailAsync(product.Id);
        Assert.NotNull(detail);
        Assert.Equal(12, detail!.CurrentStock);
        Assert.Equal(3, detail.PagedMovements.TotalCount);
    }

    [Fact]
    public async Task RecordMovement_OutExceedingAvailableStock_IsBlocked()
    {
        await _service.CreateProductAsync(new ProductFormDto { Sku = "ELEC-20", Name = "Keyboard", ReorderLevel = 5 });
        var product = await _db.Products.SingleAsync(p => p.Sku == "ELEC-20");

        await _service.RecordMovementAsync(product.Id, MovementType.In, 10, "Opening");

        var overdraw = await _service.RecordMovementAsync(product.Id, MovementType.Out, 11, "Too many");

        Assert.False(overdraw.Success);
        Assert.Contains("Insufficient stock", overdraw.ErrorMessage);

        var detail = await _service.GetProductDetailAsync(product.Id);
        Assert.Equal(10, detail!.CurrentStock);
    }

    [Fact]
    public async Task RecordMovement_ZeroOrNegativeQuantity_IsRejected()
    {
        await _service.CreateProductAsync(new ProductFormDto { Sku = "ELEC-30", Name = "Mouse", ReorderLevel = 5 });
        var product = await _db.Products.SingleAsync(p => p.Sku == "ELEC-30");

        var zeroResult = await _service.RecordMovementAsync(product.Id, MovementType.In, 0, "Invalid");
        var negativeResult = await _service.RecordMovementAsync(product.Id, MovementType.Out, -5, "Invalid");

        Assert.False(zeroResult.Success);
        Assert.False(negativeResult.Success);
    }

    [Fact]
    public async Task RecordMovement_OnInactiveProduct_IsBlocked()
    {
        await _service.CreateProductAsync(new ProductFormDto { Sku = "ELEC-40", Name = "Old Printer", ReorderLevel = 1 });
        var product = await _db.Products.SingleAsync(p => p.Sku == "ELEC-40");

        await _service.ToggleActiveAsync(product.Id);

        var result = await _service.RecordMovementAsync(product.Id, MovementType.In, 5, "Attempt on inactive");

        Assert.False(result.Success);
        Assert.Contains("inactive", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetProducts_SupportsPaginationSearchAndLowStockFilter()
    {
        for (int i = 1; i <= 7; i++)
        {
            await _service.CreateProductAsync(new ProductFormDto
            {
                Sku = $"SKU-{i:000}",
                Name = $"Product {i}",
                ReorderLevel = 5
            });
        }

        var first = await _db.Products.SingleAsync(p => p.Sku == "SKU-001");
        await _service.RecordMovementAsync(first.Id, MovementType.In, 10, "Stocked");

        var page1 = await _service.GetProductsAsync(searchTerm: null, includeInactive: true, lowStockOnly: false, page: 1, pageSize: 5);
        Assert.Equal(7, page1.TotalCount);
        Assert.Equal(5, page1.Items.Count);
        Assert.Equal(2, page1.TotalPages);

        var lowStockOnly = await _service.GetProductsAsync(searchTerm: null, includeInactive: true, lowStockOnly: true, page: 1, pageSize: 10);
        Assert.Equal(6, lowStockOnly.TotalCount);
    }

    [Fact]
    public async Task GetDashboardStats_ReturnsGlobalActiveTotals()
    {
        await _service.CreateProductAsync(new ProductFormDto { Sku = "A-1", Name = "Active Healthy", ReorderLevel = 2 });
        await _service.CreateProductAsync(new ProductFormDto { Sku = "A-2", Name = "Active Low", ReorderLevel = 5 });
        await _service.CreateProductAsync(new ProductFormDto { Sku = "I-1", Name = "Inactive Item", ReorderLevel = 5 });

        var p1 = await _db.Products.SingleAsync(p => p.Sku == "A-1");
        var p2 = await _db.Products.SingleAsync(p => p.Sku == "A-2");
        var p3 = await _db.Products.SingleAsync(p => p.Sku == "I-1");

        await _service.RecordMovementAsync(p1.Id, MovementType.In, 10, "In");
        await _service.RecordMovementAsync(p2.Id, MovementType.In, 3, "In");
        await _service.RecordMovementAsync(p3.Id, MovementType.In, 50, "In");
        await _service.ToggleActiveAsync(p3.Id);

        var stats = await _service.GetDashboardStatsAsync();

        Assert.Equal(2, stats.TotalActiveSkus);
        Assert.Equal(13, stats.TotalUnitsOnHand);
        Assert.Equal(1, stats.BelowReorderCount);
    }
}