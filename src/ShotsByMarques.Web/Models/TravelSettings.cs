namespace ShotsByMarques.Web.Models;

/// <summary>
/// Travel fee configuration ("Travel" section in appsettings.json):
/// sessions within FreeKm of the base location are free; beyond that,
/// PricePerKm is charged for each extra kilometre.
/// </summary>
public class TravelSettings
{
    public string BaseCity { get; set; } = "Ancede";
    public double BaseLat { get; set; } = 41.1;
    public double BaseLon { get; set; } = -8.05;
    public double FreeKm { get; set; } = 60;
    public decimal PricePerKm { get; set; } = 0.50m;
}
