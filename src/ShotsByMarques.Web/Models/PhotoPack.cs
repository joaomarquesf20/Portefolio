namespace ShotsByMarques.Web.Models;

/// <summary>
/// Discount pack: buying MinPhotos or more photos applies DiscountPercent
/// to the whole order (e.g. "Pack 10 Fotos" = 10+ photos, 15% off).
/// </summary>
public class PhotoPack
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinPhotos { get; set; }
    public decimal DiscountPercent { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
