using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Services;
using System.Security.Claims;

namespace ShotsByMarques.Web.Controllers;

/// <summary>
/// Streams photo derivatives for files stored outside wwwroot. Derivatives
/// are PRE-BAKED at upload (and repaired by the admin update/publish flows)
/// — these endpoints do not run SkiaSharp during normal viewer navigation:
///   /Photos/Thumb — WATERMARKED, small 900px (grid tiles + lightbox
///     initial frame: tiles show baked watermark pixels).
///   /Photos/Preview — WATERMARKED, larger 1600px (lightbox only; the page
///     never puts it in a src attribute — site.js fetches it into a blob: URL).
///   /Photos/Cover — CLEAN high-res 1920px, for covers / hero backgrounds.
/// STRICT NO-FALLBACK: if a watermarked derivative is missing it is rebuilt
/// from the original synchronously BEFORE responding (or the endpoint 404s) —
/// the clean original is NEVER streamed from App_Data/photos/ by this
/// controller under any circumstance. The full-resolution original is only
/// served by DownloadsController.File after payment/ownership checks.
/// </summary>
public class PhotosController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public PhotosController(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [Route("photos/preview/{id:int}")]
    public async Task<IActionResult> Preview(int id)
    {
        var photo = await AuthorizedPhotoAsync(id);
        if (photo is null) return NotFound();

        if (photo.PreviewPath.StartsWith("/"))
            return Redirect(photo.PreviewPath); // legacy wwwroot path (sample photos)

        if (!photo.PreviewPath.StartsWith("photos/"))
            return NotFound();

        var photosRoot = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "App_Data", "photos"));
        var fullPath = Path.GetFullPath(Path.Combine(photosRoot, photo.PreviewPath["photos/".Length..]));
        if (!fullPath.StartsWith(photosRoot + Path.DirectorySeparatorChar))
            return NotFound();

        // STRICT NO-FALLBACK: the watermarked preview is the only thing that
        // leaves this endpoint. If the baked file vanished (manual cleanup or
        // an older row), rebuild it from the original SYNCHRONOUSLY first.
        // The clean original file is NEVER streamed here, not even as a
        // placeholder for a pending regeneration.
        if (!System.IO.File.Exists(fullPath))
        {
            var originalPath = Path.GetFullPath(Path.Combine(photosRoot, photo.FilePath["photos/".Length..]));
            if (!photo.FilePath.StartsWith("photos/")
                || !originalPath.StartsWith(photosRoot + Path.DirectorySeparatorChar)
                || !System.IO.File.Exists(originalPath))
                return NotFound();

            try
            {
                PhotoStorageService.GenerateWatermarkedPreview(originalPath, fullPath);
            }
            catch (IOException) when (System.IO.File.Exists(fullPath))
            {
                // Lost a generate-race to a concurrent request — the file exists now.
            }
            catch
            {
                return NotFound();
            }
        }

        return PhysicalFile(fullPath, ContentTypeFor(Path.GetExtension(fullPath)));
    }

    /// <summary>
    /// WATERMARKED thumbnail (grid tiles, lightbox initial frame). Served
    /// pre-baked at upload ("{base}-wt.jpg" beside the original, SkiaSharp);
    /// if the file is missing it is generated synchronously BEFORE responding
    /// (never the clean original, never a partial frame). Authorization
    /// mirrors Preview: private galleries expose photos only to their owner
    /// (or admins), and unpublished public galleries stay invisible.
    /// Anti-theft guarantee: screenshots/screen-recording of grid tiles or
    /// the lightbox always capture the baked watermark.
    /// </summary>
    [Route("photos/thumb/{id:int}")]
    public async Task<IActionResult> Thumb(int id)
    {
        var photo = await AuthorizedPhotoAsync(id);
        if (photo is null) return NotFound();

        if (photo.FilePath.StartsWith("/"))
            return Redirect(photo.FilePath); // legacy wwwroot sample asset (already clean)

        if (!photo.FilePath.StartsWith("photos/"))
            return NotFound();

        var photosRoot = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "App_Data", "photos"));
        var fullPath = Path.GetFullPath(Path.Combine(photosRoot, photo.FilePath["photos/".Length..]));
        if (!fullPath.StartsWith(photosRoot + Path.DirectorySeparatorChar) || !System.IO.File.Exists(fullPath))
            return NotFound();

        var thumbPath = Path.Combine(
            Path.GetDirectoryName(fullPath) ?? photosRoot,
            Path.GetFileNameWithoutExtension(fullPath) + "-wt.jpg");

        if (!System.IO.File.Exists(thumbPath))
        {
            try
            {
                PhotoStorageService.GenerateWatermarkedThumb(fullPath, thumbPath);
            }
            catch (IOException) when (System.IO.File.Exists(thumbPath))
            {
                // Lost a generate-race to a concurrent request — the file exists now.
            }
            catch
            {
                return NotFound();
            }
        }

        Response.Headers["Cache-Control"] = "public, max-age=604800";
        return PhysicalFile(thumbPath, "image/jpeg");
    }

    /// <summary>
    /// CLEAN, HIGH-RESOLUTION derivative (covers, hero banners, full-bleed
    /// page backgrounds). 1920px, JPEG quality 92 — sharp on desktop
    /// monitors. Authorization mirrors Preview/Thumb; still never the raw
    /// original (that only goes out after payment/ownership checks), and no
    /// watermark — covers/backgrounds are the ONLY clean public asset (the
    /// site's public face); grid tiles and the lightbox show the watermarked
    /// derivatives instead.
    /// </summary>
    [Route("photos/cover/{id:int}")]
    public async Task<IActionResult> Cover(int id)
    {
        var photo = await AuthorizedPhotoAsync(id);
        if (photo is null) return NotFound();

        if (photo.FilePath.StartsWith("/"))
            return Redirect(photo.FilePath); // legacy wwwroot sample asset (already clean)

        if (!photo.FilePath.StartsWith("photos/"))
            return NotFound();

        var photosRoot = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "App_Data", "photos"));
        var fullPath = Path.GetFullPath(Path.Combine(photosRoot, photo.FilePath["photos/".Length..]));
        if (!fullPath.StartsWith(photosRoot + Path.DirectorySeparatorChar) || !System.IO.File.Exists(fullPath))
            return NotFound();

        var coverPath = Path.Combine(
            Path.GetDirectoryName(fullPath) ?? photosRoot,
            Path.GetFileNameWithoutExtension(fullPath) + "-large.jpg");

        if (!System.IO.File.Exists(coverPath))
        {
            try
            {
                PhotoStorageService.GenerateCleanLarge(fullPath, coverPath);
            }
            catch (IOException) when (System.IO.File.Exists(coverPath))
            {
                // Lost a generate-race to a concurrent request — the file exists now.
            }
            catch
            {
                return NotFound();
            }
        }

        Response.Headers["Cache-Control"] = "public, max-age=604800";
        return PhysicalFile(coverPath, "image/jpeg");
    }

    /// <summary>Shared access rules: private → owner/admin; public → published only.</summary>
    private async Task<Models.Photo?> AuthorizedPhotoAsync(int id)
    {
        var photo = await _db.Photos
            .Include(p => p.Gallery).ThenInclude(g => g!.Booking)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (photo is null || photo.Gallery is null)
            return null;

        if (photo.Gallery.IsPrivate)
        {
            var isOwner = User.Identity!.IsAuthenticated
                          && photo.Gallery.Booking?.ClientUserId == User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!isOwner && !User.IsInRole("Admin"))
                return null;
        }
        else if (!photo.Gallery.IsPublished)
        {
            return null;
        }

        return photo;
    }

    private static string ContentTypeFor(string ext) => ext.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };
}
