namespace ShotsByMarques.Web.Models;

public class Booking
{
    public int Id { get; set; }
    public string ClientUserId { get; set; } = string.Empty;
    public int SessionTypeId { get; set; }
    public DateTime Start { get; set; }
    public string? Notes { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? ClientUser { get; set; }
    public SessionType? SessionType { get; set; }
    public Gallery? Gallery { get; set; }

    public DateTime End => Start.AddMinutes(SessionType?.DurationMinutes ?? 120);
}
