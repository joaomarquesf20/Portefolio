using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.Helpers;

public static class PhotoUrlHelper
{
    /// <summary>
    /// Large (watermarked) preview URL. THIS IS THE LIGHTBOX-ONLY ASSET —
    /// the view must never place it in a src attribute; site.js receives it
    /// via a data-secure-src attribute and swaps it in as a blob: URL.
    /// </summary>
    public static string LargePreviewUrl(this Photo photo) => PreviewUrlInternal(photo);

    private static string PreviewUrlInternal(Photo photo) =>
        photo.PreviewPath.StartsWith("photos/")
            ? $"/Photos/Preview/{photo.Id}"
            : photo.PreviewPath;

    /// <summary>
    /// Publicly viewable preview URL: photos stored outside wwwroot are served
    /// through the preview endpoint; wwwroot paths (sample photos) are used as-is.
    /// Kept for checkout/legacy callers — gallery grids should prefer ThumbUrl.
    /// </summary>
    public static string PreviewUrl(this Photo photo) => PreviewUrlInternal(photo);

    /// <summary>
    /// CLEAN (watermark-free) thumbnail for light-theme grid tiles and covers.
    /// Uploaded photos resolve to the /Photos/Thumb endpoint, which streams a
    /// watermark-free resize derived from the original — the WATERMARKED large
    /// preview is reserved exclusively for the lightbox (LargePreviewUrl).
    /// Legacy wwwroot sample assets expose a clean variant; "-wm." is stripped.
    /// </summary>
    public static string ThumbUrl(this Photo photo) =>
        photo.PreviewPath.StartsWith("photos/")
            ? $"/Photos/Thumb/{photo.Id}"
            : photo.PreviewPath.Replace("-wm.", ".");

    /// <summary>
    /// Cover URL for rendering. Covers are usually a public wwwroot path, but
    /// when the cover was picked from one of the gallery's uploaded photos it
    /// points at a private preview path ("photos/...") — resolve it to the
    /// CLEAN thumb endpoint instead of a raw (broken) path.
    /// </summary>
    public static string CoverUrl(this Gallery gallery) =>
        gallery.CoverPath is null || gallery.CoverPath.Length == 0
            ? string.Empty
            : gallery.CoverPath.StartsWith("photos/")
                ? gallery.Photos.FirstOrDefault(p => p.PreviewPath == gallery.CoverPath)?.ThumbUrl() ?? gallery.CoverPath
                : gallery.CoverPath;
}
