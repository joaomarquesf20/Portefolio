using ShotsByMarques.Web.Models;
using Stripe;
using Stripe.Checkout;

namespace ShotsByMarques.Web.Services;

public class StripeCheckoutService
{
    private readonly IConfiguration _config;
    private readonly StripeClient _client;
    private readonly string _baseUrl;

    public StripeCheckoutService(IConfiguration config, IHostEnvironment env)
    {
        _config = config;
        _client = new StripeClient(config["Stripe:SecretKey"]);
        _baseUrl = env.IsDevelopment() ? "http://localhost:5123" : config["SiteUrl"] ?? string.Empty;
    }

    /// <summary>
    /// Picks the public base URL for absolute redirect URLs (success/cancel).
    /// The per-request override comes from the controller (honouring reverse
    /// proxy forwarded headers — e.g. ngrok), falling back to the configured
    /// base URL when it cannot be resolved.
    /// </summary>
    private static string BaseUrl(string configured, string? perRequest) =>
        string.IsNullOrWhiteSpace(perRequest) ? configured : perRequest.TrimEnd('/');

    public bool IsConfigured =>
        !string.IsNullOrEmpty(_config["Stripe:SecretKey"]) &&
        !_config["Stripe:SecretKey"]!.StartsWith("sk_placeholder");

    public async Task<string?> CreateCheckoutSessionAsync(Order order, IEnumerable<OrderItem> items, string? publicBaseUrl = null)
    {
        if (!IsConfigured)
            return null;

        var baseUrl = BaseUrl(_baseUrl, publicBaseUrl);
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = $"{baseUrl}/Checkout/Success?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{baseUrl}/Checkout/Cancel?session_id={{CHECKOUT_SESSION_ID}}",
            CustomerEmail = order.ClientUser?.Email,
            ClientReferenceId = order.OrderNumber,
            Metadata = new Dictionary<string, string>
            {
                ["orderId"] = order.Id.ToString()
            },
            LineItems = items.Select(i => new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "eur",
                    UnitAmount = (long)Math.Round(i.Price * 100),
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = i.Title,
                        Description = $"Foto digital — {order.OrderNumber}"
                    }
                }
            }).ToList()
        };

        // Payment methods (e.g. "card,mb_way" — MB Way for Portuguese Stripe accounts).
        // Empty = let Stripe decide from the Dashboard settings.
        var paymentMethods = _config["Stripe:PaymentMethods"];
        if (!string.IsNullOrWhiteSpace(paymentMethods))
        {
            options.PaymentMethodTypes = paymentMethods
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        // Combined discount (photo pack + promo coupon code) applied as a single
        // exact coupon, so the amount Stripe charges matches Order.Total exactly.
        var discountCents = (long)Math.Round((order.DiscountAmount + order.CouponDiscount) * 100);
        if (discountCents > 0)
        {
            var couponLabel = string.Join(" · ", new[] { order.PackName, order.CouponCode }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
            var coupon = await new CouponService(_client).CreateAsync(new CouponCreateOptions
            {
                Name = string.IsNullOrWhiteSpace(couponLabel) ? "Desconto" : couponLabel,
                AmountOff = discountCents,
                Currency = "eur",
                Duration = "once"
            });
            options.Discounts = new List<SessionDiscountOptions>
            {
                new() { Coupon = coupon.Id }
            };
        }

        var service = new SessionService(_client);
        var session = await service.CreateAsync(options);
        return session.Id;
    }

    /// <summary>
    /// Creates a Stripe Checkout session for a booking payment (deposit or balance).
    /// The webhook and the success page match it via metadata (bookingId + purpose).
    /// </summary>
    public async Task<string?> CreateBookingCheckoutSessionAsync(Booking booking, decimal amount, string purpose, string? publicBaseUrl = null)
    {
        if (!IsConfigured)
            return null;

        var baseUrl = BaseUrl(_baseUrl, publicBaseUrl);
        var successAction = purpose == "balance" ? "BalanceSuccess" : "DepositSuccess";
        var isBalance = purpose == "balance";
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = $"{baseUrl}/Bookings/{successAction}?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{baseUrl}/as-minhas-marcacoes",
            CustomerEmail = booking.ClientUser?.Email,
            Metadata = new Dictionary<string, string>
            {
                ["bookingId"] = booking.Id.ToString(),
                ["purpose"] = purpose
            },
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "eur",
                        UnitAmount = (long)Math.Round(amount * 100),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = isBalance ? "Sessão — saldo (na entrega)" : "Sessão — depósito (50%)",
                            Description = booking.SessionType?.Name ?? "Marcação de sessão"
                        }
                    }
                }
            }
        };

        var paymentMethods = _config["Stripe:PaymentMethods"];
        if (!string.IsNullOrWhiteSpace(paymentMethods))
        {
            options.PaymentMethodTypes = paymentMethods
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        var service = new SessionService(_client);
        var session = await service.CreateAsync(options);
        return session.Id;
    }

    /// <summary>"paid", "processing" (MB Way confirmation pending) or "unpaid".</summary>
    public async Task<string?> GetPaymentStatusAsync(string sessionId)
    {
        var service = new SessionService(_client);
        var session = await service.GetAsync(sessionId);
        return session.PaymentStatus;
    }

    public async Task<string?> GetSessionUrlAsync(string sessionId)
    {
        var service = new SessionService(_client);
        var session = await service.GetAsync(sessionId);
        return session.Url;
    }
}
