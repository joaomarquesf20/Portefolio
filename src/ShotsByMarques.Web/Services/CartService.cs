using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.Services;

/// <summary>
/// DB-backed cart for signed-in clients: the photo selection follows the
/// account across devices (mobile ⟷ PC). Anonymous visitors never reach it —
/// their cart lives in localStorage and site.js pushes those ids here the
/// first time they load a page authenticated (merge on login).
/// </summary>
public class CartService
{
    private readonly ApplicationDbContext _db;

    public CartService(ApplicationDbContext db) => _db = db;

    /// <summary>Photo ids currently saved for this user, oldest first.</summary>
    public async Task<List<int>> GetPhotoIdsAsync(string userId) =>
        await _db.CartItems.AsNoTracking()
            .Where(c => c.ClientUserId == userId)
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .Select(c => c.PhotoId)
            .ToListAsync();

    /// <summary>
    /// Union-adds the given photo ids to the user's cart, then returns the
    /// FULL id list. The "sync = add-only, remove/clear are explicit"
    /// semantics plus "response carries the whole list" keeps every device
    /// converged: callers replace their local copy with what comes back.
    /// Unknown photo ids (deleted/never existed) are silently dropped — a
    /// stale local cart cannot poison the server cart.
    /// </summary>
    public async Task<List<int>> SyncAsync(string userId, IEnumerable<int> photoIds)
    {
        var desired = photoIds.Where(id => id > 0).Distinct().ToList();
        if (desired.Count > 0)
        {
            var known = await _db.Photos.AsNoTracking()
                .Where(p => desired.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync();
            desired = known; // only ids the Photos table actually has
        }

        var existingIds = (await _db.CartItems
            .Where(c => c.ClientUserId == userId)
            .Select(c => c.PhotoId)
            .ToListAsync()).ToHashSet();

        foreach (var id in desired)
        {
            if (existingIds.Contains(id)) continue;
            _db.CartItems.Add(new CartItem { ClientUserId = userId, PhotoId = id });
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Concurrent duplicate (double-click / second tab): the unique
            // index (ClientUserId, PhotoId) rejected the row — treat it as
            // already saved and keep serving the real list.
            _db.ChangeTracker.Clear();
        }

        return await GetPhotoIdsAsync(userId);
    }

    /// <summary>Removes every row for one photo (explicit user removal).</summary>
    public async Task RemoveAsync(string userId, int photoId)
    {
        var rows = await _db.CartItems
            .Where(c => c.ClientUserId == userId && c.PhotoId == photoId)
            .ToListAsync();
        if (rows.Count == 0) return;
        _db.CartItems.RemoveRange(rows);
        await _db.SaveChangesAsync();
    }

    /// <summary>Empties the user's cart (explicit clear or paid order).</summary>
    public async Task ClearAsync(string userId)
    {
        var rows = await _db.CartItems
            .Where(c => c.ClientUserId == userId)
            .ToListAsync();
        if (rows.Count == 0) return;
        _db.CartItems.RemoveRange(rows);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Subtotal + best automatic pack discount + final total for a photo
    /// selection — the same pricing rules the checkout page renders, used by
    /// the AJAX cart endpoints so JS updates totals from the server instead
    /// of duplicating pack math in the browser. Count counts the FOR-SALE
    /// photos actually found; PackName/MinPackPhotos mirror the checkout
    /// view-model (name of the applied pack, smallest active pack threshold).
    /// </summary>
    public sealed record CartSummary(int Count, decimal Subtotal, decimal Discount, decimal Total, string? PackName, int? MinPackPhotos);

    public async Task<CartSummary> GetSummaryAsync(IReadOnlyCollection<int> photoIds)
    {
        var prices = await _db.Photos.AsNoTracking()
            .Where(p => photoIds.Contains(p.Id) && p.IsForSale)
            .Select(p => p.Price)
            .ToListAsync();

        var packs = await _db.PhotoPacks.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.MinPhotos)
            .ToListAsync();

        var subtotal = prices.Sum();
        var pack = PackService.BestPack(packs, photoIds.Count, subtotal);

        return new CartSummary(
            Count: photoIds.Count,
            Subtotal: pack.Subtotal,
            Discount: pack.Discount,
            Total: pack.Total,
            PackName: pack.PackName,
            MinPackPhotos: packs.FirstOrDefault()?.MinPhotos);
    }
}
