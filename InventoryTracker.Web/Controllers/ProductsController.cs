using System.Text;
using InventoryTracker.Web.Models;
using InventoryTracker.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace InventoryTracker.Web.Controllers;

public class ProductsController : Controller
{
    private readonly IInventoryService _inventoryService;

    public ProductsController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    // Screen 1: Product List + Search + Filter
    [HttpGet]
    public async Task<IActionResult> Index(string? search, bool showInactive = true)
    {
        var products = await _inventoryService.GetProductsAsync(search, showInactive);
        ViewBag.Search = search;
        ViewBag.ShowInactive = showInactive;
        return View(products);
    }

    // Screen 2: Product Detail + Stock Movements History
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var detail = await _inventoryService.GetProductDetailAsync(id);
        if (detail == null) return NotFound();
        return View(detail);
    }

    // Record Stock Movement (In / Out)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordMovement(int productId, MovementType type, int quantity, string? note)
    {
        var result = await _inventoryService.RecordMovementAsync(productId, type, quantity, note);

        if (!result.Success)
            TempData["ErrorMessage"] = result.ErrorMessage;
        else
            TempData["SuccessMessage"] = $"Stock movement ({type}: {quantity}) recorded successfully.";

        return RedirectToAction(nameof(Details), new { id = productId });
    }

    // Stretch Goal: Export Movement History as CSV
    [HttpGet]
    public async Task<IActionResult> ExportMovementsCsv(int id)
    {
        var detail = await _inventoryService.GetProductDetailAsync(id);
        if (detail == null) return NotFound();

        var sb = new StringBuilder();
        sb.AppendLine("MovementId,SKU,ProductName,Type,Quantity,Note,TimestampUtc");

        foreach (var m in detail.Movements)
        {
            var safeName = detail.Name.Replace("\"", "\"\"");
            var safeNote = (m.Note ?? string.Empty).Replace("\"", "\"\"");
            sb.AppendLine($"{m.Id},\"{detail.Sku}\",\"{safeName}\",{m.Type},{m.Quantity},\"{safeNote}\",{m.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"{detail.Sku}_stock_movements.csv");
    }

    // Screen 3a: Create Product (Pre-filled Auto-SKU, editable for duplicate SKU validation)
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var nextSku = await _inventoryService.GenerateNextSkuAsync();
        return View(new ProductFormDto { Sku = nextSku, ReorderLevel = 5 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormDto dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var result = await _inventoryService.CreateProductAsync(dto);
        if (!result.Success)
        {
            ModelState.AddModelError(nameof(dto.Sku), result.ErrorMessage!);
            return View(dto);
        }

        TempData["SuccessMessage"] = $"Product '{dto.Name}' ({dto.Sku.ToUpperInvariant()}) created.";
        return RedirectToAction(nameof(Index));
    }

    // Screen 3b: Edit Product
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var detail = await _inventoryService.GetProductDetailAsync(id);
        if (detail == null) return NotFound();

        ViewBag.ProductId = id;
        return View(new ProductFormDto
        {
            Sku = detail.Sku,
            Name = detail.Name,
            Description = detail.Description,
            ReorderLevel = detail.ReorderLevel
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductFormDto dto)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ProductId = id;
            return View(dto);
        }

        var result = await _inventoryService.UpdateProductAsync(id, dto);
        if (!result.Success)
        {
            ModelState.AddModelError(nameof(dto.Sku), result.ErrorMessage!);
            ViewBag.ProductId = id;
            return View(dto);
        }

        TempData["SuccessMessage"] = "Product details updated.";
        return RedirectToAction(nameof(Index));
    }

    // Deactivate / Activate Product
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var result = await _inventoryService.ToggleActiveAsync(id);
        if (!result.Success)
            TempData["ErrorMessage"] = result.ErrorMessage;
        else
            TempData["SuccessMessage"] = "Product status updated.";

        return RedirectToAction(nameof(Index));
    }


}