using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;
using ShotsByMarques.Web.Services;
using ShotsByMarques.Web.ViewModels;

namespace ShotsByMarques.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminAvailabilityController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AvailabilityCalendarService _calendar;

    public AdminAvailabilityController(ApplicationDbContext db, AvailabilityCalendarService calendar)
    {
        _db = db;
        _calendar = calendar;
    }

    // ---- Calendar of per-day availability ----

    public async Task<IActionResult> Index(int? year, int? month)
    {
        var today = DateTime.Today;
        var y = Math.Clamp(year ?? today.Year, 2020, 2100);
        var m = Math.Clamp(month ?? today.Month, 1, 12);

        var days = await _calendar.BuildMonthDaysAsync(_db, y, m, includeMiniCounts: true);

        var regions = ShotsByMarques.Web.Services.PortugalCities.All
            .Select(c => c.Region)
            .Distinct()
            .ToList();

        return View(new AvailabilityCalendarViewModel { Year = y, Month = m, Days = days, Regions = regions });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(DateTime date, string status, int? maxMiniSessions, string? title, string? zone, string? location, string? miniStart, string? miniEnd, string? dayStart, string? dayEnd)
    {
        var d = date.Date;
        if (string.IsNullOrWhiteSpace(status) || status == "0")
        {
            var existing = await _db.DayAvailability.FirstOrDefaultAsync(x => x.Date == d);
            if (existing is not null)
            {
                _db.DayAvailability.Remove(existing);
                await _db.SaveChangesAsync();
            }
            TempData["Success"] = "Dia reposto na agenda semanal.";
        }
        else if (int.TryParse(status, out var s) && Enum.IsDefined(typeof(DayStatus), s))
        {
            var dayStatus = (DayStatus)s;
            var row = await _db.DayAvailability.FirstOrDefaultAsync(x => x.Date == d);
            if (row is null)
            {
                row = new DayAvailability { Date = d };
                _db.DayAvailability.Add(row);
            }
            row.Status = dayStatus;
            row.MaxMiniSessions = dayStatus == DayStatus.Event ? Math.Max(1, maxMiniSessions ?? 4) : null;
            row.Title = dayStatus == DayStatus.Event && !string.IsNullOrWhiteSpace(title)
                ? title.Trim()
                : null;
            // Event days: the event zone/locality. Available/maybe days: the
            // photographer's zone that day (empty = can travel anywhere).
            row.Zone = string.IsNullOrWhiteSpace(zone) ? null : zone.Trim();
            row.Location = dayStatus == DayStatus.Event && !string.IsNullOrWhiteSpace(location)
                ? location.Trim()
                : null;
            // Mini-session time window (event days only): the admin sets the hours
            // mini sessions are offered; clients then pick an available slot inside.
            var hasMiniInput = !string.IsNullOrWhiteSpace(miniStart) || !string.IsNullOrWhiteSpace(miniEnd);
            if (dayStatus == DayStatus.Event &&
                TimeSpan.TryParse(miniStart, out var ms) &&
                TimeSpan.TryParse(miniEnd, out var me) && ms < me)
            {
                row.MiniStart = ms;
                row.MiniEnd = me;
            }
            else
            {
                if (dayStatus == DayStatus.Event && hasMiniInput)
                    TempData["Warning"] = "Horários não guardados: escolhe a hora de início e de fim (a de fim tem de ser depois).";
                row.MiniStart = null;
                row.MiniEnd = null;
            }
            // Per-day hours override (available/maybe days only): when set, the
            // day has its own window instead of the weekly template. Empty = weekly.
            var isHoursDay = dayStatus is DayStatus.Available or DayStatus.Maybe;
            var hasDayHours = !string.IsNullOrWhiteSpace(dayStart) || !string.IsNullOrWhiteSpace(dayEnd);
            if (isHoursDay &&
                TimeSpan.TryParse(dayStart, out var ds) &&
                TimeSpan.TryParse(dayEnd, out var de) && ds < de)
            {
                row.WindowStart = ds;
                row.WindowEnd = de;
            }
            else
            {
                if (isHoursDay && hasDayHours)
                    TempData["Warning"] = "Horas do dia não guardadas: escolhe a hora de início e de fim (a de fim tem de ser depois).";
                row.WindowStart = null;
                row.WindowEnd = null;
            }
            await _db.SaveChangesAsync();
            TempData["Success"] = "Disponibilidade do dia atualizada.";
        }

        return RedirectToAction(nameof(Index), new { year = date.Year, month = date.Month });
    }

    // ---- Bulk marking: multiple days, one status + shared hours ----

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkSet(string[] dates, string status, string? dayStart, string? dayEnd, int? year, int? month)
    {
        var today = DateTime.Today;
        int? statusValue = string.IsNullOrWhiteSpace(status) ? null : (int?)int.Parse(status);

        TimeSpan? ws = null, we = null;
        if (TimeSpan.TryParse(dayStart, out var ds) && TimeSpan.TryParse(dayEnd, out var de) && ds < de)
        {
            ws = ds;
            we = de;
        }

        var changed = 0;
        foreach (var raw in dates ?? Array.Empty<string>())
        {
            if (!DateTime.TryParse(raw, out var parsed) || parsed.Date < today)
                continue;

            var d = parsed.Date;
            var row = await _db.DayAvailability.FirstOrDefaultAsync(x => x.Date == d);

            // "0" (or empty) clears the day's marking entirely.
            if (statusValue is null || statusValue == 0)
            {
                if (row is not null)
                {
                    _db.DayAvailability.Remove(row);
                    changed++;
                }
                continue;
            }
            if (!Enum.IsDefined(typeof(DayStatus), statusValue.Value))
                continue;

            var dayStatus = (DayStatus)statusValue.Value;
            if (row is null)
            {
                row = new DayAvailability { Date = d };
                _db.DayAvailability.Add(row);
            }

            // Switching an event day to something else drops the event-only fields.
            var wasEvent = row.Status == DayStatus.Event;
            row.Status = dayStatus;
            if (wasEvent && dayStatus != DayStatus.Event)
            {
                row.Title = null;
                row.Location = null;
                row.MiniStart = null;
                row.MiniEnd = null;
                row.MaxMiniSessions = null;
            }

            if (dayStatus is DayStatus.Available or DayStatus.Maybe)
            {
                // Shared hours (or 09:00–20:00 default); the day's zone, if any, is kept.
                row.WindowStart = ws;
                row.WindowEnd = we;
            }
            else if (dayStatus == DayStatus.Unavailable)
            {
                row.WindowStart = null;
                row.WindowEnd = null;
                row.Zone = null;
            }
            else if (dayStatus == DayStatus.Event)
            {
                row.MaxMiniSessions = row.MaxMiniSessions ?? 4;
                row.WindowStart = null;
                row.WindowEnd = null;
            }
            changed++;
        }

        await _db.SaveChangesAsync();
        TempData[changed > 0 ? "Success" : "Warning"] =
            changed > 0 ? $"{changed} dia(s) atualizado(s)." : "Nenhum dia atualizado (dias passados são ignorados).";
        return RedirectToAction(nameof(Index), new { year = year ?? today.Year, month = month ?? today.Month });
    }
}
