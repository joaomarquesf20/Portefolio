using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using System.Security.Claims;

namespace ShotsByMarques.Web.Controllers;

/// <summary>
/// Streams photo previews for files stored outside wwwroot.
/// NOTE: in production, PreviewPath must be a WATERMARKED derivative —
/// this endpoint is public, the full-resolution file is only served by
/// DownloadsController.File after payment.
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
        var photo = await _db.Photos.Include(p => p.Gallery).ThenInclude(g => g!.Booking).FirstOrDefaultAsync(p => p.Id == id);
        if (photo is null || photo.Gallery is null)
            return NotFound();

        if (photo.Gallery.IsPrivate)
        {
            var isOwner = User.Identity!.IsAuthenticated
                          && photo.Gallery.Booking?.ClientUserId == User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!isOwner && !User.IsInRole("Admin"))
                return NotFound();
        }
        else if (!photo.Gallery.IsPublished)
        {
            return NotFound();
        }

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

    private static string ContentTypeFor(string ext) => ext.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };
}
