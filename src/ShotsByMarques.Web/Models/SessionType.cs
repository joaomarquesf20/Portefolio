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

    /// <summary>Mini sessions booked on event days (never listed on the public sessions page).</summary>
    public bool IsMiniSessionType { get; set; }

    public List<Booking> Bookings { get; set; } = new();
}
