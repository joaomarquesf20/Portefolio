using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.Services;

public record PackDiscount(PhotoPack? Pack, decimal Subtotal, decimal Discount, decimal Total)
{
    public string? PackName => Pack?.Name;
}

public static class PackService
{
    /// <summary>
    /// Returns the best applicable pack for the given photo count:
    /// the active pack with the highest discount whose MinPhotos is satisfied.
    /// </summary>
    public static PackDiscount BestPack(IEnumerable<PhotoPack> packs, int photoCount, decimal subtotal)
    {
        var best = packs
            .Where(p => p.IsActive && p.MinPhotos <= photoCount)
            .OrderByDescending(p => p.DiscountPercent)
            .FirstOrDefault();

        if (best is null)
            return new PackDiscount(null, subtotal, 0m, subtotal);

        var discount = Math.Round(subtotal * best.DiscountPercent / 100m, 2);
        return new PackDiscount(best, subtotal, discount, subtotal - discount);
    }
}
