using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;
using ShotsByMarques.Web.ViewModels;

namespace ShotsByMarques.Web.Services;

/// <summary>
/// Single source of truth for the public booking calendar. Dates and wall-clock
/// hours are always interpreted in mainland Portugal; database values are UTC.
/// </summary>
public class BookingService
{
    private readonly ApplicationDbContext _db;

    public BookingService(ApplicationDbContext db) => _db = db;

    public async Task<List<string>> GetAvailableSlotsAsync(int sessionTypeId, DateTime date)
    {
        return (await GetSlotOptionsAsync(sessionTypeId, date))
            .Where(slot => !slot.Taken)
            .Select(slot => slot.Time)
            .ToList();
    }

    /// <summary>Builds one-hour session starts inside the exact window configured for the day.</summary>
    public async Task<List<SlotOption>> GetSlotOptionsAsync(int sessionTypeId, DateTime date)
    {
        var type = await _db.SessionTypes.FindAsync(sessionTypeId);
        if (type is null || type.IsMiniSessionType || date.Date < PortugalTime.Today)
            return new List<SlotOption>();

        var day = await _db.DayAvailability.FirstOrDefaultAsync(d => d.Date == date.Date);
        if (day?.Status != DayStatus.Available)
            return new List<SlotOption>();

        var startLocal = date.Date.Add(day.WindowStart ?? new TimeSpan(9, 0, 0));
        var endLocal = date.Date.Add(day.WindowEnd ?? new TimeSpan(20, 0, 0));
        if (startLocal >= endLocal)
            return new List<SlotOption>();

        var startUtc = ToUtc(startLocal);
        var endUtc = ToUtc(endLocal);
        var bookings = await ActiveBookingsNearAsync(startUtc, endUtc);
        var slots = new List<SlotOption>();

        for (var current = startLocal;
             current.AddMinutes(type.DurationMinutes) <= endLocal;
             current = current.AddHours(1))
        {
            var slotStartUtc = ToUtc(current);
            var slotEndUtc = ToUtc(current.AddMinutes(type.DurationMinutes));
            var taken = current <= PortugalTime.Now || bookings.Any(booking =>
                booking.Start < slotEndUtc && slotStartUtc < BookingEndUtc(booking));

            slots.Add(new SlotOption
            {
                Time = current.ToString("HH:mm"),
                Taken = taken
            });
        }

        return slots;
    }

    public async Task<bool> IsSlotAvailableAsync(int sessionTypeId, DateTime startLocal)
    {
        var type = await _db.SessionTypes.FindAsync(sessionTypeId);
        if (type is null || type.IsMiniSessionType)
            return false;

        var day = await _db.DayAvailability.FirstOrDefaultAsync(d => d.Date == startLocal.Date);
        if (day?.Status != DayStatus.Available)
            return false;

        var windowStart = day.WindowStart ?? new TimeSpan(9, 0, 0);
        var windowEnd = day.WindowEnd ?? new TimeSpan(20, 0, 0);
        var endLocal = startLocal.TimeOfDay.Add(TimeSpan.FromMinutes(type.DurationMinutes));
        if (startLocal.TimeOfDay < windowStart || endLocal > windowEnd)
            return false;

        var startUtc = ToUtc(startLocal);
        if (startUtc <= DateTime.UtcNow)
            return false;

        var endUtc = startUtc.AddMinutes(type.DurationMinutes);
        var bookings = await ActiveBookingsNearAsync(startUtc, endUtc);
        return bookings.All(booking => startUtc >= BookingEndUtc(booking) || endUtc <= booking.Start);
    }

    public async Task<List<string>> GetMiniSlotsAsync(DateTime date)
    {
        return (await GetMiniSlotOptionsAsync(date))
            .Where(slot => !slot.Taken)
            .Select(slot => slot.Time)
            .ToList();
    }

