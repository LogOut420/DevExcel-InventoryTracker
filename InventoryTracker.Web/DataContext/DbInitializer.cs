using InventoryTracker.Web.DataContext;
using InventoryTracker.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryTracker.Web.DataContext;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext context)
    {
        await context.Database.MigrateAsync();


        // Seed Data
        if (await context.Products.AnyAsync())
            return;

        var now = DateTime.UtcNow;

        var laptop = new Product
        {
            Sku = "PRD-1001",
            Name = "HP EliteBook G8",
            Description = "14-inch Business Laptop, 16GB RAM, 512GB SSD",
            ReorderLevel = 5,
            IsActive = true,
            CreatedAt = now.AddDays(-10)
        };

        var monitor = new Product
        {
            Sku = "PRD-1002",
            Name = "Dell UltraSharp 27\" Monitor",
            Description = "4K USB-C Hub IPS Display",
            ReorderLevel = 4,
            IsActive = true,
            CreatedAt = now.AddDays(-8)
        };

        var keyboard = new Product
        {
            Sku = "PRD-1003",
            Name = "Logitech MX Keys Wireless",
            Description = "Backlit Mechanical Keyboard",
            ReorderLevel = 6,
            IsActive = true,
            CreatedAt = now.AddDays(-5)
        };

        var printer = new Product
        {
            Sku = "PRD-1004",
            Name = "HP LaserJet Pro M404dn",
            Description = "Legacy Office Printer (Discontinued)",
            ReorderLevel = 2,
            IsActive = false,
            CreatedAt = now.AddDays(-15)
        };

        context.Products.AddRange(laptop, monitor, keyboard, printer);
        await context.SaveChangesAsync();

        // Seed immutable stock movement ledger (Stock = SUM(In) - SUM(Out))
        var movements = new List<StockMovement>
        {
            // PRD-1001: 15 In - 4 Out = 11 (In Stock, above reorder level 5)
            new() { ProductId = laptop.Id, Type = MovementType.In, Quantity = 15, Note = "Initial vendor shipment", CreatedAt = now.AddDays(-9) },
            new() { ProductId = laptop.Id, Type = MovementType.Out, Quantity = 4, Note = "Issued to Engineering onboarding", CreatedAt = now.AddDays(-4) },

            // PRD-1002: 6 In - 3 Out = 3 (Low Stock, below reorder level 4)
            new() { ProductId = monitor.Id, Type = MovementType.In, Quantity = 6, Note = "Opening warehouse stock", CreatedAt = now.AddDays(-7) },
            new() { ProductId = monitor.Id, Type = MovementType.Out, Quantity = 3, Note = "Dispatched to QA Lab", CreatedAt = now.AddDays(-2) },

            // PRD-1003: 0 movements = 0 (Low Stock, below reorder level 6)

            // PRD-1004 (Inactive): 2 In = 2
            new() { ProductId = printer.Id, Type = MovementType.In, Quantity = 2, Note = "Archived stock count", CreatedAt = now.AddDays(-14) }
        };

        context.StockMovements.AddRange(movements);
        await context.SaveChangesAsync();
    }


}