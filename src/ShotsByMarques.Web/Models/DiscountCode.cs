using System.ComponentModel.DataAnnotations;

namespace ShotsByMarques.Web.Models;

/// <summary>How a discount code's Value is interpreted.</summary>
public enum DiscountCodeType
{
    /// <summary>Value is a percentage of the base price (e.g. 20 = 20% off).</summary>
    Percentage = 0,

    /// <summary>Value is a fixed euro amount (e.g. 5 = €5 off).</summary>
    FixedAmount = 1
}

/// <summary>
/// Promotional discount code managed in the admin panel. Matches are
/// case-insensitive (codes are always stored uppercase), the percentage or
/// fixed value is applied by DiscountCodeService, and the final price is
/// clamped so the discount can never reduce it below zero.
/// </summary>
public class DiscountCode
{
    public int Id { get; set; }

    /// <summary>The promo code customers type at checkout (always uppercase).</summary>
    [Required(ErrorMessage = "Escreve um código promocional.")]
    [StringLength(50, ErrorMessage = "O código não pode ter mais de 50 caracteres.")]
    public string Code { get; set; } = string.Empty;

    public DiscountCodeType DiscountType { get; set; } = DiscountCodeType.Percentage;

    /// <summary>Percentage (e.g. 20 = 20% off) or fixed euro amount, depending on DiscountType.</summary>
    [Range(0.01, 100_000, ErrorMessage = "O valor tem de ser maior que zero.")]
    public decimal Value { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Optional expiry date — the code stops validating after this day (inclusive).</summary>
    public DateTime? ExpiryDate { get; set; }
}
