using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.Helpers;

public static class PhotoUrlHelper
{
    /// <summary>
    /// Publicly viewable preview URL: photos stored outside wwwroot are served
    /// through the preview endpoint; wwwroot paths (sample photos) are used as-is.
    /// </summary>
    public static string PreviewUrl(this Photo photo) =>
        photo.PreviewPath.StartsWith("photos/")
            ? $"/Photos/Preview/{photo.Id}"
            : photo.PreviewPath;
}
