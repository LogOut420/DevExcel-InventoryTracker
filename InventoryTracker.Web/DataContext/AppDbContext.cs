using InventoryTracker.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryTracker.Web.DataContext;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(p => p.Sku).IsUnique();
            entity.Property(p => p.Sku).HasMaxLength(50).IsRequired();
            entity.Property(p => p.Name).HasMaxLength(150).IsRequired();

            if (Database.IsSqlServer())
            {
                entity.Property(p => p.RowVersion).IsRowVersion();
            }
            else
            {
                entity.Ignore(p => p.RowVersion);
            }
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasOne(m => m.Product)
                  .WithMany(p => p.Movements)
                  .HasForeignKey(m => m.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}