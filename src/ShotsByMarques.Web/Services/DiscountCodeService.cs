using System.Globalization;

namespace ShotsByMarques.Web.Services;

/// <summary>Result of checking a promotional discount code against a base price.</summary>
public record DiscountCodeResult(bool Success, decimal DiscountAmount, decimal FinalPrice, string Message);

/// <summary>
/// Temporary, in-memory list of active campaign codes. Matches are
/// case-insensitive and the final price is always clamped so a discount can
/// never turn it negative.
/// </summary>
public static class DiscountCodeService
{
    private static readonly CultureInfo Currency = new("pt-PT", false);
    private const string TenPercentCode = "SHOTS10";
    private const string FifteenEuroCode = "OFERTA15";
    private const decimal FifteenEuro = 15m;
    private const decimal TenPercent = 0.10m;

    public static DiscountCodeResult Validate(string? code, decimal basePrice)
    {
        var requestedCode = code?.Trim().ToUpperInvariant() ?? string.Empty;

        if (basePrice < 0m)
            return new DiscountCodeResult(false, 0m, 0m, "O preço base é inválido.");

        var quotedDiscount = requestedCode switch
        {
            TenPercentCode => Math.Round(basePrice * TenPercent, 2, MidpointRounding.AwayFromZero),
            FifteenEuroCode => FifteenEuro,
            _ => 0m
        };

        if (quotedDiscount <= 0m)
            return new DiscountCodeResult(false, 0m, basePrice, "Código inválido. Experimenta SHOTS10 ou OFERTA15.");

        var finalPrice = Math.Max(0m, basePrice - quotedDiscount);
        finalPrice = Math.Round(finalPrice, 2, MidpointRounding.AwayFromZero);
        var appliedDiscount = Math.Min(quotedDiscount, basePrice);

        var label = requestedCode == FifteenEuroCode
            ? $"{FormatMoney(appliedDiscount)} fixos"
            : "10%";

        return new DiscountCodeResult(
            true,
            appliedDiscount,
            finalPrice,
            $"Código {requestedCode} aplicado — desconto de {label}.");
    }

    private static string FormatMoney(decimal value) =>
        value.ToString("C", Currency);
}
