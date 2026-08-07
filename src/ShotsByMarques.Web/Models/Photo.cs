namespace ShotsByMarques.Web.Models;

public class Photo
{
    public int Id { get; set; }
    public int GalleryId { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>Path to the full-resolution file (available after purchase).</summary>
    public string FilePath { get; set; } = string.Empty;
    /// <summary>Path to the watermarked preview shown publicly.</summary>
    public string PreviewPath { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int SortOrder { get; set; }
    public bool IsForSale { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Gallery? Gallery { get; set; }
}
