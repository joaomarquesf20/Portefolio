namespace ShotsByMarques.Web.Models;

/// <summary>
/// Operational mode of a gallery, computed from its flags (see Gallery.Mode):
///  - Portfolio: pure showcase — no prices, no cart, no "incluída" badges.
///  - ForSale: photos sold individually / in packs — prices + "+ Selecionar".
///  - PrivateDelivery: private booking gallery — photos already paid with the
///    session; a single header notice + per-photo "Descarregar" for the owner.
/// </summary>
public enum GalleryMode
{
    Portfolio,
    ForSale,
    PrivateDelivery
}

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

    /// <summary>
    /// Computed operational mode (get-only ⇒ EF Core never maps it — no
    /// migration needed). The three modes are STRICTLY separated (views must
    /// branch on this, not on ad-hoc flag combinations):
    ///  - PrivateDelivery: private booking gallery — the photos ARE the
    ///    delivery of an already-paid session (IsForSale leftovers are ignored).
    ///  - ForSale: public event gallery with at least one photo for sale.
    ///  - Portfolio: everything else — published session galleries and
    ///    showcase galleries. 100% commercial-free display.
    /// Note: relies on Photos being loaded (Detail includes it).
    /// </summary>
    public GalleryMode Mode =>
        (IsPrivate && BookingId.HasValue) ? GalleryMode.PrivateDelivery
        : (!IsSession && Photos.Count > 0 && Photos.Any(p => p.IsForSale)) ? GalleryMode.ForSale
        : GalleryMode.Portfolio;
}
