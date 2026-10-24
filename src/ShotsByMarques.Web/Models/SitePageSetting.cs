namespace ShotsByMarques.Web.Models;

public class SitePageSetting
{
    public int Id { get; set; }
    public string PageKey { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
