namespace ShotsByMarques.Web.Models;

public class SessionType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DurationMinutes { get; set; } = 120;
    public decimal Price { get; set; }
    public string? ImagePath { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    /// <summary>
    /// Mini sessions — rendered in the public sessions grid like any other
    /// type, but bookable only through the event-day flow at
    /// /sessoes/mini-sessao/{date} (the normal booking route rejects them).
    /// </summary>
    public bool IsMiniSessionType { get; set; }

    public List<Booking> Bookings { get; set; } = new();
}
