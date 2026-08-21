namespace ShotsByMarques.Web.Models;

public class Booking
{
    public int Id { get; set; }
    public string ClientUserId { get; set; } = string.Empty;
    public int SessionTypeId { get; set; }
    public DateTime Start { get; set; }
    public string? Notes { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The client's area/town for the session (used to estimate travel distance).</summary>
    public string? Location { get; set; }

    /// <summary>Travel surcharge beyond the free radius (e.g. (km − 60) × €/km).</summary>
    public decimal TravelFee { get; set; }

    /// <summary>50% paid online at booking.</summary>
    public bool DepositPaid { get; set; }

    /// <summary>Remaining 50% paid when the photos are delivered.</summary>
    public bool BalancePaid { get; set; }

    /// <summary>Stripe checkout session of the deposit payment (success-page/webhook matching).</summary>
    public string? DepositSessionId { get; set; }

    /// <summary>Stripe checkout session of the balance payment (success-page/webhook matching).</summary>
    public string? BalanceSessionId { get; set; }

    public ApplicationUser? ClientUser { get; set; }
    public SessionType? SessionType { get; set; }
    public Gallery? Gallery { get; set; }

    public DateTime End => Start.AddMinutes(SessionType?.DurationMinutes ?? 120);

    /// <summary>Half of the total, rounded — charged as a deposit at booking time.</summary>
    public decimal DepositAmount => Math.Round(Price / 2, 2, MidpointRounding.AwayFromZero);

    /// <summary>The other half, charged when the photos are delivered.</summary>
    public decimal BalanceAmount => Price - DepositAmount;
}
