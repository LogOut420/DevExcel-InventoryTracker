using InventoryTracker.Web.DataContext;
using Microsoft.EntityFrameworkCore;

namespace InventoryTracker.Web.DataContext;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext context)
    {
        await context.Database.MigrateAsync();
    }


}