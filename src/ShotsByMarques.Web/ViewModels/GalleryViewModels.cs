using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.ViewModels;

public class GalleryDetailViewModel
{
    public Gallery Gallery { get; set; } = new();
    public List<Photo> Photos { get; set; } = new();
}
