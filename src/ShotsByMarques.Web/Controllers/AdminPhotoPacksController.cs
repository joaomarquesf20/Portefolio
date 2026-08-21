using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminPhotoPacksController : Controller
{
    private readonly ApplicationDbContext _db;

    public AdminPhotoPacksController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var packs = await _db.PhotoPacks
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.MinPhotos)
            .ToListAsync();
        return View(packs);
    }

    public IActionResult Create() => View(new PhotoPack { SortOrder = 10, MinPhotos = 5, DiscountPercent = 10 });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PhotoPack pack)
    {
        Validate(pack);
        if (ModelState.IsValid)
        {
            _db.PhotoPacks.Add(pack);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(pack);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var pack = await _db.PhotoPacks.FindAsync(id);
        return pack is null ? NotFound() : View(pack);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PhotoPack pack)
    {
        if (id != pack.Id)
            return NotFound();

        Validate(pack);
        if (!ModelState.IsValid)
            return View(pack);

        var existing = await _db.PhotoPacks.FindAsync(id);
        if (existing is null)
            return NotFound();

        existing.Name = pack.Name;
        existing.MinPhotos = pack.MinPhotos;
        existing.DiscountPercent = pack.DiscountPercent;
        existing.IsActive = pack.IsActive;
        existing.SortOrder = pack.SortOrder;

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var pack = await _db.PhotoPacks.FindAsync(id);
        if (pack is not null)
        {
            pack.IsActive = !pack.IsActive;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var pack = await _db.PhotoPacks.FindAsync(id);
        if (pack is not null)
        {
            _db.PhotoPacks.Remove(pack);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkDelete(string ids)
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

        var packs = await _db.PhotoPacks.Where(p => idList.Contains(p.Id)).ToListAsync();
        _db.PhotoPacks.RemoveRange(packs);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{packs.Count} pack(s) eliminado(s).";
        return RedirectToAction(nameof(Index));
    }

    private void Validate(PhotoPack pack)
    {
        if (string.IsNullOrWhiteSpace(pack.Name))
            ModelState.AddModelError(nameof(pack.Name), "O nome é obrigatório.");
        if (pack.MinPhotos < 2)
            ModelState.AddModelError(nameof(pack.MinPhotos), "O mínimo é 2 fotos.");
        if (pack.DiscountPercent <= 0 || pack.DiscountPercent > 90)
            ModelState.AddModelError(nameof(pack.DiscountPercent), "O desconto deve estar entre 1% e 90%.");
    }
}
