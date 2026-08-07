namespace ShotsByMarques.Web.Models;

public class Gallery
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Description { get; set; }
    public string? CoverPath { get; set; }
    public DateTime? EventDate { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When set, the gallery is PRIVATE and only visible to the client
    /// who booked the linked session (and to admins). Used to deliver
    /// the photos of a session to the right person.
    /// </summary>
    public int? BookingId { get; set; }
    public bool IsPrivate { get; set; }
    public Booking? Booking { get; set; }

    /// <summary>
    /// Gallery kind chosen by the admin:
    ///  - Session (true): photos are INCLUDED in the session price (never sold per photo)
    ///  - Event (false): photos are sold individually / in packs
    /// Used instead of guessing from the photos when listing the portfolio.
    /// </summary>
    public bool IsSession { get; set; }

    public List<Photo> Photos { get; set; } = new();
}
