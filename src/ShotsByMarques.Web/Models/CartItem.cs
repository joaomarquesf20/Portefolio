namespace ShotsByMarques.Web.Models;

/// <summary>
/// One photo saved in a signed-in client's cart. Makes the photo selection
/// follow the account across devices: SQLite-backed, exposed through
/// CartService/CartController, and adopted by site.js on page load
/// (anonymous carts live only in localStorage until the first login, which
/// merges them into the DB).
/// </summary>
public class CartItem
{
    public int Id { get; set; }

    /// <summary>ASP.NET Identity user id — same value as Booking.ClientUserId.</summary>
    public string ClientUserId { get; set; } = string.Empty;

    public int PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
