using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.ViewModels;
using System.Security.Claims;

namespace ShotsByMarques.Web.Controllers;

public class GalleriesController : Controller
{
    private readonly ApplicationDbContext _db;

    public GalleriesController(ApplicationDbContext db) => _db = db;

    [Route("portfolio")]
    public async Task<IActionResult> Index()
    {
        var galleries = await _db.Galleries
            .Where(g => g.IsPublished && !g.IsPrivate)
            .Include(g => g.Photos)
            .OrderByDescending(g => g.EventDate)
            .ToListAsync();

        // Sessões: photos included in the session price (never sold individually).
        // Eventos: photos sold individually / in packs. The admin picks the type
        // when creating the gallery; galleries without photos use the explicit flag.
        var sessions = galleries.Where(g => g.IsSession).ToList();
        var events = galleries.Where(g => !g.IsSession).ToList();

        return View(new PortfolioViewModel { Sessions = sessions, Events = events });
    }

    [Route("galeria/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var gallery = await _db.Galleries
            .Include(g => g.Booking)
            .Include(g => g.Photos.OrderBy(p => p.SortOrder))
            .FirstOrDefaultAsync(g => g.Id == id);

        if (gallery is null)
            return NotFound();

        // Private galleries are only for the client who booked (or an admin).
        if (gallery.IsPrivate)
        {
            var isOwner = User.Identity!.IsAuthenticated
                          && gallery.Booking?.ClientUserId == User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!isOwner && !User.IsInRole("Admin"))
                return NotFound();
        }
        else if (!gallery.IsPublished)
        {
            return NotFound();
        }

        // Show available discount packs so clients know what to aim for.
        ViewData["PhotoPacks"] = await _db.PhotoPacks
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        return View(new GalleryDetailViewModel { Gallery = gallery, Photos = gallery.Photos });
    }
}
