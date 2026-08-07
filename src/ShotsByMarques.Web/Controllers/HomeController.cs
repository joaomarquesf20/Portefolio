using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;

namespace ShotsByMarques.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;

    public HomeController(ApplicationDbContext db) => _db = db;

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

        return View(new ShotsByMarques.Web.ViewModels.PortfolioViewModel
        {
            Sessions = sessions,
            Events = events
        });
    }

    [Route("servicos")]
    public IActionResult Servicos() => View();

}
