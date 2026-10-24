using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Services;
using ShotsByMarques.Web.ViewModels;

namespace ShotsByMarques.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminSiteImagesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SitePageBackgroundService _backgrounds;

    private sealed record BackgroundDefinition(
        string Label,
        string Description,
        string Section,
        string? PageUrl);

    private static readonly IReadOnlyDictionary<string, BackgroundDefinition> Definitions =
        new Dictionary<string, BackgroundDefinition>(StringComparer.Ordinal)
        {
            [SitePageBackgroundService.HomeHero] = new("Capa principal", "A imagem de entrada da página inicial.", "Página inicial", "/"),
            [SitePageBackgroundService.HomePortfolio] = new("Bloco Portefólio", "Fundo da secção de portefólio na página inicial.", "Página inicial", "/#portfolio"),
            [SitePageBackgroundService.HomeServices] = new("Bloco Serviços", "Fundo da secção de serviços na página inicial.", "Página inicial", "/#servicos"),
            [SitePageBackgroundService.HomeContact] = new("Bloco Contacto", "Fundo da secção de contacto na página inicial.", "Página inicial", "/#contacto"),
            [SitePageBackgroundService.Portfolio] = new("Portefólio", "Fundo usado na página com todas as galerias.", "Páginas principais", "/portfolio"),
            [SitePageBackgroundService.Sessions] = new("Sessões", "Fundo usado na página de marcação de sessões.", "Páginas principais", "/sessoes"),
            [SitePageBackgroundService.Services] = new("Serviços", "Fundo usado na página de serviços.", "Páginas principais", "/servicos")
        };

    public AdminSiteImagesController(ApplicationDbContext db, SitePageBackgroundService backgrounds)
    {
        _db = db;
        _backgrounds = backgrounds;
    }

    public async Task<IActionResult> Index()
    {
        var configured = await _db.SitePageSettings
            .AsNoTracking()
            .ToDictionaryAsync(s => s.PageKey, s => s.ImagePath);
        var galleryCovers = await _db.Galleries
            .AsNoTracking()
            .Where(g => g.IsPublished && !g.IsPrivate && g.CoverPath != null)
            .OrderByDescending(g => g.EventDate)
            .Select(g => g.CoverPath!)
            .Take(6)
            .ToListAsync();
        var fallback = galleryCovers.FirstOrDefault() ?? "/sample-photos/cars-cover.svg";

        var items = SitePageBackgroundService.Keys.Select((key, index) =>
        {
            var definition = Definitions[key];
            var customPath = configured.GetValueOrDefault(key);

            return new SitePageBackgroundItemViewModel
            {
                Key = key,
                Label = definition.Label,
                Description = definition.Description,
                Section = definition.Section,
                PageUrl = definition.PageUrl,
                ImagePath = customPath
                    ?? (galleryCovers.Count > index ? galleryCovers[index] : fallback),
                IsCustom = !string.IsNullOrWhiteSpace(customPath)
            };
        }).ToList();

        return View(new SitePageBackgroundIndexViewModel { Items = items });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(string key, IFormFile? image)
    {
        if (!SitePageBackgroundService.Keys.Contains(key, StringComparer.Ordinal))
            return BadRequest();

        if (image is null || image.Length == 0)
        {
            TempData["Error"] = "Escolhe uma imagem antes de guardar.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _backgrounds.SaveAsync(key, image);
            TempData["Success"] = "Imagem de fundo atualizada.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
