using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;

namespace ShotsByMarques.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminOrdersController : Controller
{
    private readonly ApplicationDbContext _db;

    public AdminOrdersController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var orders = await _db.Orders
            .Include(o => o.ClientUser)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
        return View(orders);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order is not null)
        {
            _db.OrderItems.RemoveRange(order.Items);
            _db.Orders.Remove(order);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Encomenda eliminada.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkDelete(string ids)
    {
        var idList = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (idList.Count == 0)
            return RedirectToAction(nameof(Index));

        var orders = await _db.Orders
            .Include(o => o.Items)
            .Where(o => idList.Contains(o.Id))
            .ToListAsync();

        foreach (var order in orders)
        {
            _db.OrderItems.RemoveRange(order.Items);
            _db.Orders.Remove(order);
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{orders.Count} encomenda(s) eliminada(s).";
        return RedirectToAction(nameof(Index));
    }
}
