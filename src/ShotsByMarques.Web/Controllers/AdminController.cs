using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;

    public AdminController(ApplicationDbContext db) => _db = db;

    [Route("admin")]
    public async Task<IActionResult> Dashboard()
    {
        ViewData["GalleryCount"] = await _db.Galleries.CountAsync();
        ViewData["PhotoCount"] = await _db.Photos.CountAsync();
        ViewData["BookingCount"] = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Pending);
        ViewData["OrderCount"] = await _db.Orders.CountAsync();
        ViewData["Revenue"] = await _db.Orders
            .Where(o => o.Status == OrderStatus.Paid)
            .SumAsync(o => (decimal?)o.Total) ?? 0m;

        ViewData["RecentBookings"] = await _db.Bookings
            .Include(b => b.ClientUser)
            .Include(b => b.SessionType)
            .OrderByDescending(b => b.CreatedAt)
            .Take(5)
            .ToListAsync();

        ViewData["RecentOrders"] = await _db.Orders
            .Include(o => o.ClientUser)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .ToListAsync();

        return View();
    }
}
