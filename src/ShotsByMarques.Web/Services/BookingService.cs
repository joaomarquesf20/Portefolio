using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;
using ShotsByMarques.Web.ViewModels;

namespace ShotsByMarques.Web.Services;

public class BookingService
{
    private readonly ApplicationDbContext _db;

    public BookingService(ApplicationDbContext db) => _db = db;

    /// <summary>
    /// Hourly slots (local wall clock) inside the admin-defined availability
    /// windows for that weekday, minus already-booked intervals. If the admin
    /// defined no window for the day, no slots are returned.
    /// </summary>
    public async Task<List<string>> GetAvailableSlotsAsync(int sessionTypeId, DateTime date)
    {
        var options = await GetSlotOptionsAsync(sessionTypeId, date);
        return options.Where(o => !o.Taken).Select(o => o.Time).ToList();
    }

    /// <summary>
    /// Full slot grid for the day: every in-window hour with a Taken flag
    /// (overlapping booking or already past) so the client sees the whole day
    /// and understands why some hours are unavailable.
    /// </summary>
    public async Task<List<SlotOption>> GetSlotOptionsAsync(int sessionTypeId, DateTime date)
    {
        var sessionType = await _db.SessionTypes.FindAsync(sessionTypeId);
        if (sessionType is null || sessionType.IsMiniSessionType || date.Date < DateTime.Today)
            return new List<SlotOption>();

        // Only explicitly marked days are bookable: the admin must mark a day as
        // Available/Talvez (with hours). Unmarked, unavailable and event days are
        // closed for regular sessions.
        var dayOverride = await _db.DayAvailability.FirstOrDefaultAsync(d => d.Date == date.Date);
        if (dayOverride is null ||
            dayOverride.Status == DayStatus.Unavailable ||
            dayOverride.Status == DayStatus.Event)
            return new List<SlotOption>();

        // The day's own hours; the standard 09:00–20:00 applies when the admin
        // marked the day but left the hour bar empty.
        var dayStart = date.Date.Add(dayOverride.WindowStart ?? new TimeSpan(9, 0, 0));
        var dayEnd = date.Date.Add(dayOverride.WindowEnd ?? new TimeSpan(20, 0, 0));

        // Bookings taken on this calendar day (the window end is exclusive: the
        // last session must END by the end time, so overlap is checked against
        // every booking of the day).
        var takenUtc = await _db.Bookings
            .Where(b => b.SessionTypeId == sessionTypeId
                        && b.Status != BookingStatus.Cancelled
                        && b.Start >= ToUtc(dayStart) && b.Start < ToUtc(dayStart).AddDays(1))
            .Select(b => b.Start)
            .ToListAsync();

        var slots = new List<SlotOption>();
        var current = dayStart;
        // Sessions must fit inside the window: the last slot starts exactly
        // DurationMinutes before the end (e.g. a 09:00–20:00 window with 2h
        // sessions offers slots 09:00 … 18:00, the last one ending at 20:00).
        while (current.AddMinutes(sessionType.DurationMinutes) <= dayEnd)
        {
            var isPast = date.Date == DateTime.Today && current <= DateTime.Now;
            var slotStartUtc = ToUtc(current);
            var slotEndUtc = ToUtc(current.AddMinutes(sessionType.DurationMinutes));
            var overlaps = takenUtc.Any(t =>
                t < slotEndUtc && slotStartUtc < t.AddMinutes(sessionType.DurationMinutes));
            if (isPast || overlaps)
                slots.Add(new SlotOption { Time = current.ToString("HH:mm"), Taken = true });
            else
                slots.Add(new SlotOption { Time = current.ToString("HH:mm"), Taken = false });
            current = current.AddHours(1);
        }
        return slots.DistinctBy(s => s.Time).OrderBy(s => s.Time).ToList();
    }

