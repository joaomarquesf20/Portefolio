using System.IO.Compression;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;
using ShotsByMarques.Web.Services;
using ShotsByMarques.Web.ViewModels;

namespace ShotsByMarques.Web.Controllers;

[Authorize]
public class DownloadsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly PhotoStorageService _storage;

    public DownloadsController(ApplicationDbContext db, IWebHostEnvironment env, PhotoStorageService storage)
    {
        _db = db;
        _env = env;
        _storage = storage;
    }

    [Route("downloads")]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var orders = await _db.Orders
            .Where(o => o.ClientUserId == userId)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        // Session galleries: photos are INCLUDED in the session price (not sold
        // individually), so the client downloads them here without paying again.
        var sessionGalleries = await _db.Galleries
            .Include(g => g.Booking).ThenInclude(b => b!.SessionType)
            .Include(g => g.Photos)
            .Where(g => g.IsPrivate && g.Booking != null && g.Booking.ClientUserId == userId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

        return View(new DownloadsViewModel { Orders = orders, SessionGalleries = sessionGalleries });
    }

    /// <summary>Streams a purchased photo file after checking ownership + payment.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("downloads/ficheiro/{orderItemId:int}")]
    public async Task<IActionResult> File(int orderItemId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var item = await _db.OrderItems
            .Include(i => i.Order)
            .FirstOrDefaultAsync(i => i.Id == orderItemId && i.Order!.ClientUserId == userId);

        if (item is null || item.Order!.Status != OrderStatus.Paid)
            return NotFound();

        var (fullPath, contentType) = ResolveFile(item.FilePath);
        if (fullPath is null || !System.IO.File.Exists(fullPath))
            return NotFound();

        var fileName = SafeFileName(item.Title) + Path.GetExtension(item.FilePath);
        return PhysicalFile(fullPath, contentType, fileName, enableRangeProcessing: true);
    }

    /// <summary>
    /// Downloads ALL photos of a paid order as a single ZIP file.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("downloads/zip/{orderId:int}")]
    public async Task<IActionResult> OrderZip(int orderId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.ClientUserId == userId);

        if (order is null || order.Status != OrderStatus.Paid)
            return NotFound();

        // Write the ZIP to a temp file instead of building it in memory — a full
        // order can be 1GB+ of original photos.
        var zipPath = Path.Combine(Path.GetTempPath(), $"sbm_{Guid.NewGuid():N}.zip");
        try
        {
            using (var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in order.Items)
                {
                    var (fullPath, _) = ResolveFile(item.FilePath);
                    if (fullPath is null || !System.IO.File.Exists(fullPath))
                        continue;

                    var baseName = SafeFileName(item.Title);
                    var extension = Path.GetExtension(item.FilePath);
                    var entryName = baseName + extension;
                    var counter = 1;
                    while (!usedNames.Add(entryName))
                        entryName = $"{baseName} ({counter++}){extension}";

                    var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                    await using var entryStream = entry.Open();
                    await using var fileStream = System.IO.File.OpenRead(fullPath);
                    await fileStream.CopyToAsync(entryStream);
                }
            }

            // Remove the temp file once the response has finished streaming.
            Response.OnCompleted(() =>
            {
                try { System.IO.File.Delete(zipPath); } catch { /* best effort */ }
                return Task.CompletedTask;
            });

            return PhysicalFile(zipPath, "application/zip", $"{SafeFileName(order.OrderNumber)}.zip");
        }
        catch
        {
            try { System.IO.File.Delete(zipPath); } catch { /* best effort */ }
            throw;
        }
    }

    /// <summary>
    /// Streams a session photo (original, NO watermark) to the client who booked
    /// the session — the session was already paid, so photos are included.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("downloads/sessao/{photoId:int}")]
    public async Task<IActionResult> SessionPhoto(int photoId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var photo = await _db.Photos
            .Include(p => p.Gallery).ThenInclude(g => g!.Booking).ThenInclude(b => b!.SessionType)
            .FirstOrDefaultAsync(p => p.Id == photoId);

        // Only the client who booked the session (or an admin) may download.
        var isOwner = photo?.Gallery?.IsPrivate == true
                      && photo.Gallery.Booking != null
                      && photo.Gallery.Booking.ClientUserId == userId;
        if (photo is null || (!isOwner && !User.IsInRole("Admin")))
            return NotFound();

        // Session photos are included in the price — they are never sold per photo.
        if (photo.IsForSale)
            return NotFound();

        // Photos unlock only after the balance is paid (delivery). Exempt:
        // admins and mini-session bookings (reservations, no split payment).
        var booking = photo.Gallery!.Booking;
        var isMini = booking?.SessionType?.IsMiniSessionType == true;
        if (!User.IsInRole("Admin") && !isMini && booking is { BalancePaid: false })
            return NotFound();

        var (fullPath, contentType) = ResolveFile(photo.FilePath);
        if (fullPath is null || !System.IO.File.Exists(fullPath))
            return NotFound();

        var fileName = SafeFileName(photo.Title) + Path.GetExtension(photo.FilePath);
        return PhysicalFile(fullPath, contentType, fileName, enableRangeProcessing: true);
    }

    /// <summary>Cleans a name so it is safe to use as a download filename.</summary>
    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "foto" : cleaned.Trim();
    }

    /// <summary>
    /// Resolves a stored path to an absolute file path, allowing only:
    ///  - "photos/..." stored OUTSIDE wwwroot (full-resolution uploads)
    ///  - "/sample-photos/..." inside wwwroot (seed demo placeholders)
    /// </summary>
    private (string? FullPath, string ContentType) ResolveFile(string path)
    {
        var photosRoot = Path.GetFullPath(_storage.PhotosRoot);
        if (path.StartsWith("photos/"))
        {
            var full = Path.GetFullPath(Path.Combine(photosRoot, path["photos/".Length..]));
            return full.StartsWith(photosRoot + Path.DirectorySeparatorChar)
                ? (full, ContentTypeFor(Path.GetExtension(full)))
                : (null, "application/octet-stream");
        }

        if (path.StartsWith("/sample-photos/"))
        {
            var webRoot = Path.GetFullPath(_env.WebRootPath);
            var full = Path.GetFullPath(Path.Combine(webRoot, path.TrimStart('/')));
            return full.StartsWith(webRoot + Path.DirectorySeparatorChar)
                ? (full, ContentTypeFor(Path.GetExtension(full)))
                : (null, "application/octet-stream");
        }

        return (null, "application/octet-stream");
    }

    private static string ContentTypeFor(string ext) => ext.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        _ => "image/jpeg"
    };
}
