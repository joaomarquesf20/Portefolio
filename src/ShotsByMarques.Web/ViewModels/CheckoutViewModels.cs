using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.ViewModels;

public class CheckoutItemViewModel
{
    public int PhotoId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string GalleryTitle { get; set; } = string.Empty;
    public string PreviewPath { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public string PreviewUrl => PreviewPath.StartsWith("photos/")
        ? $"/Photos/Preview/{PhotoId}"
        : PreviewPath;
}

public class CheckoutIndexViewModel
{
    public List<CheckoutItemViewModel> Items { get; set; } = new();
    public decimal Subtotal => Items.Sum(i => i.Price);
    public decimal Discount { get; set; }
    public string? PackName { get; set; }
    public int? MinPackPhotos { get; set; }
    public decimal Total => Subtotal - Discount;
    public bool RequiresLogin { get; set; }
    public bool StripeConfigured { get; set; } = true;
}

public class DownloadsViewModel
{
    public List<Order> Orders { get; set; } = new();

    /// <summary>Private galleries of the client's sessions — photos included in the session price.</summary>
    public List<ShotsByMarques.Web.Models.Gallery> SessionGalleries { get; set; } = new();
}
