using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminBookingsController : Controller
{
    private readonly ApplicationDbContext _db;

    public AdminBookingsController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(BookingStatus? status)
    {
        var query = _db.Bookings
            .Include(b => b.ClientUser)
            .Include(b => b.SessionType)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        var bookings = await query.OrderByDescending(b => b.Start).ToListAsync();
        ViewData["Filter"] = status;
        return View(bookings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int id, BookingStatus status)
    {
        var booking = await _db.Bookings.FindAsync(id);
        if (booking is not null)
        {
            booking.Status = status;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
