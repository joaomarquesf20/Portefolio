namespace ShotsByMarques.Web.Models;

/// <summary>Per-date availability status set by the admin on the calendar.</summary>
public enum DayStatus
{
    Available = 1,
    Unavailable = 2,
    Maybe = 3,
    Event = 4
}

/// <summary>
/// A per-date marking on the availability calendar. Days without a row are
/// closed (no bookings). Event days additionally offer mini sessions, capped
/// at <see cref="MaxMiniSessions"/> bookings.
/// </summary>
public class DayAvailability
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public DayStatus Status { get; set; }
    public int? MaxMiniSessions { get; set; }
    public string? Title { get; set; }

    /// <summary>
    /// Zone (e.g. "Porto") where the photographer will be that day.
    /// For available/maybe days: empty = can travel anywhere.
    /// For event days: the zone where the event takes place.
    /// </summary>
    public string? Zone { get; set; }

    /// <summary>Locality of the event (e.g. "Parque da Cidade") — event days only.</summary>
    public string? Location { get; set; }

    /// <summary>
    /// Time window of the mini sessions on the event day (local wall-clock).
    /// The admin defines it; clients pick an available slot inside it.
    /// Null = the admin has not scheduled the event's mini sessions yet.
    /// </summary>
    public TimeSpan? MiniStart { get; set; }
    public TimeSpan? MiniEnd { get; set; }

    /// <summary>
    /// Hours of the day (available/maybe days): clients can only book inside
    /// this window. Null = standard 09:00–20:00.
    /// </summary>
    public TimeSpan? WindowStart { get; set; }
    public TimeSpan? WindowEnd { get; set; }
}
