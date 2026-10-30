using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.Services;

/// <summary>Result of checking a promotional discount code against a base price.</summary>
public record DiscountCodeResult(bool Success, decimal DiscountAmount, decimal FinalPrice, string Message);

/// <summary>
/// Validates promo codes stored in the database, managed in the admin panel
/// (AdminDiscountCodes). Matches are case-insensitive (codes are stored
/// uppercase), only active, unexpired codes apply, and the final price is
/// always clamped so a discount can never turn it negative.
/// </summary>
public class DiscountCodeService
{
    private static readonly CultureInfo Currency = new("pt-PT", false);
    private readonly ApplicationDbContext _db;

    public DiscountCodeService(ApplicationDbContext db) => _db = db;

    public async Task<DiscountCodeResult> ValidateAsync(string? code, decimal basePrice)
    {
        var requestedCode = code?.Trim().ToUpperInvariant() ?? string.Empty;

        if (basePrice < 0m)
            return new DiscountCodeResult(false, 0m, 0m, "O preço base é inválido.");

        var discount = await _db.DiscountCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Code == requestedCode);

        if (discount is null || !discount.IsActive)
            return Invalid(basePrice);

        if (discount.ExpiryDate.HasValue && discount.ExpiryDate.Value.Date < PortugalTime.Today)
            return new DiscountCodeResult(false, 0m, basePrice, $"O código {requestedCode} já expirou.");

        var quotedDiscount = discount.DiscountType switch
        {
            DiscountCodeType.Percentage => Math.Round(basePrice * discount.Value / 100m, 2, MidpointRounding.AwayFromZero),
            DiscountCodeType.FixedAmount => discount.Value,
            _ => 0m
        };

        if (quotedDiscount <= 0m)
            return Invalid(basePrice);

        var finalPrice = Math.Max(0m, basePrice - quotedDiscount);
        finalPrice = Math.Round(finalPrice, 2, MidpointRounding.AwayFromZero);
        var appliedDiscount = Math.Min(quotedDiscount, basePrice);

        var label = discount.DiscountType == DiscountCodeType.FixedAmount
            ? $"{FormatMoney(appliedDiscount)} fixos"
            : $"{discount.Value:0.##}%";

        return new DiscountCodeResult(
            true,
            appliedDiscount,
            finalPrice,
            $"Código {requestedCode} aplicado — desconto de {label}.");
    }

    private static DiscountCodeResult Invalid(decimal basePrice) =>
        new(false, 0m, basePrice, "Código promocional inválido.");

    private static string FormatMoney(decimal value) =>
        value.ToString("C", Currency);
}
