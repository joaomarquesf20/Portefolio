using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminSessionTypesController : Controller
{
    private readonly ApplicationDbContext _db;

    public AdminSessionTypesController(ApplicationDbContext db) => _db = db;

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
    public async Task<IActionResult> Create(SessionType sessionType)
    {
        Validate(sessionType);

        if (ModelState.IsValid)
        {
            sessionType.DurationMinutes = 120; // duration is fixed internally (availability slots)
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
    public async Task<IActionResult> Edit(int id, SessionType sessionType)
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
            existing.SortOrder = sessionType.SortOrder;
            existing.IsActive = sessionType.IsActive;
            existing.ImagePath = sessionType.ImagePath;

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(sessionType);
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
}
