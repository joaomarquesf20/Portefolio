using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.Services;

public class BookingService
{
    private readonly ApplicationDbContext _db;

    public BookingService(ApplicationDbContext db) => _db = db;

    /// <summary>
    /// Hourly slots between 09:00 and 19:00 (local wall clock),
    /// minus already-booked intervals. Bookings are stored as UTC.
    /// </summary>
    public async Task<List<string>> GetAvailableSlotsAsync(int sessionTypeId, DateTime date)
    {
        var sessionType = await _db.SessionTypes.FindAsync(sessionTypeId);
        if (sessionType is null || date.Date < DateTime.Today)
            return new List<string>();

        var dayStart = date.Date.AddHours(9);
        var dayEnd = date.Date.AddHours(19);

        var takenUtc = await _db.Bookings
            .Where(b => b.SessionTypeId == sessionTypeId
                        && b.Status != BookingStatus.Cancelled
                        && b.Start >= ToUtc(dayStart) && b.Start < ToUtc(dayEnd))
            .Select(b => b.Start)
            .ToListAsync();

        var slots = new List<string>();
        var current = dayStart;
        while (current.AddMinutes(sessionType.DurationMinutes) <= dayEnd)
        {
            if (date.Date == DateTime.Today && current <= DateTime.Now)
            {
                // skip slots already past today
            }
            else
            {
                var slotStartUtc = ToUtc(current);
                var slotEndUtc = ToUtc(current.AddMinutes(sessionType.DurationMinutes));
                var overlaps = takenUtc.Any(t =>
                    t < slotEndUtc && slotStartUtc < t.AddMinutes(sessionType.DurationMinutes));
                if (!overlaps)
                    slots.Add(current.ToString("HH:mm"));
            }
            current = current.AddHours(1);
        }
        return slots;
    }

    public async Task<bool> IsSlotAvailableAsync(int sessionTypeId, DateTime startLocal)
    {
        var sessionType = await _db.SessionTypes.FindAsync(sessionTypeId);
        if (sessionType is null)
            return false;

        var startUtc = ToUtc(startLocal);
        if (startUtc < DateTime.UtcNow)
            return false;

        var endUtc = startUtc.AddMinutes(sessionType.DurationMinutes);
        return !await _db.Bookings.AnyAsync(b =>
            b.SessionTypeId == sessionTypeId &&
            b.Status != BookingStatus.Cancelled &&
            b.Start < endUtc && startUtc < b.Start.AddMinutes(sessionType.DurationMinutes));
    }

    /// <summary>Treats a local wall-clock DateTime as Local and converts to UTC for storage.</summary>
    public static DateTime ToUtc(DateTime local) =>
        DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
}
