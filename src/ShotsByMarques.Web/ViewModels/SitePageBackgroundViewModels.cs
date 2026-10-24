namespace ShotsByMarques.Web.ViewModels;

public class SitePageBackgroundItemViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string? PageUrl { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public bool IsCustom { get; set; }
}

public class SitePageBackgroundIndexViewModel
{
    public List<SitePageBackgroundItemViewModel> Items { get; set; } = new();
}
