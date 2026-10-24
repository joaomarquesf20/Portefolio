using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;
using ShotsByMarques.Web.ViewModels;

namespace ShotsByMarques.Web.Services;

/// <summary>
/// Builds the monthly calendar grid (Monday-first, with adjacent-month cells)
/// with each day's per-date availability status. Shared by the admin calendar
/// and the client booking form so the logic cannot drift apart.
/// </summary>
public class AvailabilityCalendarService
{
    public async Task<List<CalendarDay>> BuildMonthDaysAsync(ApplicationDbContext db, int year, int month, bool includeMiniCounts = false)
    {
        var today = PortugalTime.Today;
        var first = new DateTime(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var offset = ((int)first.DayOfWeek + 6) % 7; // Monday-first grid

        var overrides = await db.DayAvailability
            .Where(d => d.Date.Year == year && d.Date.Month == month)
            .ToListAsync();
        var byDate = overrides.ToDictionary(o => o.Date);

        // Booking counts per day (only when requested, e.g. admin calendar):
        // mini-session bookings on event days, and all bookings on normal days.
        var miniBooked = new Dictionary<DateTime, int>();
        var booked = new Dictionary<DateTime, int>();
        if (includeMiniCounts)
        {
            // SQL stores UTC. Convert to Portugal time before grouping, otherwise
            // late-night bookings can appear on the wrong calendar day.
            var monthStartUtc = PortugalTime.ToUtc(first.AddDays(-1));
            var monthEndUtc = PortugalTime.ToUtc(first.AddMonths(1).AddDays(1));
            var monthBookings = await db.Bookings
                .Include(b => b.SessionType)
                .Where(b => b.Status != BookingStatus.Cancelled
                            && b.Start >= monthStartUtc
                            && b.Start < monthEndUtc)
                .Select(b => new { b.Start, IsMini = b.SessionType!.IsMiniSessionType })
                .ToListAsync();

            miniBooked = monthBookings
                .Where(b => b.IsMini)
                .GroupBy(b => PortugalTime.FromUtc(b.Start).Date)
                .ToDictionary(g => g.Key, g => g.Count());
            booked = monthBookings
                .GroupBy(b => PortugalTime.FromUtc(b.Start).Date)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        var days = new List<CalendarDay>();

        // Leading cells from the previous month (not interactive).
        var prevMonth = first.AddMonths(-1);
        var prevDays = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);
        for (int i = offset - 1; i >= 0; i--)
        {
            days.Add(new CalendarDay
            {
                Date = new DateTime(prevMonth.Year, prevMonth.Month, prevDays - i),
                InMonth = false
            });
        }

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(year, month, day);
            byDate.TryGetValue(date, out var ov);
            days.Add(new CalendarDay
            {
                Date = date,
                InMonth = true,
                IsPast = date.Date < today,
                Status = ov?.Status,
                MaxMiniSessions = ov?.MaxMiniSessions,
                EventTitle = ov?.Title,
                Zone = ov?.Zone,
                Location = ov?.Location,
                MiniStart = ov?.MiniStart,
                MiniEnd = ov?.MiniEnd,
                WindowStart = ov?.WindowStart,
                WindowEnd = ov?.WindowEnd,
                MiniBooked = ov is not null && ov.Status == DayStatus.Event ? miniBooked.GetValueOrDefault(date) : 0,
                BookedCount = booked.GetValueOrDefault(date)
            });
        }

        // Trailing cells from the next month to complete the grid.
        var nextMonth = first.AddMonths(1);
        var trailing = days.Count % 7 == 0 ? 0 : 7 - (days.Count % 7);
        for (int i = 1; i <= trailing; i++)
        {
            days.Add(new CalendarDay
            {
                Date = new DateTime(nextMonth.Year, nextMonth.Month, i),
                InMonth = false
            });
        }
        return days;
    }
}
