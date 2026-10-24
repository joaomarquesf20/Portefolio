using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;
using ShotsByMarques.Web.Services;

namespace ShotsByMarques.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminBookingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PhotoStorageService _storage;
    private readonly BookingCalendarService _bookingCalendar;

    public AdminBookingsController(ApplicationDbContext db, PhotoStorageService storage, BookingCalendarService bookingCalendar)
    {
        _db = db;
        _storage = storage;
        _bookingCalendar = bookingCalendar;
    }

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

    [HttpGet]
    public async Task<IActionResult> AddToGoogleCalendar(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.SessionType)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null)
            return NotFound();
        return Redirect(_bookingCalendar.CreateGoogleCalendarUrl(booking));
    }

    [HttpGet]
    public async Task<IActionResult> DownloadCalendarFile(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.SessionType)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null)
            return NotFound();
        return File(_bookingCalendar.CreateIcs(booking), "text/calendar; charset=utf-8", _bookingCalendar.CreateFileName(booking));
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

    /// <summary>Marks a payment as received (e.g. cash at delivery, or deposit paid in person).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPayment(int id, string what)
    {
        var booking = await _db.Bookings.FindAsync(id);
        if (booking is not null)
        {
            if (what == "balance") booking.BalancePaid = true;
            else if (what == "deposit") booking.DepositPaid = true;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Deletes the booking. If a private gallery was created to deliver
    /// this session's photos, it is deleted along with its photos.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.Gallery).ThenInclude(g => g!.Photos)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (booking is not null)
        {
            if (booking.Gallery is not null)
            {
                foreach (var photo in booking.Gallery.Photos)
                    _storage.Delete(photo.FilePath, photo.PreviewPath);
                _db.Galleries.Remove(booking.Gallery);
            }
            _db.Bookings.Remove(booking);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Marcação eliminada.";
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

        var bookings = await _db.Bookings
            .Include(b => b.Gallery).ThenInclude(g => g!.Photos)
            .Where(b => idList.Contains(b.Id))
            .ToListAsync();

        foreach (var booking in bookings)
        {
            if (booking.Gallery is not null)
            {
                foreach (var photo in booking.Gallery.Photos)
                    _storage.Delete(photo.FilePath, photo.PreviewPath);
                _db.Galleries.Remove(booking.Gallery);
            }
            _db.Bookings.Remove(booking);
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{bookings.Count} marcação(ões) eliminada(s).";
        return RedirectToAction(nameof(Index));
    }
}
