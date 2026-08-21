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

    public bool IsConfigured =>
        !string.IsNullOrEmpty(_config["Stripe:SecretKey"]) &&
        !_config["Stripe:SecretKey"]!.StartsWith("sk_placeholder");

    public async Task<string?> CreateCheckoutSessionAsync(Order order, IEnumerable<OrderItem> items)
    {
        if (!IsConfigured)
            return null;

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = $"{_baseUrl}/Checkout/Success?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{_baseUrl}/Checkout/Cancel?session_id={{CHECKOUT_SESSION_ID}}",
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

        // Photo pack discount: apply the exact discount amount (in cents) as a coupon,
        // so the amount Stripe charges matches Order.Total exactly.
        if (order.DiscountAmount > 0)
        {
            var amountOff = (long)Math.Round(order.DiscountAmount * 100);
            if (amountOff > 0)
            {
                var coupon = await new CouponService(_client).CreateAsync(new CouponCreateOptions
                {
                    Name = order.PackName ?? "Desconto pack",
                    AmountOff = amountOff,
                    Currency = "eur",
                    Duration = "once"
                });
                options.Discounts = new List<SessionDiscountOptions>
                {
                    new() { Coupon = coupon.Id }
                };
            }
        }

        var service = new SessionService(_client);
        var session = await service.CreateAsync(options);
        return session.Id;
    }

    /// <summary>
    /// Creates a Stripe Checkout session for a booking payment (deposit or balance).
    /// The webhook and the success page match it via metadata (bookingId + purpose).
    /// </summary>
    public async Task<string?> CreateBookingCheckoutSessionAsync(Booking booking, decimal amount, string purpose)
    {
        if (!IsConfigured)
            return null;

        var successAction = purpose == "balance" ? "BalanceSuccess" : "DepositSuccess";
        var isBalance = purpose == "balance";
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = $"{_baseUrl}/Bookings/{successAction}?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{_baseUrl}/as-minhas-marcacoes",
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
