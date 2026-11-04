using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Helpers;
using ShotsByMarques.Web.Models;
using ShotsByMarques.Web.Services;

namespace ShotsByMarques.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SitePageBackgroundService _backgrounds;
    private readonly IEmailService _emailService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ApplicationDbContext db, SitePageBackgroundService backgrounds,
        IEmailService emailService, ILogger<HomeController> logger)
    {
        _db = db;
        _backgrounds = backgrounds;
        _emailService = emailService;
        _logger = logger;
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitContact(string Name, string Email, string Subject, string Message)
    {
        var contact = new ContactMessage
        {
            Name = Name?.Trim() ?? string.Empty,
            Email = Email?.Trim() ?? string.Empty,
            Subject = Subject?.Trim() ?? string.Empty,
            Message = Message?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };
        _db.ContactMessages.Add(contact);
        await _db.SaveChangesAsync();

        // Notification failures must not affect the saved message or the user's
        // success message, so they are logged and swallowed here.
        try
        {
            var subject = $"[ShotsByMarques Contact] {contact.Subject} - {contact.Name}";
            var body = $"<p><strong>Nome:</strong> {System.Net.WebUtility.HtmlEncode(contact.Name)}</p>" +
                       $"<p><strong>Email:</strong> {System.Net.WebUtility.HtmlEncode(contact.Email)}</p>" +
                       $"<p><strong>Assunto:</strong> {System.Net.WebUtility.HtmlEncode(contact.Subject)}</p>" +
                       $"<p><strong>Mensagem:</strong></p><p>{System.Net.WebUtility.HtmlEncode(contact.Message).Replace("\n", "<br/>")}</p>";
            await _emailService.SendAsync(subject, body, contact.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar a notificação de contacto (ContactMessage {ContactMessageId}).", contact.Id);
        }

        TempData["SuccessMessage"] = "Obrigado! A sua mensagem foi enviada.";
        return RedirectToAction(nameof(Index));
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