    /// <summary>
    /// Event mini slots are generated from the event's configured window. The
    /// internal slot cadence remains configurable through the mini type, while
    /// no duration is shown to the client.
    /// </summary>
    public async Task<List<SlotOption>> GetMiniSlotOptionsAsync(DateTime date)
    {
        var day = await _db.DayAvailability.FirstOrDefaultAsync(d => d.Date == date.Date);
        var miniType = await _db.SessionTypes.FirstOrDefaultAsync(s => s.IsMiniSessionType);
        if (day?.Status != DayStatus.Event || day.MiniStart is null || day.MiniEnd is null || miniType is null)
            return new List<SlotOption>();
        if (date.Date < PortugalTime.Today || day.MiniStart >= day.MiniEnd)
            return new List<SlotOption>();

        var windowStart = date.Date.Add(day.MiniStart.Value);
        var windowEnd = date.Date.Add(day.MiniEnd.Value);
        var startUtc = ToUtc(windowStart);
        var endUtc = ToUtc(windowEnd);
        var bookings = await ActiveBookingsNearAsync(startUtc, endUtc, miniOnly: true);
        var max = day.MaxMiniSessions ?? 4;
        var limitReached = bookings.Count >= max;
        var slots = new List<SlotOption>();

        for (var current = windowStart;
             current.AddMinutes(miniType.DurationMinutes) <= windowEnd;
             current = current.AddMinutes(miniType.DurationMinutes))
        {
            var slotStartUtc = ToUtc(current);
            var slotEndUtc = ToUtc(current.AddMinutes(miniType.DurationMinutes));
            var taken = limitReached
                || current <= PortugalTime.Now
                || bookings.Any(booking => booking.Start < slotEndUtc && slotStartUtc < BookingEndUtc(booking));
            slots.Add(new SlotOption { Time = current.ToString("HH:mm"), Taken = taken });
        }

        return slots;
    }

    public async Task<bool> IsMiniSlotAvailableAsync(DateTime date, TimeSpan time)
    {
        var day = await _db.DayAvailability.FirstOrDefaultAsync(d => d.Date == date.Date);
        var miniType = await _db.SessionTypes.FirstOrDefaultAsync(s => s.IsMiniSessionType);
        if (day?.Status != DayStatus.Event || day.MiniStart is null || day.MiniEnd is null || miniType is null)
            return false;

        var startLocal = date.Date.Add(time);
        var endLocal = startLocal.AddMinutes(miniType.DurationMinutes);
        if (startLocal.TimeOfDay < day.MiniStart || endLocal.TimeOfDay > day.MiniEnd)
            return false;

        var startUtc = ToUtc(startLocal);
        if (startUtc <= DateTime.UtcNow)
            return false;

        var bookings = await ActiveBookingsNearAsync(
            ToUtc(date.Date.Add(day.MiniStart.Value)),
            ToUtc(date.Date.Add(day.MiniEnd.Value)),
            miniOnly: true);
        if (bookings.Count >= (day.MaxMiniSessions ?? 4))
            return false;

        var endUtc = startUtc.AddMinutes(miniType.DurationMinutes);
        return bookings.All(booking => startUtc >= BookingEndUtc(booking) || endUtc <= booking.Start);
    }

    /// <summary>Converts a local wall-clock value to UTC for database comparisons/storage.</summary>
    public static DateTime ToUtc(DateTime local) => PortugalTime.ToUtc(local);

    private async Task<List<Booking>> ActiveBookingsNearAsync(DateTime rangeStartUtc, DateTime rangeEndUtc, bool miniOnly = false)
    {
        var query = _db.Bookings
            .AsNoTracking()
            .Include(booking => booking.SessionType)
            .Where(booking => booking.Status != BookingStatus.Cancelled
                              && booking.Start < rangeEndUtc
                              && booking.Start > rangeStartUtc.AddHours(-24));

        if (miniOnly)
            query = query.Where(booking => booking.SessionType!.IsMiniSessionType);

        return await query.ToListAsync();
    }

    private static DateTime BookingEndUtc(Booking booking)
    {
        var minutes = booking.SessionType?.DurationMinutes ?? 120;
        return booking.Start.AddMinutes(minutes);
    }
}
