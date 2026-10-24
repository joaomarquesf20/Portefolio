using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Helpers;
using ShotsByMarques.Web.Services;

namespace ShotsByMarques.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SitePageBackgroundService _backgrounds;

    public HomeController(ApplicationDbContext db, SitePageBackgroundService backgrounds)
    {
        _db = db;
        _backgrounds = backgrounds;
    }

    public async Task<IActionResult> Index()
    {
        var published = _db.Galleries.Where(g => g.IsPublished && !g.IsPrivate);

        var sessions = await published
            .Where(g => g.IsSession)
            .Include(g => g.Photos)
            .OrderByDescending(g => g.EventDate)
            .Take(6)
            .ToListAsync();

        var events = await published
            .Where(g => !g.IsSession)
            .Include(g => g.Photos)
            .OrderByDescending(g => g.EventDate)
            .Take(6)
            .ToListAsync();

        var covers = sessions.Concat(events)
            .Select(g => g.CoverUrl())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();
        var fallback = covers.FirstOrDefault() ?? "/sample-photos/cars-cover.svg";
        string Pick(int index) => covers.Count > index ? covers[index] : fallback;

        ViewData["BgHomeHero"] = await _backgrounds.GetAsync(SitePageBackgroundService.HomeHero, Pick(0));
        ViewData["BgHomePortfolio"] = await _backgrounds.GetAsync(SitePageBackgroundService.HomePortfolio, Pick(1));
        ViewData["BgHomeServices"] = await _backgrounds.GetAsync(SitePageBackgroundService.HomeServices, Pick(2));
        ViewData["BgHomeContact"] = await _backgrounds.GetAsync(SitePageBackgroundService.HomeContact, Pick(3));

        return View(new ShotsByMarques.Web.ViewModels.PortfolioViewModel
        {
            Sessions = sessions,
            Events = events
        });
    }

    [Route("servicos")]
    public async Task<IActionResult> Servicos()
    {
        var fallback = await _db.Galleries
            .Where(g => g.IsPublished && !g.IsPrivate && g.CoverPath != null)
            .OrderByDescending(g => g.EventDate)
            .Select(g => g.CoverPath!)
            .FirstOrDefaultAsync() ?? "/sample-photos/cars-cover.svg";

        ViewData["BgServices"] = await _backgrounds.GetAsync(SitePageBackgroundService.Services, fallback);
        return View();
    }

}
