using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.ViewModels;

public class AvailabilityCalendarViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<CalendarDay> Days { get; set; } = new();

    /// <summary>Distinct zones (e.g. "Porto", "Lisboa") for the event/day location pickers.</summary>
    public List<string> Regions { get; set; } = new();
}

public class CalendarDay
{
    public DateTime Date { get; set; }
    public bool InMonth { get; set; }
    public bool IsPast { get; set; }
    public DayStatus? Status { get; set; }
    public int? MaxMiniSessions { get; set; }
    public string? EventTitle { get; set; }
    public int MiniBooked { get; set; }

    /// <summary>All non-cancelled bookings on this day (admin calendar only).</summary>
    public int BookedCount { get; set; }

    /// <summary>Zone where the photographer will be that day (event zone or fixed availability).</summary>
    public string? Zone { get; set; }

    /// <summary>Event locality, e.g. "Parque da Cidade".</summary>
    public string? Location { get; set; }

    /// <summary>Mini-session time window on the event day (admin-defined).</summary>
    public TimeSpan? MiniStart { get; set; }
    public TimeSpan? MiniEnd { get; set; }

    /// <summary>Per-day hours override (available/maybe days). Null = weekly template.</summary>
    public TimeSpan? WindowStart { get; set; }
    public TimeSpan? WindowEnd { get; set; }
}
