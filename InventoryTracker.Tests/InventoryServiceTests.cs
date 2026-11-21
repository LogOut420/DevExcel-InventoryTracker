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
    public async Task NewProduct_WithNoMovements_HasZeroDerivedStock()
    {
        await _service.CreateProductAsync(new ProductFormDto
        {
            Sku = "PRD-1001",
            Name = "HP Laptop",
            ReorderLevel = 5
        });

        var product = await _db.Products.SingleAsync(p => p.Sku == "PRD-1001");
        var detail = await _service.GetProductDetailAsync(product.Id);

        Assert.NotNull(detail);
        Assert.Equal(0, detail!.CurrentStock);
    }

    [Fact]
    public async Task StockCalculation_ComputesSumOfInMinusSumOfOut()
    {
        await _service.CreateProductAsync(new ProductFormDto
        {
            Sku = "PRD-1002",
            Name = "Dell Monitor",
            ReorderLevel = 4
        });

        var product = await _db.Products.SingleAsync(p => p.Sku == "PRD-1002");

        await _service.RecordMovementAsync(product.Id, MovementType.In, 20, "Opening stock");
        await _service.RecordMovementAsync(product.Id, MovementType.Out, 7, "Issued to team");
        await _service.RecordMovementAsync(product.Id, MovementType.In, 5, "Restock");
        await _service.RecordMovementAsync(product.Id, MovementType.Out, 3, "Issued to QA");

        var detail = await _service.GetProductDetailAsync(product.Id);

        Assert.NotNull(detail);
        Assert.Equal(15, detail!.CurrentStock); // (20 + 5) - (7 + 3) = 15
    }

    [Fact]
    public async Task StockOut_EqualToAvailableStock_ReducesStockToZero()
    {
        await _service.CreateProductAsync(new ProductFormDto
        {
            Sku = "PRD-1003",
            Name = "Wireless Keyboard",
            ReorderLevel = 2
        });

        var product = await _db.Products.SingleAsync(p => p.Sku == "PRD-1003");
        await _service.RecordMovementAsync(product.Id, MovementType.In, 10, "Batch 1");

        var result = await _service.RecordMovementAsync(product.Id, MovementType.Out, 10, "Full dispatch");

        Assert.True(result.Success);

        var detail = await _service.GetProductDetailAsync(product.Id);
        Assert.Equal(0, detail!.CurrentStock);
    }

    [Fact]
    public async Task StockOut_ExceedingAvailableStock_IsBlockedAndNeverGoesBelowZero()
    {
        await _service.CreateProductAsync(new ProductFormDto
        {
            Sku = "PRD-1004",
            Name = "USB-C Dock",
            ReorderLevel = 3
        });

        var product = await _db.Products.SingleAsync(p => p.Sku == "PRD-1004");
        await _service.RecordMovementAsync(product.Id, MovementType.In, 8, "Opening");

        var result = await _service.RecordMovementAsync(product.Id, MovementType.Out, 9, "Overdraw attempt");

        Assert.False(result.Success);
        Assert.Contains("Insufficient stock", result.ErrorMessage);

        var detail = await _service.GetProductDetailAsync(product.Id);
        Assert.Equal(8, detail!.CurrentStock);
    }
}

