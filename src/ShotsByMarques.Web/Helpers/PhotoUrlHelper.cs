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

    /// <summary>
    /// Cover URL for rendering. Covers are usually a public wwwroot path, but
    /// when the cover was picked from one of the gallery's uploaded photos it
    /// points at a private preview path ("photos/...") — resolve it to the
    /// watermarked preview endpoint instead of a raw (broken) path.
    /// </summary>
    public static string CoverUrl(this Gallery gallery) =>
        gallery.CoverPath is null || gallery.CoverPath.Length == 0
            ? string.Empty
            : gallery.CoverPath.StartsWith("photos/")
                ? gallery.Photos.FirstOrDefault(p => p.PreviewPath == gallery.CoverPath)?.PreviewUrl() ?? gallery.CoverPath
                : gallery.CoverPath;
}
