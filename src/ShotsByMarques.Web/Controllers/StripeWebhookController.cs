using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;
using Stripe;

namespace ShotsByMarques.Web.Controllers;

/// <summary>
/// Receives Stripe events (checkout.session.completed) and marks orders as paid.
/// In development, forward events with: stripe listen --forward-to localhost:5123/stripe/webhook
/// </summary>
[ApiController]
[Route("stripe/webhook")]
public class StripeWebhookController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(ApplicationDbContext db, IConfiguration config, IHostEnvironment env, ILogger<StripeWebhookController> logger)
    {
        _db = db;
        _config = config;
        _env = env;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Index()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var secret = _config["Stripe:WebhookSecret"];

        if (string.IsNullOrEmpty(secret))
        {
            if (_env.IsProduction())
                return StatusCode(500, "Stripe webhook secret is missing in production.");
            return Ok(); // dev: not configured — the success page handles the fallback
        }

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], secret);

            if (stripeEvent.Type == "checkout.session.completed")
            {
                var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                if (session?.Metadata?.TryGetValue("orderId", out var orderId) == true
                    && int.TryParse(orderId, out var id))
                {
                    var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
                    if (order is not null && order.Status != OrderStatus.Paid)
                    {
                        order.Status = OrderStatus.Paid;
                        order.PaidAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync();
                        _logger.LogInformation("Order {OrderNumber} marked as paid.", order.OrderNumber);
                    }
                }
            }

            return Ok();
        }
        catch (StripeException e)
        {
            _logger.LogError(e, "Invalid Stripe signature.");
            return BadRequest();
        }
    }
}
