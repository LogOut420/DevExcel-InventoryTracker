# Inventory Tracker (.NET 10 MVC)

A lightweight, production-structured Inventory Tracker built with **ASP.NET Core 10 (MVC)**, **Entity Framework Core**, **SQL Server**, **Bootstrap 5**, and **xUnit**.

## Core Architecture & Design Decisions

- **Immutable Movement Ledger (`Derived Stock`)**: `Product` does not store a mutable `Quantity` column. Current stock is always derived strictly as `SUM(In) - SUM(Out)` from `StockMovements`, ensuring full auditability and eliminating stock drift.
- **Negative Stock & Race Condition Protection**:
  - `InventoryService.RecordMovementAsync` runs inside an explicit database transaction (`BeginTransactionAsync`), validates `quantity <= currentStock` before persisting any `Out` movement, and marks the parent `Product` entity as modified so SQL Server advances its `[Timestamp] RowVersion` concurrency token.
  - Overlapping concurrent requests catch `DbUpdateConcurrencyException`, roll back cleanly, and display a conflict warning to the user.
- **Auto-Generated SKUs with Uniqueness Enforcement**: Suggests the next sequential SKU (`PRD-1001`, `PRD-1002`, ...) on product creation while enforcing case-insensitive uniqueness both in service logic and via a SQL Server `UNIQUE` index.

## Stretch Goals Implemented

1. **Server-Side Pagination**: 5-item pagination on the Product List and 10-item pagination on the Stock Movement Ledger.
2. **CSV Movement Export**: One-click export (`GET /Products/ExportMovementsCsv/{id}`) of a product's complete chronological movement ledger.
3. **Global Inventory Dashboard**: Summary cards showing Total Active SKUs, Active Units on Hand, and Count Below Reorder Level across the entire catalog.
4. **Optimistic Concurrency (`RowVersion`)**: Database-level concurrency control with clean conflict feedback.
5. **Audit Fields & Structured Logging**: `CreatedAt` and `UpdatedAt` UTC timestamps alongside `ILogger` structured telemetry.
6. **JSON Stock API Endpoint**: `GET /api/products/{id}/stock` returning live derived stock and reorder status as JSON.

## How to Run Locally

### Prerequisites
- .NET 10 SDK
- SQL Server (LocalDB or SQL Server Express/Developer)

### Setup Steps
1. Update the `DefaultConnection` string in `InventoryTracker.Web/appsettings.json` to match your SQL Server instance.
2. Run the web application (database and EF Core migrations apply automatically on startup via `DbInitializer.InitializeAsync`):
   ```bash
   dotnet run --project InventoryTracker.Web