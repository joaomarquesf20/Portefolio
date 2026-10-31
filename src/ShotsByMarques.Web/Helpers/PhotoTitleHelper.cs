using System.Text.RegularExpressions;

namespace ShotsByMarques.Web.Helpers;

/// <summary>
/// Photo titles uploaded from cameras default to the raw file name
/// ("_DSC8299", "IMG_1145.jpg"). Those are machine names, not captions —
/// they must never appear as text overlays in the gallery grid or the
/// lightbox. A genuinely typed title ("First dance", "Natal 2024") is
/// always kept.
/// </summary>
public static class PhotoTitleHelper
{
    /// <summary>
    /// Camera-generated names: optional underscore, a 2–4 letter device
    /// prefix (DSC/IMG/PXL/DJI/DSCF…), optional separator, then digits.
    /// The whole string must match — descriptive titles with words never do.
    /// </summary>
    private static readonly Regex RawCameraName = new(
        @"^_?[a-z]{2,4}[_-]?[0-9][0-9_-]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private static readonly Regex ImageExtension = new(
        @"\.(jpe?g|png|webp|heic|tiff?)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Title safe to display in overlays: the trimmed/clean title, or "" when
    /// the value is a raw camera filename (strip the extension first).
    /// </summary>
    public static string VisibleTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        var clean = ImageExtension.Replace(title.Trim(), "").Trim();
        return RawCameraName.IsMatch(clean) ? "" : clean;
    }
}
