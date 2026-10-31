namespace ShotsByMarques.Web.Models;

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string ClientUserId { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;
    public decimal Total { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? PackName { get; set; }

    /// <summary>Discount applied by the promo code (stacks on top of the pack discount).</summary>
    public decimal CouponDiscount { get; set; }

    /// <summary>The promo code that produced CouponDiscount (normalized uppercase).</summary>
    public string? CouponCode { get; set; }
    public string? StripeSessionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    public ApplicationUser? ClientUser { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}
