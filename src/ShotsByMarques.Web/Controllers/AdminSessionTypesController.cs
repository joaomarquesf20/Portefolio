using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;
using ShotsByMarques.Web.Services;

namespace ShotsByMarques.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminSessionTypesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PhotoStorageService _storage;
    private readonly IWebHostEnvironment _env;

    public AdminSessionTypesController(ApplicationDbContext db, PhotoStorageService storage, IWebHostEnvironment env)
    {
        _db = db;
        _storage = storage;
        _env = env;
    }

    public async Task<IActionResult> Index()
    {
        var sessionTypes = await _db.SessionTypes
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync();
        return View(sessionTypes);
    }

    public IActionResult Create() => View(new SessionType { SortOrder = 10 });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SessionType sessionType, IFormFile? cover)
    {
        Validate(sessionType);

        if (ModelState.IsValid)
        {
            if (cover is not null && cover.Length > 0)
                sessionType.ImagePath = await _storage.SaveCoverAsync(cover);
            sessionType.DurationMinutes = 120; // duration is fixed internally (availability slots)
            // New session types go to the end of the list — the admin reorders
            // them with the ↑/↓ buttons (the form no longer sends a SortOrder).
            var maxOrder = await _db.SessionTypes.MaxAsync(s => (int?)s.SortOrder) ?? 0;
            sessionType.SortOrder = maxOrder + 10;
            _db.SessionTypes.Add(sessionType);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(sessionType);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var sessionType = await _db.SessionTypes.FindAsync(id);
        return sessionType is null ? NotFound() : View(sessionType);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SessionType sessionType, IFormFile? cover)
    {
        if (id != sessionType.Id)
            return NotFound();

        Validate(sessionType);

        if (ModelState.IsValid)
        {
            // Update only the editable fields explicitly — avoids a full Update()
            // which would mark every column (e.g. ImagePath) as modified.
            var existing = await _db.SessionTypes.FindAsync(id);
            if (existing is null)
                return NotFound();

            existing.Name = sessionType.Name;
            existing.Description = sessionType.Description;
            existing.Price = sessionType.Price;
            existing.IsActive = sessionType.IsActive;
            // SortOrder is managed only by the ↑/↓ buttons (MoveUp/MoveDown),
            // never by this form.

            if (cover is not null && cover.Length > 0)
            {
                DeleteUploadedCover(existing.ImagePath);
                existing.ImagePath = await _storage.SaveCoverAsync(cover);
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(sessionType);
    }

    /// <summary>Deletes an uploaded cover (not the seeded sample SVGs).</summary>
    private void DeleteUploadedCover(string? path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith("/uploads/covers/"))
            return;
        var fullPath = System.IO.Path.Combine(_env.WebRootPath, path.TrimStart('/').Replace('/', System.IO.Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);
    }

    private void Validate(SessionType sessionType)
    {
        if (string.IsNullOrWhiteSpace(sessionType.Name))
            ModelState.AddModelError(nameof(sessionType.Name), "O nome é obrigatório.");
        if (sessionType.Price < 0)
            ModelState.AddModelError(nameof(sessionType.Price), "O preço não pode ser negativo.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveUp(int id)
    {
        var current = await _db.SessionTypes.FindAsync(id);
        if (current is null)
            return RedirectToAction(nameof(Index));

        var previous = await _db.SessionTypes
            .Where(s => s.SortOrder < current.SortOrder || (s.SortOrder == current.SortOrder && s.Id < current.Id))
            .OrderByDescending(s => s.SortOrder)
            .ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync();
        if (previous is not null)
        {
            (current.SortOrder, previous.SortOrder) = (previous.SortOrder, current.SortOrder);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveDown(int id)
    {
        var current = await _db.SessionTypes.FindAsync(id);
        if (current is null)
            return RedirectToAction(nameof(Index));

        var next = await _db.SessionTypes
            .Where(s => s.SortOrder > current.SortOrder || (s.SortOrder == current.SortOrder && s.Id > current.Id))
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Id)
            .FirstOrDefaultAsync();
        if (next is not null)
        {
            (current.SortOrder, next.SortOrder) = (next.SortOrder, current.SortOrder);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var sessionType = await _db.SessionTypes.FindAsync(id);
        if (sessionType is not null)
        {
            sessionType.IsActive = !sessionType.IsActive;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var sessionType = await _db.SessionTypes.FindAsync(id);
        if (sessionType is not null)
        {
            // The mini-session type is infrastructure for event-day bookings —
            // it must always exist (the seeder recreates it if missing).
            if (sessionType.IsMiniSessionType)
            {
                TempData["Error"] = "O tipo \"Mini Sessão — Evento\" é usado pelas mini sessões dos eventos e não pode ser eliminado. Podes editar o preço/duração.";
                return RedirectToAction(nameof(Index));
            }

            var referenced = await _db.Bookings.AnyAsync(b => b.SessionTypeId == id);
            if (referenced)
            {
                sessionType.IsActive = false;
                await _db.SaveChangesAsync();
                TempData["Error"] = "Este tipo de sessão tem marcações associadas — foi desativado em vez de eliminado.";
            }
            else
            {
                _db.SessionTypes.Remove(sessionType);
                await _db.SaveChangesAsync();
            }
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

        var types = await _db.SessionTypes
            .Where(s => idList.Contains(s.Id))
            .ToListAsync();
        var referenced = await _db.Bookings
            .Where(b => idList.Contains(b.SessionTypeId))
            .Select(b => b.SessionTypeId)
            .Distinct()
            .ToListAsync();

        var deleted = 0;
        var deactivated = 0;
        foreach (var sessionType in types)
        {
            // The mini-session type is infrastructure for event-day bookings.
            if (sessionType.IsMiniSessionType)
                continue;

            if (referenced.Contains(sessionType.Id))
            {
                sessionType.IsActive = false;
                deactivated++;
            }
            else
            {
                _db.SessionTypes.Remove(sessionType);
                deleted++;
            }
        }
        await _db.SaveChangesAsync();

        var msg = $"{deleted} tipo(s) eliminado(s).";
        if (deactivated > 0)
            msg += $" {deactivated} tinham marcações associadas e foram desativados.";
        TempData["Success"] = msg;
        return RedirectToAction(nameof(Index));
    }
}
