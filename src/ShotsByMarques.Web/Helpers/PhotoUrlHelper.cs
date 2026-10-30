using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.Helpers;

public static class PhotoUrlHelper
{
    /// <summary>
    /// Normalizes a stored image path (e.g. SessionType.ImagePath) into a URL
    /// the browser can load. Normally the value is a clean wwwroot-relative
    /// path ("/sample-photos/cars-cover.svg"), but stray absolute local values
    /// such as "C:\Users\...\wwwroot\sample-photos\cars-cover.svg" would render
    /// broken-image icons. Backslashes and "~/" are normalized, anything before
    /// "/wwwroot/" is stripped so the URL always originates from wwwroot, and
    /// unrecoverable local paths (drive letters, UNC shares, "file:") fall back
    /// to the supplied default image instead of 404ing in the browser.
    /// </summary>
    public static string ToWebPath(this string? path, string fallback)
    {
        if (string.IsNullOrWhiteSpace(path)) return fallback;

        var url = path.Trim().Replace('\\', '/');
        if (url.StartsWith("~/", StringComparison.Ordinal)) url = url[2..];

        // Absolute remote URL — already loadable by the browser, use as-is.
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;

        // Absolute filesystem path: keep only the section served from wwwroot.
        var wwwrootIndex = url.IndexOf("/wwwroot/", StringComparison.OrdinalIgnoreCase);
        if (wwwrootIndex >= 0) return url[(wwwrootIndex + "/wwwroot/".Length)..];
        if (url.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase)) return url["wwwroot/".Length..];

        // Unrecoverable local paths (drive "C:/..."/UNC "//server/..."/"file:")
        // can never resolve through the server — serve the fallback image.
        var isLocalAbsolute =
            (url.Length >= 2 && char.IsAsciiLetter(url[0]) && url[1] == ':') ||
            (url.StartsWith("//") && url.Length > 2) ||
            url.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
        if (isLocalAbsolute) return fallback;

        return url.StartsWith('/') ? url : '/' + url;
    }

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
    /// CLEAN (watermark-free) thumbnail for light-theme grid tiles.
    /// Uploaded photos resolve to the /Photos/Thumb endpoint (900px, quality
    /// 92) — small enough to be low-theft-value. Legacy wwwroot sample assets
    /// expose a clean variant; "-wm." is stripped.
    /// </summary>
    public static string ThumbUrl(this Photo photo) =>
        photo.PreviewPath.StartsWith("photos/")
            ? $"/Photos/Thumb/{photo.Id}"
            : photo.PreviewPath.Replace("-wm.", ".");

    /// <summary>
    /// CLEAN, HIGH-RESOLUTION derivative for covers, full-bleed masonry tiles
    /// and hero/page backgrounds. Uploaded photos resolve to /Photos/Cover —
    /// 1920px, JPEG quality 92, sharp on desktop monitors, but still a
    /// downscaled re-encode (never the raw original). Legacy wwwroot sample
    /// assets expose a clean variant; "-wm." is stripped.
    /// </summary>
    public static string LargeCleanUrl(this Photo photo) =>
        photo.PreviewPath.StartsWith("photos/")
            ? $"/Photos/Cover/{photo.Id}"
            : photo.PreviewPath.Replace("-wm.", ".");

    /// <summary>
    /// Cover URL for rendering. Covers are usually a public wwwroot path, but
    /// when the cover was picked from one of the gallery's uploaded photos it
    /// points at a private preview path ("photos/...") — resolve it to the
    /// CLEAN high-res cover endpoint instead of a raw (broken) path.
    /// </summary>
    public static string CoverUrl(this Gallery gallery) =>
        gallery.CoverPath is null || gallery.CoverPath.Length == 0
            ? string.Empty
            : gallery.CoverPath.StartsWith("photos/")
                ? gallery.Photos.FirstOrDefault(p => p.PreviewPath == gallery.CoverPath)?.LargeCleanUrl() ?? gallery.CoverPath
                : gallery.CoverPath;
}
