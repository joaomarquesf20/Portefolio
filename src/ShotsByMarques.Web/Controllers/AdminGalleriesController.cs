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
    /// <summary>Default per-photo price for event galleries (€).</summary>
    private const decimal DefaultPhotoPrice = 3m;

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

    public async Task<IActionResult> Index(string? filter)
    {
        var query = _db.Galleries
            .Include(g => g.Photos)
            .Include(g => g.Booking).ThenInclude(b => b!.ClientUser)
            .AsQueryable();

        if (filter == "public")
            query = query.Where(g => !g.IsPrivate && g.IsPublished);
        else if (filter == "private")
            query = query.Where(g => g.IsPrivate);
        else if (filter == "hidden")
            query = query.Where(g => !g.IsPrivate && !g.IsPublished);

        ViewData["Filter"] = filter;
        return View(await query.OrderByDescending(g => g.CreatedAt).ToListAsync());
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
            TempData["Success"] = "Galeria criada. Carrega agora as fotos — depois escolhe qualquer uma como capa.";
            // Go straight into the photo manager: upload folder → set cover → done.
            return RedirectToAction(nameof(Photos), new { id = gallery.Id });
        }
        return View(gallery);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var gallery = await _db.Galleries
            .Include(g => g.Photos.OrderBy(p => p.SortOrder))
            .FirstOrDefaultAsync(g => g.Id == id);
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

        // Session galleries are priced as a package, never per photo: the photos
        // must NEVER be sold separately, even if they were uploaded with a price
        // before the gallery became a session (e.g. an event turned into a
        // session delivery). Clear the price of every photo.
        if (gallery.IsSession)
        {
            var photos = await _db.Photos.Where(p => p.GalleryId == gallery.Id).ToListAsync();
            foreach (var photo in photos)
            {
                photo.IsForSale = false;
                photo.Price = 0m;
            }
        }

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
    public async Task<IActionResult> MakePrivate(int id)
    {
        var gallery = await _db.Galleries.FindAsync(id);
        if (gallery is not null)
        {
            // "Privar" esconde a galeria do público sem a apagar: despublica-a.
            gallery.IsPublished = false;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkAction(string ids, string action)
    {
        // The view sends the selected ids as a comma-separated string ("1,2,3").
        var idList = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (idList.Count == 0)
            return RedirectToAction(nameof(Index));

        var galleries = await _db.Galleries.Include(g => g.Photos)
            .Where(g => idList.Contains(g.Id))
            .ToListAsync();

        if (action == "private")
        {
            // Esconder do público sem apagar nada.
            foreach (var gallery in galleries)
                gallery.IsPublished = false;
            await _db.SaveChangesAsync();
        }
        else if (action == "delete")
        {
            foreach (var gallery in galleries)
            {
                foreach (var photo in gallery.Photos)
                    _storage.Delete(photo.FilePath, photo.PreviewPath);
                _db.Galleries.Remove(gallery);
            }
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var gallery = await _db.Galleries
            .Include(g => g.Photos)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (gallery is not null)
        {
            foreach (var photo in gallery.Photos)
                _storage.Delete(photo.FilePath, photo.PreviewPath);
            _db.Galleries.Remove(gallery);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Galeria eliminada.";
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Photos(int id)
    {
        var gallery = await _db.Galleries
            .Include(g => g.Booking).ThenInclude(b => b!.ClientUser)
            .Include(g => g.Photos.OrderBy(p => p.SortOrder))
            .FirstOrDefaultAsync(g => g.Id == id);
        if (gallery is null)
            return NotFound();
        ViewData["Bookings"] = await BookingsAsync();
        return View(gallery);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Large multi-file uploads (Render/Linux): restate the 2 GB body + multipart
    // limits at the endpoint so no hosting-level default can shrink them, and
    // allow hundreds of files in one request.
    [RequestSizeLimit(2L * 1024 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2L * 1024 * 1024 * 1024, ValueCountLimit = 4096)]
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
        else if (forSale && price <= 0)
            price = DefaultPhotoPrice;

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

    /// <summary>
    /// Applies a price to ALL photos of an event gallery at once — useful when
    /// photos were uploaded while the gallery was still a session (free) and the
    /// admin then switches it to an event and wants to sell them.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyPriceToAll(int id, decimal price, bool forSale = true)
    {
        var gallery = await _db.Galleries.FindAsync(id);
        if (gallery is null)
            return NotFound();

        // Session galleries (or galleries linked to a booking) keep photos free.
        if (gallery.IsSession || gallery.BookingId.HasValue)
        {
            TempData["Error"] = "As fotos de uma galeria de sessão estão incluídas no preço — não podem ser vendidas à parte.";
            return RedirectToAction(nameof(Photos), new { id });
        }

        if (forSale && price <= 0)
            price = DefaultPhotoPrice;

        var photos = await _db.Photos.Where(p => p.GalleryId == id).ToListAsync();
        foreach (var photo in photos)
        {
            photo.IsForSale = forSale;
            photo.Price = forSale ? price : 0m;
        }
        await _db.SaveChangesAsync();

        TempData["Success"] = forSale
            ? $"Preço de {price.ToString("C", new System.Globalization.CultureInfo("pt-PT"))} aplicado a {photos.Count} foto(s)."
            : $"{photos.Count} foto(s) marcadas como incluídas (sem custo).";
        return RedirectToAction(nameof(Photos), new { id });
    }

    /// <summary>
    /// Applies a price to only the SELECTED photos of an event gallery — the
    /// admin picks a few photos (checkboxes) and sets their price in one go.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyPriceToSelected(string ids, decimal price, bool forSale = true)
    {
        var idList = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        if (idList.Count == 0)
            return RedirectToAction(nameof(Index));

        var photos = await _db.Photos.Include(p => p.Gallery)
            .Where(p => idList.Contains(p.Id))
            .ToListAsync();
        if (photos.Count == 0)
            return RedirectToAction(nameof(Index));

        var galleryId = photos[0].GalleryId;
        var gallery = photos[0].Gallery!;
        if (gallery.IsSession || gallery.BookingId.HasValue)
        {
            TempData["Error"] = "As fotos de uma galeria de sessão estão incluídas no preço — não podem ser vendidas à parte.";
            return RedirectToAction(nameof(Photos), new { id = galleryId });
        }

        if (forSale && price <= 0)
            price = DefaultPhotoPrice;

        foreach (var photo in photos)
        {
            photo.IsForSale = forSale;
            photo.Price = forSale ? price : 0m;
        }
        await _db.SaveChangesAsync();

        TempData["Success"] = forSale
            ? $"Preço de {price.ToString("C", new System.Globalization.CultureInfo("pt-PT"))} aplicado a {photos.Count} foto(s) selecionada(s)."
            : $"{photos.Count} foto(s) selecionada(s) marcadas como incluídas (sem custo).";
        return RedirectToAction(nameof(Photos), new { id = galleryId });
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
            photo.Price = DefaultPhotoPrice;
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

    /// <summary>
    /// Deletes several photos at once (multi-select checkboxes in the photo
    /// manager). Works for event and session galleries alike.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSelected(string ids)
    {
        var idList = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (idList.Count == 0)
            return RedirectToAction(nameof(Index));

        var photos = await _db.Photos
            .Where(p => idList.Contains(p.Id))
            .ToListAsync();
        if (photos.Count == 0)
            return RedirectToAction(nameof(Index));

        var galleryId = photos[0].GalleryId;
        foreach (var photo in photos)
            _storage.Delete(photo.FilePath, photo.PreviewPath);

        _db.Photos.RemoveRange(photos);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{photos.Count} foto(s) eliminada(s).";
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

    /// <summary>
    /// Sets one of the gallery's uploaded photos as the gallery cover — no
    /// separate cover file needed. Uses the photo's WATERMARKED preview path,
    /// so the cover is public-safe and works on every page that renders it.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCover(int id, string? from)
    {
        var photo = await _db.Photos.Include(p => p.Gallery).FirstOrDefaultAsync(p => p.Id == id);
        if (photo?.Gallery is null)
            return NotFound();

        var gallery = photo.Gallery;
        var oldCover = gallery.CoverPath;
        gallery.CoverPath = photo.PreviewPath;
        await _db.SaveChangesAsync();

        // Keep wwwroot tidy: if the previous cover was an uploaded file, remove it.
        if (oldCover is not null && oldCover != gallery.CoverPath)
            _storage.DeletePublicCover(oldCover);

        TempData["Success"] = $"Capa atualizada — \"{photo.Title}\" é a nova capa da galeria.";

        if (string.Equals(from, "edit", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction(nameof(Edit), new { id = gallery.Id });
        return RedirectToAction(nameof(Photos), new { id = gallery.Id });
    }
}
