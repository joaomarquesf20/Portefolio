using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Services;
using System.Security.Claims;

namespace ShotsByMarques.Web.Controllers;

/// <summary>
/// Streams photo derivatives for files stored outside wwwroot:
///   /Photos/Thumb — CLEAN, watermark-free, small (grid tiles + covers).
///   /Photos/Preview — WATERMARKED, larger (lightbox only; the page never
///     puts it in a src attribute — site.js fetches it into a blob: URL).
/// The full-resolution original is only served by DownloadsController.File
/// after payment/ownership checks.
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
        if (!fullPath.StartsWith(photosRoot + Path.DirectorySeparatorChar) || !System.IO.File.Exists(fullPath))
            return NotFound();

        return PhysicalFile(fullPath, ContentTypeFor(Path.GetExtension(fullPath)));
    }

    /// <summary>
    /// Watermark-FREE thumbnail (grid tiles, covers). Built on demand from the
    /// original and cached on disk beside it. Authorization mirrors Preview:
    /// private galleries expose photos only to their owner (or admins), and
    /// unpublished public galleries stay invisible. The ant-theft split is
    /// enforced here: clients can grab this small clean image freely, but the
    /// large watermarked preview (/Photos/Preview) is what the lightbox shows.
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
            Path.GetFileNameWithoutExtension(fullPath) + "-thumb.jpg");

        if (!System.IO.File.Exists(thumbPath))
        {
            try
            {
                PhotoStorageService.GenerateCleanThumb(fullPath, thumbPath);
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
