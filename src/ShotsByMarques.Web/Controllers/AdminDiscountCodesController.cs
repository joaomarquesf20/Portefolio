using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;
using ShotsByMarques.Web.Services;

namespace ShotsByMarques.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminDiscountCodesController : Controller
{
    private readonly ApplicationDbContext _db;

    public AdminDiscountCodesController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var codes = await _db.DiscountCodes
            .OrderByDescending(c => c.IsActive)
            .ThenBy(c => c.Code)
            .ToListAsync();
        return View(codes);
    }

    public IActionResult Create() => View(new DiscountCode());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DiscountCode model)
    {
        Normalize(model);
        ValidateCode(model);
        if (!ModelState.IsValid)
            return View(model);

        if (await CodeTakenAsync(model.Code))
        {
            ModelState.AddModelError(nameof(model.Code), "Já existe um código com esse nome.");
            return View(model);
        }

        _db.DiscountCodes.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Código {model.Code} criado.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var code = await _db.DiscountCodes.FindAsync(id);
        return code is null ? NotFound() : View(code);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, DiscountCode model)
    {
        if (id != model.Id)
            return NotFound();

        Normalize(model);
        ValidateCode(model);

        var existing = await _db.DiscountCodes.FindAsync(id);
        if (existing is null)
            return NotFound();

        if (!ModelState.IsValid)
            return View(model);

        if (await CodeTakenAsync(model.Code, excludeId: id))
        {
            ModelState.AddModelError(nameof(model.Code), "Já existe um código com esse nome.");
            return View(model);
        }

        existing.Code = model.Code;
        existing.DiscountType = model.DiscountType;
        existing.Value = model.Value;
        existing.IsActive = model.IsActive;
        existing.ExpiryDate = model.ExpiryDate;

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Código {existing.Code} guardado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var code = await _db.DiscountCodes.FindAsync(id);
        if (code is not null)
        {
            code.IsActive = !code.IsActive;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var code = await _db.DiscountCodes.FindAsync(id);
        if (code is not null)
        {
            _db.DiscountCodes.Remove(code);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Código {code.Code} eliminado.";
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Codes are stored uppercase so the case-sensitive SQLite unique index stays consistent with the checkout lookup.</summary>
    private static void Normalize(DiscountCode code) =>
        code.Code = (code.Code ?? string.Empty).Trim().ToUpperInvariant();

    private void ValidateCode(DiscountCode code)
    {
        if (string.IsNullOrWhiteSpace(code.Code))
            ModelState.AddModelError(nameof(code.Code), "Escreve um código promocional.");

        if (code.DiscountType == DiscountCodeType.Percentage && (code.Value <= 0 || code.Value > 90))
            ModelState.AddModelError(nameof(code.Value), "A percentagem tem de estar entre 1% e 90%.");

        if (code.DiscountType == DiscountCodeType.FixedAmount && code.Value <= 0)
            ModelState.AddModelError(nameof(code.Value), "O valor do desconto tem de ser maior que zero.");

        if (code.ExpiryDate.HasValue && code.ExpiryDate.Value.Date < PortugalTime.Today)
            ModelState.AddModelError(nameof(code.ExpiryDate), "A data de validade tem de ser hoje ou no futuro.");
    }

    private async Task<bool> CodeTakenAsync(string normalizedCode, int? excludeId = null) =>
        await _db.DiscountCodes.AnyAsync(c => c.Code == normalizedCode && (excludeId == null || c.Id != excludeId));
}
