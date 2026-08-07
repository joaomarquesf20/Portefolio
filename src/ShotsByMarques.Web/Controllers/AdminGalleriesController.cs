using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;
using ShotsByMarques.Web.Services;

namespace ShotsByMarques.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminGalleriesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PhotoStorageService _storage;

    public AdminGalleriesController(ApplicationDbContext db, PhotoStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    private async Task<List<Booking>> BookingsAsync() => await _db.Bookings
        .Include(b => b.ClientUser)
        .Include(b => b.SessionType)
        .Where(b => b.Status != BookingStatus.Cancelled)
        .OrderByDescending(b => b.Start)
        .ToListAsync();

    public async Task<IActionResult> Index()
    {
        var galleries = await _db.Galleries
            .Include(g => g.Photos)
            .Include(g => g.Booking).ThenInclude(b => b!.ClientUser)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();
        return View(galleries);
    }

    public async Task<IActionResult> Create()
    {
        ViewData["Bookings"] = await BookingsAsync();
        return View(new Gallery());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Gallery gallery, IFormFile? cover)
    {
        ViewData["Bookings"] = await BookingsAsync();

        // A gallery linked to a booking is always private (delivered to that client).
        if (gallery.BookingId.HasValue)
        {
            gallery.IsPrivate = true;
            if (await _db.Galleries.AnyAsync(g => g.BookingId == gallery.BookingId))
                ModelState.AddModelError(nameof(gallery.BookingId), "Já existe uma galeria para esta marcação.");
        }
        else if (gallery.IsPrivate)
            ModelState.AddModelError(nameof(gallery.BookingId), "Escolhe a marcação associada à galeria privada.");

        if (ModelState.IsValid)
        {
            gallery.CreatedAt = DateTime.UtcNow;
            // A gallery linked to a booking is always a session gallery (photos included).
            if (gallery.BookingId.HasValue)
                gallery.IsSession = true;
            if (cover is not null && cover.Length > 0)
            {
                gallery.CoverPath = await _storage.SaveCoverAsync(cover);
            }
            _db.Galleries.Add(gallery);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(gallery);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var gallery = await _db.Galleries.FindAsync(id);
        if (gallery is null)
            return NotFound();
        ViewData["Bookings"] = await BookingsAsync();
        return View(gallery);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Gallery gallery)
    {
        if (id != gallery.Id)
            return NotFound();

        ViewData["Bookings"] = await BookingsAsync();
        if (gallery.BookingId.HasValue)
        {
            gallery.IsPrivate = true;
            if (await _db.Galleries.AnyAsync(g => g.BookingId == gallery.BookingId && g.Id != gallery.Id))
                ModelState.AddModelError(nameof(gallery.BookingId), "Já existe uma galeria para esta marcação.");
        }
        else if (gallery.IsPrivate)
            ModelState.AddModelError(nameof(gallery.BookingId), "Escolhe a marcação associada à galeria privada.");

        if (!ModelState.IsValid)
            return View(gallery);

        // A gallery linked to a booking is always a session gallery (photos included).
        if (gallery.BookingId.HasValue)
            gallery.IsSession = true;

        _db.Update(gallery);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(int id)
    {
        var gallery = await _db.Galleries.FindAsync(id);
        if (gallery is not null)
        {
            gallery.IsPublished = !gallery.IsPublished;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var gallery = await _db.Galleries.Include(g => g.Photos).FirstOrDefaultAsync(g => g.Id == id);
        if (gallery is not null)
        {
            foreach (var photo in gallery.Photos)
                _storage.Delete(photo.FilePath, photo.PreviewPath);
            _db.Galleries.Remove(gallery);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Photos(int id)
    {
        var gallery = await _db.Galleries
            .Include(g => g.Booking).ThenInclude(b => b!.ClientUser)
            .Include(g => g.Photos.OrderBy(p => p.SortOrder))
            .FirstOrDefaultAsync(g => g.Id == id);
        return gallery is null ? NotFound() : View(gallery);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadPhotos(int id, List<IFormFile> files, decimal price, bool forSale = true)
    {
        var gallery = await _db.Galleries.FindAsync(id);
        if (gallery is null)
            return NotFound();
        if (price < 0)
            price = 0;

        // Session galleries (chosen as "Sessão" or linked to a booking) are ALWAYS
        // included in the session price — the client already paid, so photos are
        // never sold separately.
        var isSessionGallery = gallery.IsSession || gallery.BookingId.HasValue;
        if (isSessionGallery)
            forSale = false;

        var maxSort = await _db.Photos.Where(p => p.GalleryId == id).MaxAsync(p => (int?)p.SortOrder) ?? 0;
        var skipped = 0;

        foreach (var file in files.Where(f => f.Length > 0))
        {
            if (file.Length > PhotoStorageService.MaxUploadBytes)
            {
                skipped++;
                continue;
            }
            var (filePath, previewPath) = await _storage.SaveAsync(file, id);
            _db.Photos.Add(new Photo
            {
                GalleryId = id,
                Title = Path.GetFileNameWithoutExtension(file.FileName),
                FilePath = filePath,
                PreviewPath = previewPath,
                // Sessões: fotos incluídas (sem preço). Eventos: fotos à venda.
                Price = forSale ? price : 0m,
                SortOrder = ++maxSort,
                IsForSale = forSale
            });
        }

        // Remove the seeded sample photos from this gallery when the admin uploads
        // the first real photos — they are placeholder SVGs, not real work.
        var realUploaded = files.Any(f => f.Length > 0 && f.Length <= PhotoStorageService.MaxUploadBytes);
        var removedSamples = 0;
        if (realUploaded)
        {
            var samplePhotos = await _db.Photos
                .Where(p => p.GalleryId == id && p.FilePath.StartsWith("/sample-photos/"))
                .ToListAsync();
            removedSamples = samplePhotos.Count;
            _db.Photos.RemoveRange(samplePhotos);
        }

        await _db.SaveChangesAsync();
        if (removedSamples > 0)
            TempData["Success"] = $"Fotos carregadas. Foram removidas {removedSamples} foto(s) de exemplo desta galeria.";
        if (skipped > 0)
            TempData["Error"] = $"{skipped} ficheiro(s) ignorado(s) (limite de 25 MB).";
        return RedirectToAction(nameof(Photos), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSale(int id)
    {
        var photo = await _db.Photos.Include(p => p.Gallery).FirstOrDefaultAsync(p => p.Id == id);
        if (photo is null)
            return NotFound();

        // Session galleries: photos are ALWAYS included in the session price —
        // they can never be sold separately.
        if (photo.Gallery!.IsSession || photo.Gallery.BookingId.HasValue)
        {
            TempData["Error"] = "As fotos de uma galeria de sessão estão incluídas no preço — não podem ser vendidas à parte.";
            return RedirectToAction(nameof(Photos), new { id = photo.GalleryId });
        }

        photo.IsForSale = !photo.IsForSale;
        if (!photo.IsForSale)
            photo.Price = 0m;
        else if (photo.Price <= 0)
            photo.Price = 10m;
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Photos), new { id = photo.GalleryId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePhoto(int id)
    {
        var photo = await _db.Photos.FindAsync(id);
        if (photo is null)
            return NotFound();

        var galleryId = photo.GalleryId;
        _storage.Delete(photo.FilePath, photo.PreviewPath);
        _db.Photos.Remove(photo);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Photos), new { id = galleryId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSamples(int id)
    {
        var samples = await _db.Photos
            .Where(p => p.GalleryId == id && p.FilePath.StartsWith("/sample-photos/"))
            .ToListAsync();
        _db.Photos.RemoveRange(samples);
        await _db.SaveChangesAsync();
        if (samples.Count > 0)
            TempData["Success"] = $"Removidas {samples.Count} foto(s) de exemplo.";
        return RedirectToAction(nameof(Photos), new { id });
    }
}
