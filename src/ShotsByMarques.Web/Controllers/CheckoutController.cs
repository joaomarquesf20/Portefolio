using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;
using ShotsByMarques.Web.Services;
using ShotsByMarques.Web.ViewModels;
using System.Security.Claims;

namespace ShotsByMarques.Web.Controllers;

[Authorize]
public class CheckoutController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly StripeCheckoutService _stripe;

    public CheckoutController(ApplicationDbContext db, StripeCheckoutService stripe)
    {
        _db = db;
        _stripe = stripe;
    }

    /// <summary>Receives the selected photo ids (?ids=1,2,3) from the client cart.</summary>
    public async Task<IActionResult> Index(string? ids)
    {
        var model = new CheckoutIndexViewModel { StripeConfigured = _stripe.IsConfigured };

        var photoIds = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (photoIds.Count == 0)
            return View(model);

        var photos = await _db.Photos
            .Include(p => p.Gallery).ThenInclude(g => g!.Booking)
            .Where(p => photoIds.Contains(p.Id) && p.IsForSale &&
                        // Session galleries (or private galleries tied to a booking)
                        // are already paid with the session — photos are NEVER sold.
                        !p.Gallery!.IsSession && p.Gallery.BookingId == null &&
                        ((p.Gallery.IsPrivate && p.Gallery.Booking != null && p.Gallery.Booking.ClientUserId == UserId()) ||
                         (!p.Gallery.IsPrivate && p.Gallery.IsPublished)))
            .ToListAsync();

        model.Items = photos.Select(p => new CheckoutItemViewModel
        {
            PhotoId = p.Id,
            Title = p.Title,
            GalleryTitle = p.Gallery!.Title,
            PreviewPath = p.PreviewPath,
            Price = p.Price
        }).ToList();

        await ApplyBestPackAsync(model);
        return View(model);

        async Task ApplyBestPackAsync(CheckoutIndexViewModel m)
        {
            var result = await BestPackAsync(m.Items.Count, m.Subtotal);
            m.Discount = result.Discount;
            m.PackName = result.PackName;
            m.MinPackPhotos = (await _db.PhotoPacks.Where(p => p.IsActive).OrderBy(p => p.MinPhotos).FirstOrDefaultAsync())?.MinPhotos;
        }
    }

    [HttpPost("validar-codigo")]
    [ValidateAntiForgeryToken]
    [Produces("application/json")]
    public IActionResult ValidateDiscount([FromForm] DiscountCodeRequest request)
    {
        // The browser sends the base price as an invariant-culture string
        // (e.g. "24.30"); binding it as decimal broke on comma-culture servers.
        // Parse it manually here instead, so model binding never rejects it.
        if (request is null)
        {
            return Json(new DiscountCodeResponse
            {
                Success = false,
                DiscountAmount = 0m,
                FinalPrice = 0m,
                Message = "Os dados do código promocional são inválidos."
            });
        }

        decimal basePrice = 0m;
        var parseError = false;
        try
        {
            if (request.BasePrice is null)
            {
                parseError = true;
            }
            else
            {
                basePrice = decimal.Parse(request.BasePrice, System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        catch (FormatException)
        {
            parseError = true;
        }
        catch (OverflowException)
        {
            parseError = true;
        }

        basePrice = Math.Max(0m, basePrice);

        if (parseError || !ModelState.IsValid)
        {
            var message = parseError
                ? "O preço base é inválido."
                : ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e))
                    ?? "Os dados do código promocional são inválidos.";
            return Json(new DiscountCodeResponse
            {
                Success = false,
                DiscountAmount = 0m,
                FinalPrice = basePrice,
                Message = message
            });
        }

        return Json(DiscountCodeService.Validate(request.Code, basePrice));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder(string? ids)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var client = await _db.Users.FindAsync(userId);

        var photoIds = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var photos = await _db.Photos
            .Include(p => p.Gallery).ThenInclude(g => g!.Booking)
            .Where(p => photoIds.Contains(p.Id) && p.IsForSale &&
                        // Session galleries (or private galleries tied to a booking)
                        // are already paid with the session — photos are NEVER sold.
                        !p.Gallery!.IsSession && p.Gallery.BookingId == null &&
                        ((p.Gallery.IsPrivate && p.Gallery.Booking != null && p.Gallery.Booking.ClientUserId == userId) ||
                         (!p.Gallery.IsPrivate && p.Gallery.IsPublished)))
            .ToListAsync();
        if (photos.Count == 0)
            return RedirectToAction(nameof(Index));

        var order = new Order
        {
            OrderNumber = $"SBM-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..24],
            ClientUserId = userId,
            ClientUser = client,
            Subtotal = photos.Sum(p => p.Price)
        };

        // Apply the best photo pack (discount) to the order.
        var packResult = await BestPackAsync(photos.Count, order.Subtotal);
        order.DiscountAmount = packResult.Discount;
        order.PackName = packResult.PackName;
        order.Total = packResult.Total;
        foreach (var photo in photos)
        {
            order.Items.Add(new OrderItem
            {
                PhotoId = photo.Id,
                Title = photo.Title,
                Price = photo.Price,
                FilePath = photo.FilePath
            });
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        string? sessionId;
        try
        {
            sessionId = await _stripe.CreateCheckoutSessionAsync(order, order.Items);
        }
        catch (Exception ex)
        {
            order.Status = OrderStatus.Cancelled;
            await _db.SaveChangesAsync();
            TempData["Error"] = $"Não foi possível iniciar o pagamento: {ex.Message}";
            return RedirectToAction(nameof(Index), new { ids });
        }
        if (sessionId is null)
        {
            order.Status = OrderStatus.Cancelled;
            await _db.SaveChangesAsync();
            TempData["Error"] = "As chaves do Stripe ainda não estão configuradas. Adiciona-as em appsettings.json (modo de teste).";
            return RedirectToAction(nameof(Index), new { ids });
        }

        order.StripeSessionId = sessionId;
        await _db.SaveChangesAsync();

        var sessionUrl = await _stripe.GetSessionUrlAsync(sessionId);
        if (string.IsNullOrEmpty(sessionUrl))
            return RedirectToAction(nameof(Cancel));
        return Redirect(sessionUrl);
    }

    public async Task<IActionResult> Success(string session_id)
    {
        // States: paid (confirmed), processing (MB Way confirmation pending), unpaid
        ViewData["State"] = "unpaid";

        if (!string.IsNullOrEmpty(session_id))
        {
            var order = await _db.Orders
                .FirstOrDefaultAsync(o => o.StripeSessionId == session_id && o.ClientUserId == UserId());
            if (order is not null && order.Status != OrderStatus.Paid)
            {
                // Fallback verification: the webhook is the source of truth.
                var status = await _stripe.GetPaymentStatusAsync(session_id);
                if (status == "paid")
                {
                    order.Status = OrderStatus.Paid;
                    order.PaidAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                    ViewData["State"] = "paid";
                }
                else if (status == "processing")
                {
                    // MB Way: the customer approves in the MB Way app — confirmation is async.
                    // Auto-refresh so the page flips to "paid" once the webhook confirms it.
                    ViewData["State"] = "processing";
                    ViewData["MetaRefresh"] = "5";
                }
            }
            else
            {
                ViewData["State"] = "paid";
            }
        }
        return View();
    }

    public async Task<IActionResult> Cancel(string session_id)
    {
        if (!string.IsNullOrEmpty(session_id))
        {
            var order = await _db.Orders
                .FirstOrDefaultAsync(o => o.StripeSessionId == session_id && o.ClientUserId == UserId());
            if (order is not null && order.Status == OrderStatus.PendingPayment)
            {
                order.Status = OrderStatus.Cancelled;
                await _db.SaveChangesAsync();
            }
        }
        return View();
    }

    private async Task<PackDiscount> BestPackAsync(int photoCount, decimal subtotal)
    {
        var packs = await _db.PhotoPacks.Where(p => p.IsActive).ToListAsync();
        return PackService.BestPack(packs, photoCount, subtotal);
    }

    private string UserId() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
}
