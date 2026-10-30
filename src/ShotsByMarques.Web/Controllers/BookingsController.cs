using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;
using ShotsByMarques.Web.Services;
using ShotsByMarques.Web.ViewModels;

namespace ShotsByMarques.Web.Controllers;

public class BookingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly BookingService _bookingService;
    private readonly StripeCheckoutService _stripe;
    private readonly TravelSettings _travel;
    private readonly AvailabilityCalendarService _calendar;
    private readonly SitePageBackgroundService _backgrounds;
    private readonly BookingCalendarService _bookingCalendar;

    public BookingsController(ApplicationDbContext db, BookingService bookingService,
        StripeCheckoutService stripe, IOptions<TravelSettings> travel, AvailabilityCalendarService calendar,
        SitePageBackgroundService backgrounds, BookingCalendarService bookingCalendar)
    {
        _db = db;
        _bookingService = bookingService;
        _stripe = stripe;
        _travel = travel.Value;
        _calendar = calendar;
        _backgrounds = backgrounds;
        _bookingCalendar = bookingCalendar;
    }

    [Route("sessoes")]
    public async Task<IActionResult> Index()
    {
        // All visible session types — mini types are part of the public grid
        // (and the "Tipologias" counter). Their tiles link to the mini
        // event-day flow, since /sessoes/marcar/{id} is reserved for normal
        // sessions (the Create action returns NotFound for mini types).
        var sessionTypes = await _db.SessionTypes
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

        // Event days with mini sessions (from the admin availability calendar).
        var today = PortugalTime.Today;
        var eventDays = await _db.DayAvailability
            .Where(d => d.Status == DayStatus.Event && d.Date >= today)
            .OrderBy(d => d.Date)
            .ToListAsync();

        var booked = new Dictionary<DateTime, int>();
        if (eventDays.Count > 0)
        {
            var eventStartUtc = PortugalTime.ToUtc(eventDays.First().Date);
            var eventEndUtc = PortugalTime.ToUtc(eventDays.Last().Date.AddDays(1));
            var miniBookings = await _db.Bookings
                .Include(b => b.SessionType)
                .Where(b => b.SessionType!.IsMiniSessionType
                            && b.Status != BookingStatus.Cancelled
                            && b.Start >= eventStartUtc
                            && b.Start < eventEndUtc)
                .Select(b => b.Start)
                .ToListAsync();
            booked = miniBookings
                .GroupBy(start => PortugalTime.FromUtc(start).Date)
                .ToDictionary(group => group.Key, group => group.Count());
        }

        var miniType = await _db.SessionTypes.FirstOrDefaultAsync(s => s.IsMiniSessionType);
        var miniDays = eventDays.Select(e => new MiniSessionDayViewModel
        {
            Date = e.Date,
            Title = string.IsNullOrWhiteSpace(e.Title) ? "Mini sessões em evento" : e.Title,
            Zone = e.Zone,
            Location = e.Location,
            MaxMiniSessions = e.MaxMiniSessions ?? 4,
            Booked = booked.GetValueOrDefault(e.Date),
            Price = miniType?.Price ?? 0m
        }).ToList();

        var fallback = await _db.Galleries
            .Where(g => g.IsPublished && !g.IsPrivate && g.CoverPath != null)
            .OrderByDescending(g => g.EventDate)
            .Select(g => g.CoverPath!)
            .FirstOrDefaultAsync() ?? "/sample-photos/cars-cover.svg";
        ViewData["BgSessions"] = await _backgrounds.GetAsync(SitePageBackgroundService.Sessions, fallback);

        return View(new BookingsIndexViewModel { SessionTypes = sessionTypes, MiniDays = miniDays });
    }

    [Route("sessoes/marcar/{id:int}")]
    public async Task<IActionResult> Create(int id, int? year, int? month)
    {
        var sessionType = await _db.SessionTypes.FindAsync(id);
        if (sessionType is null || !sessionType.IsActive || sessionType.IsMiniSessionType)
            return NotFound();

        var today = PortugalTime.Today;
        var y = Math.Clamp(year ?? today.Year, 2020, 2100);
        var m = Math.Clamp(month ?? today.Month, 1, 12);

        var vm = new BookingCreateViewModel
        {
            SessionTypeId = sessionType.Id,
            SessionType = sessionType,
            Date = default,
            Year = y,
            Month = m,
            Travel = _travel,
            Cities = PortugalCities.All
                .Select(c => new CityOption { Name = c.Name, Region = c.Region, Lat = c.Lat, Lon = c.Lon })
                .ToList(),
            Regions = PortugalCities.All.Select(c => c.Region).Distinct().ToList(),
            TotalPrice = sessionType.Price,
            DepositAmount = Math.Round(sessionType.Price / 2, 2, MidpointRounding.AwayFromZero),
            BalanceAmount = sessionType.Price - Math.Round(sessionType.Price / 2, 2, MidpointRounding.AwayFromZero),
            MiniPrice = (await _db.SessionTypes.FirstOrDefaultAsync(s => s.IsMiniSessionType))?.Price ?? 0m
        };
        vm.Days = await _calendar.BuildMonthDaysAsync(_db, y, m);
        return View(vm);
    }

    [HttpPost]
    [Route("sessoes/marcar/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int id, BookingCreateViewModel model)
    {
        var sessionType = await _db.SessionTypes.FindAsync(id);
        if (sessionType is null || !sessionType.IsActive || sessionType.IsMiniSessionType)
            return NotFound();

        model.SessionType = sessionType;
        model.Travel = _travel;
        model.Cities = PortugalCities.All
            .Select(c => new CityOption { Name = c.Name, Region = c.Region, Lat = c.Lat, Lon = c.Lon })
            .ToList();
        model.Regions = PortugalCities.All.Select(c => c.Region).Distinct().ToList();
        model.MiniPrice = (await _db.SessionTypes.FirstOrDefaultAsync(s => s.IsMiniSessionType))?.Price ?? 0m;

        // Date is selected from the calendar and is posted as a hidden value.
        // Reject an empty/default value before accessing a calendar day.
        if (model.Date == default || model.Date.Date < PortugalTime.Today)
        {
            ModelState.AddModelError(nameof(model.Date), "Escolhe um dia futuro no calendário.");
            var fallbackDate = model.Date == default ? PortugalTime.Today : model.Date.Date;
            model.Year = fallbackDate.Year;
            model.Month = fallbackDate.Month;
            model.Days = await _calendar.BuildMonthDaysAsync(_db, model.Year, model.Month);
            model.TotalPrice = sessionType.Price;
            model.DepositAmount = Math.Round(model.TotalPrice / 2, 2, MidpointRounding.AwayFromZero);
            model.BalanceAmount = model.TotalPrice - model.DepositAmount;
            return View(model);
        }

        model.Year = model.Date.Year;
        model.Month = model.Date.Month;
        model.Days = await _calendar.BuildMonthDaysAsync(_db, model.Year, model.Month);

        var dayOverride = await _db.DayAvailability.FirstOrDefaultAsync(d => d.Date == model.Date.Date);

        // Event days are reserved only through the dedicated mini-session flow. They
        // must never be converted into a regular session by a crafted POST.
        if (dayOverride?.Status == DayStatus.Event)
        {
            ModelState.AddModelError(nameof(model.Date), "Este é um dia de evento. Reserva a mini sessão na área própria.");
            return View(model);
        }

        if (!TimeSpan.TryParse(model.TimeSlot, out var time))
            ModelState.AddModelError(nameof(model.TimeSlot), "Escolhe um horário válido.");

        // Store the booking in UTC (slot chosen in local wall-clock time)
        var startLocal = model.Date.Date.Add(time);
        var start = PortugalTime.ToUtc(startLocal);

        if (ModelState.IsValid && !await _bookingService.IsSlotAvailableAsync(id, startLocal))
            ModelState.AddModelError(nameof(model.TimeSlot), "Esse horário já não está disponível. Escolhe outro.");

        // Travel: estimate the distance from the base and the surcharge beyond the free radius.
        var match = PortugalCities.TryFind(model.Location);
        model.LocationRecognized = match is not null;
        // Keep the zone the client picked even if the locality is unrecognized/empty.
        model.SelectedRegion = match?.Region ?? model.SelectedRegion;
        if (match is not null)
        {
            var km = PortugalCities.DistanceKm(_travel.BaseLat, _travel.BaseLon, match.Lat, match.Lon);
            model.DistanceKm = Math.Round(km);
            model.TravelFee = model.DistanceKm > _travel.FreeKm
                ? Math.Round((decimal)(model.DistanceKm - _travel.FreeKm) * _travel.PricePerKm, 2)
                : 0m;
        }

        // The day's fixed zone wins: if the photographer said they'll be in zone X
        // that day, the client cannot book a locality outside it (server-enforced).
        var dayZone = (await _db.DayAvailability.FirstOrDefaultAsync(d => d.Date == model.Date.Date))?.Zone;
        if (!string.IsNullOrWhiteSpace(dayZone)
            && (match is null || !string.Equals(match.Region, dayZone, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(nameof(model.Location),
                $"Esse dia é na zona {dayZone} — a localidade tem de ser nessa zona.");
        }
        model.TotalPrice = sessionType.Price + model.TravelFee;
        model.DepositAmount = Math.Round(model.TotalPrice / 2, 2, MidpointRounding.AwayFromZero);
        model.BalanceAmount = model.TotalPrice - model.DepositAmount;

        if (!ModelState.IsValid)
            return View(model);

        if (!User.Identity!.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account",
                new { returnUrl = Url.Action("Create", "Bookings", new { id }) });
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var booking = new Booking
        {
            ClientUserId = userId,
            SessionTypeId = sessionType.Id,
            Start = start,
            Location = model.Location?.Trim(),
            Status = BookingStatus.Pending,
            Price = model.TotalPrice,
            TravelFee = model.TravelFee
        };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        // Deposit: 50% charged at booking time via Stripe Checkout.
        booking.SessionType = sessionType; // for a clear line item name in the checkout
        string? sessionId;
        try
        {
            sessionId = await _stripe.CreateBookingCheckoutSessionAsync(booking, booking.DepositAmount, "deposit");
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Marcação criada, mas não foi possível iniciar o pagamento do depósito: {ex.Message}";
            return RedirectToAction(nameof(Mine));
        }
        if (sessionId is null)
        {
            TempData["Success"] = "Marcação pedida! (O pagamento do depósito fica para quando as chaves do Stripe estiverem configuradas.)";
            return RedirectToAction(nameof(Mine));
        }

        booking.DepositSessionId = sessionId;
        await _db.SaveChangesAsync();

        var sessionUrl = await _stripe.GetSessionUrlAsync(sessionId);
        return string.IsNullOrEmpty(sessionUrl) ? RedirectToAction(nameof(Mine)) : Redirect(sessionUrl);
    }

    [HttpGet]
    public async Task<IActionResult> Available(int sessionTypeId, DateTime date)
    {
        // Event mini sessions have their own page and flow. Returning no regular
        // slots here prevents a normal session form from booking an event day.
        var day = await _db.DayAvailability.FirstOrDefaultAsync(d => d.Date == date.Date);
        if (day?.Status == DayStatus.Event)
            return Json(Array.Empty<SlotOption>());

        // Full grid with taken flags so the client can render unavailable hours
        // as disabled instead of hiding them.
        var slots = await _bookingService.GetSlotOptionsAsync(sessionTypeId, date);
        return Json(slots);
    }

    // ---- Booking payments: 50% deposit at booking, 50% balance at delivery ----

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("sessoes/pagar-deposito/{id:int}")]
    public async Task<IActionResult> PayDeposit(int id)
    {
        var booking = await OwnedBookingAsync(id);
        if (booking is null || booking.DepositPaid || booking.Status == BookingStatus.Cancelled)
            return RedirectToAction(nameof(Mine));
        return await StartBookingPaymentAsync(booking, booking.DepositAmount, "deposit");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("sessoes/pagar-saldo/{id:int}")]
    public async Task<IActionResult> PayBalance(int id)
    {
        var booking = await OwnedBookingAsync(id);
        if (booking is null || !booking.DepositPaid || booking.BalancePaid || booking.Status == BookingStatus.Cancelled)
            return RedirectToAction(nameof(Mine));
        return await StartBookingPaymentAsync(booking, booking.BalanceAmount, "balance");
    }

    [Authorize]
    [Route("sessoes/deposito-sucesso")]
    public async Task<IActionResult> DepositSuccess(string session_id)
    {
        ViewData["Title"] = "Depósito";
        var vm = new PaymentResultViewModel { State = "unpaid", IsBalance = false };
        if (!string.IsNullOrEmpty(session_id))
        {
            var booking = await _db.Bookings
                .Include(b => b.SessionType)
                .FirstOrDefaultAsync(b => b.DepositSessionId == session_id && b.ClientUserId == UserId());
            if (booking is not null)
            {
                vm.BookingId = booking.Id;
                vm.SessionName = booking.SessionType?.Name ?? string.Empty;
                vm.Start = PortugalTime.FromUtc(booking.Start);
                vm.Location = booking.Location ?? string.Empty;
                vm.State = "paid"; // existing booking: already confirmed previously
                if (!booking.DepositPaid)
                {
                    var status = await _stripe.GetPaymentStatusAsync(session_id);
                    if (status == "paid")
                    {
                        booking.DepositPaid = true;
                        await _db.SaveChangesAsync();
                        vm.State = "paid";
                    }
                    else if (status == "processing")
                    {
                        vm.State = "processing";
                        vm.MetaRefresh = "5";
                    }
                    else
                    {
                        vm.State = "unpaid";
                    }
                }
            }
        }
        return View("PaymentResult", vm);
    }

    [Authorize]
    [Route("sessoes/saldo-sucesso")]
    public async Task<IActionResult> BalanceSuccess(string session_id)
    {
        ViewData["Title"] = "Saldo";
        var vm = new PaymentResultViewModel { State = "unpaid", IsBalance = true };
        if (!string.IsNullOrEmpty(session_id))
        {
            var booking = await _db.Bookings
                .Include(b => b.SessionType)
                .FirstOrDefaultAsync(b => b.BalanceSessionId == session_id && b.ClientUserId == UserId());
            if (booking is not null)
            {
                vm.BookingId = booking.Id;
                vm.SessionName = booking.SessionType?.Name ?? string.Empty;
                vm.Start = PortugalTime.FromUtc(booking.Start);
                vm.Location = booking.Location ?? string.Empty;
                vm.State = "paid";
                if (!booking.BalancePaid)
                {
                    var status = await _stripe.GetPaymentStatusAsync(session_id);
                    if (status == "paid")
                    {
                        booking.BalancePaid = true;
                        await _db.SaveChangesAsync();
                        vm.State = "paid";
                    }
                    else if (status == "processing")
                    {
                        vm.State = "processing";
                        vm.MetaRefresh = "5";
                    }
                    else
                    {
                        vm.State = "unpaid";
                    }
                }
            }
        }
        return View("PaymentResult", vm);
    }

    private async Task<Booking?> OwnedBookingAsync(int id)
    {
        return await _db.Bookings
            .Include(b => b.ClientUser)
            .Include(b => b.SessionType)
            .FirstOrDefaultAsync(b => b.Id == id && b.ClientUserId == UserId());
    }

    private async Task<IActionResult> StartBookingPaymentAsync(Booking booking, decimal amount, string purpose)
    {
        string? sessionId;
        try
        {
            sessionId = await _stripe.CreateBookingCheckoutSessionAsync(booking, amount, purpose);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Não foi possível iniciar o pagamento: {ex.Message}";
            return RedirectToAction(nameof(Mine));
        }
        if (sessionId is null)
        {
            TempData["Error"] = "As chaves do Stripe ainda não estão configuradas. Adiciona-as em appsettings.json (modo de teste).";
            return RedirectToAction(nameof(Mine));
        }

        if (purpose == "balance") booking.BalanceSessionId = sessionId;
        else booking.DepositSessionId = sessionId;
        await _db.SaveChangesAsync();

        var url = await _stripe.GetSessionUrlAsync(sessionId);
        return string.IsNullOrEmpty(url) ? RedirectToAction(nameof(Mine)) : Redirect(url);
    }

    // ---- Mini sessions at events ----

    [Route("sessoes/mini-sessao/{date}")]
    public async Task<IActionResult> MiniSession(DateTime date)
    {
        var day = await _db.DayAvailability.FirstOrDefaultAsync(d => d.Date == date.Date);
        if (day is null || day.Status != DayStatus.Event || date.Date < PortugalTime.Today)
            return NotFound();

        var miniType = await _db.SessionTypes.FirstOrDefaultAsync(s => s.IsMiniSessionType);
        if (miniType is null)
            return NotFound();

        var booked = await CountMiniBookedAsync(date.Date);
        return View(new MiniSessionBookingViewModel
        {
            Date = date.Date,
            EventTitle = string.IsNullOrWhiteSpace(day.Title) ? "Mini sessões em evento" : day.Title,
            Zone = day.Zone,
            Location = day.Location,
            MaxMiniSessions = day.MaxMiniSessions ?? 4,
            Booked = booked,
            Price = miniType.Price,
            HasSchedule = day.MiniStart is not null && day.MiniEnd is not null,
            Slots = await _bookingService.GetMiniSlotsAsync(date.Date),
            SlotOptions = await _bookingService.GetMiniSlotOptionsAsync(date.Date),
            DepositAmount = Math.Round(miniType.Price / 2, 2, MidpointRounding.AwayFromZero),
            BalanceAmount = miniType.Price - Math.Round(miniType.Price / 2, 2, MidpointRounding.AwayFromZero)
        });
    }

    [HttpPost]
    [Route("sessoes/mini-sessao/{date}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MiniSession(DateTime date, MiniSessionBookingViewModel model)
    {
        var day = await _db.DayAvailability.FirstOrDefaultAsync(d => d.Date == date.Date);
        if (day is null || day.Status != DayStatus.Event)
            return NotFound();

        var miniType = await _db.SessionTypes.FirstOrDefaultAsync(s => s.IsMiniSessionType);
        if (miniType is null)
            return NotFound();

        model.Date = date.Date;
        model.EventTitle = string.IsNullOrWhiteSpace(day.Title) ? "Mini sessões em evento" : day.Title;
        model.Zone = day.Zone;
        model.Location = day.Location;
        model.MaxMiniSessions = day.MaxMiniSessions ?? 4;
        model.Price = miniType.Price;
        model.Booked = await CountMiniBookedAsync(date.Date);
        model.HasSchedule = day.MiniStart is not null && day.MiniEnd is not null;
        model.Slots = await _bookingService.GetMiniSlotsAsync(date.Date);
        model.SlotOptions = await _bookingService.GetMiniSlotOptionsAsync(date.Date);
        model.DepositAmount = Math.Round(miniType.Price / 2, 2, MidpointRounding.AwayFromZero);
        model.BalanceAmount = miniType.Price - model.DepositAmount;

        if (model.Booked >= model.MaxMiniSessions)
            ModelState.AddModelError(string.Empty, "As mini sessões deste evento já estão esgotadas.");

        if (!TimeSpan.TryParse(model.TimeSlot, out var time) ||
            !model.Slots.Contains(model.TimeSlot))
            ModelState.AddModelError(nameof(model.TimeSlot), "Escolhe um horário disponível.");

        if (!User.Identity!.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account",
                new { returnUrl = Url.Action("MiniSession", "Bookings", new { date = date.Date.ToString("yyyy-MM-dd") }) });
        }

        if (!ModelState.IsValid)
            return View(model);

        // Race-guard: the slot may have been taken by someone else meanwhile.
        if (!await _bookingService.IsMiniSlotAvailableAsync(date.Date, time))
        {
            model.Slots = await _bookingService.GetMiniSlotsAsync(date.Date);
            model.SlotOptions = await _bookingService.GetMiniSlotOptionsAsync(date.Date);
            ModelState.AddModelError(nameof(model.TimeSlot), "Esse horário já não está disponível. Escolhe outro.");
            return View(model);
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var booking = new Booking
        {
            ClientUserId = userId,
            SessionTypeId = miniType.Id,
            Start = PortugalTime.ToUtc(date.Date.Add(time)),
            // Tie the mini booking to the event's location so the photographer
            // knows where it happened (e.g. "Ponte de Lima").
            Location = string.IsNullOrWhiteSpace(day.Location) ? day.Zone : day.Location,
            Status = BookingStatus.Pending,
            Price = miniType.Price
        };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        // Deposit: 50% charged at booking time via Stripe Checkout — the same rule
        // as normal sessions; the balance is paid when the photos are delivered.
        booking.SessionType = miniType; // for a clear line item name in the checkout
        string? sessionId;
        try
        {
            sessionId = await _stripe.CreateBookingCheckoutSessionAsync(booking, booking.DepositAmount, "deposit");
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Mini sessão reservada, mas não foi possível iniciar o pagamento do depósito: {ex.Message}";
            return RedirectToAction(nameof(Mine));
        }
        if (sessionId is null)
        {
            TempData["Success"] = "Mini sessão reservada! (O pagamento do depósito fica para quando as chaves do Stripe estiverem configuradas.)";
            return RedirectToAction(nameof(Mine));
        }

        booking.DepositSessionId = sessionId;
        await _db.SaveChangesAsync();

        var sessionUrl = await _stripe.GetSessionUrlAsync(sessionId);
        return string.IsNullOrEmpty(sessionUrl) ? RedirectToAction(nameof(Mine)) : Redirect(sessionUrl);
    }

    [Authorize]
    [HttpGet("sessoes/calendario/google/{id:int}")]
    public async Task<IActionResult> AddToGoogleCalendar(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.SessionType)
            .FirstOrDefaultAsync(b => b.Id == id && b.ClientUserId == UserId());
        if (booking is null)
            return NotFound();
        return Redirect(_bookingCalendar.CreateGoogleCalendarUrl(booking));
    }

    [Authorize]
    [HttpGet("sessoes/calendario/ficheiro/{id:int}")]
    public async Task<IActionResult> DownloadCalendarFile(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.SessionType)
            .FirstOrDefaultAsync(b => b.Id == id && b.ClientUserId == UserId());
        if (booking is null)
            return NotFound();
        return File(_bookingCalendar.CreateIcs(booking), "text/calendar; charset=utf-8", _bookingCalendar.CreateFileName(booking));
    }

    [Authorize]
    [Route("as-minhas-marcacoes")]
    public async Task<IActionResult> Mine()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var bookings = await _db.Bookings
            .Where(b => b.ClientUserId == userId)
            .Include(b => b.SessionType)
            .OrderByDescending(b => b.Start)
            .ToListAsync();

        // Private galleries = the client's session photos, delivered only to them.
        var galleries = await _db.Galleries
            .Include(g => g.Photos)
            .Include(g => g.Booking)
            .Where(g => g.IsPrivate && g.Booking != null && g.Booking.ClientUserId == userId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

        return View(new BookingMineViewModel { Bookings = bookings, Galleries = galleries });
    }

    /// <summary>
    /// Mini bookings placed for an event day. Note: bookings are stored in UTC;
    /// the comparison uses the calendar date, which matches the local date for
    /// Portugal's slot range (mini slots never cross midnight in UTC).
    /// </summary>
    private async Task<int> CountMiniBookedAsync(DateTime date)
    {
        var startUtc = PortugalTime.ToUtc(date.Date);
        var endUtc = PortugalTime.ToUtc(date.Date.AddDays(1));
        return await _db.Bookings
            .Include(b => b.SessionType)
            .CountAsync(b => b.SessionType!.IsMiniSessionType
                             && b.Start >= startUtc
                             && b.Start < endUtc
                             && b.Status != BookingStatus.Cancelled);
    }

    private string UserId() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
}
