using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.Services;

public sealed class SitePageBackgroundService
{
    public const string HomeHero = "home-hero";
    public const string HomePortfolio = "home-portfolio";
    public const string HomeServices = "home-services";
    public const string HomeContact = "home-contact";
    public const string Portfolio = "portfolio";
    public const string Sessions = "sessions";
    public const string Services = "services";
    // Kept for compatibility with settings created by older versions.
    public const string ServicesIntro = "services-intro";
    public const string ServicesPricing = "services-pricing";
    public const string ServicesLatest = "services-latest";

    public static readonly string[] Keys =
    {
        HomeHero,
        HomePortfolio,
        HomeServices,
        HomeContact,
        Portfolio,
        Sessions,
        Services
    };

    private readonly ApplicationDbContext _db;
    private readonly PhotoStorageService _storage;

    public SitePageBackgroundService(ApplicationDbContext db, PhotoStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<string> GetAsync(string key, string fallback)
    {
        var configured = await _db.SitePageSettings
            .AsNoTracking()
            .Where(s => s.PageKey == key)
            .Select(s => s.ImagePath)
            .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(configured) ? fallback : configured;
    }

    public async Task SaveAsync(string key, IFormFile image)
    {
        if (!Keys.Contains(key, StringComparer.Ordinal))
            throw new ArgumentException("Unknown page background key.", nameof(key));

        if (image.Length == 0)
            throw new InvalidOperationException("The selected image is empty.");

        var existing = await _db.SitePageSettings.FirstOrDefaultAsync(s => s.PageKey == key);
        var oldPath = existing?.ImagePath;
        var newPath = await _storage.SaveCoverAsync(image);

        if (existing is null)
        {
            _db.SitePageSettings.Add(new SitePageSetting
            {
                PageKey = key,
                ImagePath = newPath,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.ImagePath = newPath;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        // Only remove files created as uploaded backgrounds. Seeded SVGs and
        // gallery covers are never deleted by this feature.
        if (!string.IsNullOrWhiteSpace(oldPath) && oldPath.StartsWith("/uploads/covers/", StringComparison.OrdinalIgnoreCase))
            _storage.DeletePublicCover(oldPath);
    }
}
