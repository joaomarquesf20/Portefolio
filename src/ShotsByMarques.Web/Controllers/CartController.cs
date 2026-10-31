using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShotsByMarques.Web.Services;

namespace ShotsByMarques.Web.Controllers;

/// <summary>
/// Server-backed cart endpoints for signed-in clients. Anonymous visitors
/// never call these (their cart is localStorage-only); site.js posts here
/// only when the page rendered authenticated. Responses always carry the
/// FULL cart id list so the browser adopts the authoritative copy.
/// </summary>
[Authorize]
public class CartController : Controller
{
    private readonly CartService _cart;

    public CartController(CartService cart) => _cart = cart;

    private string UserId() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

    /// <summary>POST /cart/sync — merge-adds comma-separated photo ids
    /// (unknown ids dropped) and returns the full DB list. Doubles as the
    /// anonymous-cart merge on first login (site.js posts on page load).</summary>
    [HttpPost("cart/sync")]
    [ValidateAntiForgeryToken]
    [Produces("application/json")]
    public async Task<IActionResult> Sync([FromForm] string? photoIds)
    {
        var ids = (photoIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0);

        return Ok(new { photoIds = await _cart.SyncAsync(UserId(), ids) });
    }

    /// <summary>POST /cart/remove — explicit removal of one photo id.</summary>
    [HttpPost("cart/remove")]
    [ValidateAntiForgeryToken]
    [Produces("application/json")]
    public async Task<IActionResult> Remove([FromForm] int photoId)
    {
        if (photoId > 0)
            await _cart.RemoveAsync(UserId(), photoId);
        return Ok(new { photoIds = await _cart.GetPhotoIdsAsync(UserId()) });
    }

    /// <summary>POST /cart/clear — empties the DB cart (client UI clear or
    /// after a paid order at Checkout/Success).</summary>
    [HttpPost("cart/clear")]
    [ValidateAntiForgeryToken]
    [Produces("application/json")]
    public async Task<IActionResult> Clear()
    {
        await _cart.ClearAsync(UserId());
        return Ok(new { photoIds = Array.Empty<int>() });
    }
}
