using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;
using ShotsByMarques.Web.Services;
using ShotsByMarques.Web.ViewModels;

namespace ShotsByMarques.Web.Controllers;

public class BookingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly BookingService _bookingService;

    public BookingsController(ApplicationDbContext db, BookingService bookingService)
    {
        _db = db;
        _bookingService = bookingService;
    }

    [Route("sessoes")]
    public async Task<IActionResult> Index()
    {
        var sessionTypes = await _db.SessionTypes
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();
        return View(sessionTypes);
    }

    [Route("sessoes/marcar/{id:int}")]
    public async Task<IActionResult> Create(int id)
    {
        var sessionType = await _db.SessionTypes.FindAsync(id);
        if (sessionType is null || !sessionType.IsActive)
            return NotFound();

        return View(new BookingCreateViewModel { SessionTypeId = sessionType.Id, SessionType = sessionType });
    }

    [HttpPost]
    [Route("sessoes/marcar/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int id, BookingCreateViewModel model)
    {
        var sessionType = await _db.SessionTypes.FindAsync(id);
        if (sessionType is null || !sessionType.IsActive)
            return NotFound();

        model.SessionType = sessionType;

        if (!TimeSpan.TryParse(model.TimeSlot, out var time))
            ModelState.AddModelError(nameof(model.TimeSlot), "Escolhe um horário válido.");

        // Store the booking in UTC (slot chosen in local wall-clock time)
        var startLocal = model.Date.Date.Add(time);
        var start = BookingService.ToUtc(startLocal);

        if (ModelState.IsValid && !await _bookingService.IsSlotAvailableAsync(id, startLocal))
            ModelState.AddModelError(nameof(model.TimeSlot), "Esse horário já não está disponível. Escolhe outro.");

        if (!ModelState.IsValid)
            return View(model);

        if (!User.Identity!.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account",
                new { returnUrl = Url.Action("Create", "Bookings", new { id }) });
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        _db.Bookings.Add(new Booking
        {
            ClientUserId = userId,
            SessionTypeId = sessionType.Id,
            Start = start,
            Notes = model.Notes,
            Status = BookingStatus.Pending,
            Price = sessionType.Price
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = "Marcação pedida! O fotógrafo vai confirmar brevemente.";
        return RedirectToAction(nameof(Mine));
    }

    [HttpGet]
    public async Task<IActionResult> Available(int sessionTypeId, DateTime date)
    {
        var slots = await _bookingService.GetAvailableSlotsAsync(sessionTypeId, date);
        return Json(slots);
    }

    [Authorize]
    [Route("as-minhas-marcacoes")]
    public async Task<IActionResult> Mine()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var bookings = await _db.Bookings
            .Where(b => b.ClientUserId == userId)
            .Include(b => b.SessionType)
            .OrderByDescending(b => b.Start)
            .ToListAsync();

        // Private galleries = the client's session photos, delivered only to them.
        var galleries = await _db.Galleries
            .Include(g => g.Photos)
            .Include(g => g.Booking)
            .Where(g => g.IsPrivate && g.Booking != null && g.Booking.ClientUserId == userId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

        return View(new BookingMineViewModel { Bookings = bookings, Galleries = galleries });
    }
}