    public async Task<bool> IsSlotAvailableAsync(int sessionTypeId, DateTime startLocal)
    {
        var sessionType = await _db.SessionTypes.FindAsync(sessionTypeId);
        if (sessionType is null || sessionType.IsMiniSessionType)
            return false;

        // Only explicitly marked days are bookable: unmarked, unavailable and
        // event days reject regular session bookings.
        var dayOverride = await _db.DayAvailability.FirstOrDefaultAsync(d => d.Date == startLocal.Date);
        if (dayOverride is null ||
            dayOverride.Status == DayStatus.Unavailable ||
            dayOverride.Status == DayStatus.Event)
            return false;

        // The chosen time must be inside the day's window (or the 09:00–20:00
        // standard when the admin left it empty), and the session must fit
        // before the end (e.g. a 2h session at 18:00 is the last one allowed
        // on a 09:00–20:00 window).
        var sessionEnd = startLocal.TimeOfDay.Add(TimeSpan.FromMinutes(sessionType.DurationMinutes));
        var winStart = dayOverride.WindowStart ?? new TimeSpan(9, 0, 0);
        var winEnd = dayOverride.WindowEnd ?? new TimeSpan(20, 0, 0);
        if (winStart > startLocal.TimeOfDay || sessionEnd > winEnd)
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

    /// <summary>
    /// Hourly mini-session slots (local wall clock) inside the admin's time window
    /// for an event day, minus already-taken mini bookings. Empty when the admin
    /// has not scheduled the event's hours yet.
    /// </summary>
    public async Task<List<string>> GetMiniSlotsAsync(DateTime date)
    {
        var options = await GetMiniSlotOptionsAsync(date);
        return options.Where(o => !o.Taken).Select(o => o.Time).ToList();
    }

    /// <summary>
    /// Full mini-session slot grid for the event day with taken flags (booked or
    /// past), so the client sees the whole grid instead of missing hours.
    /// </summary>
    public async Task<List<SlotOption>> GetMiniSlotOptionsAsync(DateTime date)
    {
        var day = await _db.DayAvailability.FirstOrDefaultAsync(d => d.Date == date.Date);
        if (day is null || day.Status != DayStatus.Event || day.MiniStart is null || day.MiniEnd is null)
            return new List<SlotOption>();
        if (date.Date < DateTime.Today)
            return new List<SlotOption>();

        var miniType = await _db.SessionTypes.FirstOrDefaultAsync(s => s.IsMiniSessionType);
        if (miniType is null)
            return new List<SlotOption>();

        var takenUtc = await _db.Bookings
            .Include(b => b.SessionType)
            .Where(b => b.SessionType!.IsMiniSessionType
                        && b.Status != BookingStatus.Cancelled
                        && b.Start.Date == date.Date)
            .Select(b => b.Start)
            .ToListAsync();

        var duration = TimeSpan.FromMinutes(miniType.DurationMinutes);
        var slots = new List<SlotOption>();
        var current = date.Date.Add(day.MiniStart.Value);
        var windowEnd = date.Date.Add(day.MiniEnd.Value);
        // Slots every 30 minutes, and each mini session must fit inside the
        // window: the last one ends exactly at the end time (e.g. 09:00–20:00 →
        // slots 09:00, 09:30, … 19:30).
        while (current.Add(duration) <= windowEnd)
        {
            var isPast = date.Date == DateTime.Today && current <= DateTime.Now;
            var startUtc = ToUtc(current);
            var endUtc = ToUtc(current.Add(duration));
            var overlaps = takenUtc.Any(t => t < endUtc && startUtc < t.Add(duration));
            slots.Add(new SlotOption
            {
                Time = current.ToString("HH:mm"),
                Taken = isPast || overlaps
            });
            current = current.AddMinutes(miniType.DurationMinutes);
        }
        return slots;
    }

    /// <summary>
    /// True when the mini slot is inside the event's admin-defined window and does
    /// not overlap another mini booking.
    /// </summary>
    public async Task<bool> IsMiniSlotAvailableAsync(DateTime date, TimeSpan time)
    {
        var day = await _db.DayAvailability.FirstOrDefaultAsync(d => d.Date == date.Date);
        if (day is null || day.Status != DayStatus.Event || day.MiniStart is null || day.MiniEnd is null)
            return false;

        var miniType = await _db.SessionTypes.FirstOrDefaultAsync(s => s.IsMiniSessionType);
        if (miniType is null)
            return false;

        var startLocal = date.Date.Add(time);
        var duration = TimeSpan.FromMinutes(miniType.DurationMinutes);
        // The mini session must fit before the end of the window (e.g. 09:00–20:00
        // → the last allowed start is 19:30).
        var sessionEnd = startLocal.TimeOfDay.Add(duration);
        if (startLocal.TimeOfDay < day.MiniStart.Value || sessionEnd > day.MiniEnd.Value)
            return false;

        var startUtc = ToUtc(startLocal);
        if (startUtc < DateTime.UtcNow)
            return false;

        var endUtc = startUtc.Add(duration);
        // AddMinutes maps to SQL (unlike Add(TimeSpan), which cannot be translated).
        return !await _db.Bookings
            .Include(b => b.SessionType)
            .AnyAsync(b => b.SessionType!.IsMiniSessionType
                           && b.Status != BookingStatus.Cancelled
                           && b.Start < endUtc && startUtc < b.Start.AddMinutes(miniType.DurationMinutes));
    }

    /// <summary>Treats a local wall-clock DateTime as Local and converts to UTC for storage.</summary>
    public static DateTime ToUtc(DateTime local) =>
        DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
}
