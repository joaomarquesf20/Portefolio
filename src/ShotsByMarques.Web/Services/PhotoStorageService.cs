using SkiaSharp;

namespace ShotsByMarques.Web.Services;

public class PhotoStorageService
{
    private readonly IWebHostEnvironment _env;

    public PhotoStorageService(IWebHostEnvironment env) => _env = env;

    /// <summary>
    /// Absolute folder for purchased (full-resolution) photos — OUTSIDE wwwroot,
    /// so they are never served as static files. Only DownloadsController.File
    /// (after ownership + payment checks) streams them.
    /// </summary>
    public string PhotosRoot => Path.Combine(_env.ContentRootPath, "App_Data", "photos");

    public const long MaxUploadBytes = 25 * 1024 * 1024; // 25 MB

    /// <summary>Maximum width/height of the watermarked preview (keeps traffic low).</summary>
    private const int PreviewMaxDimension = 1600;

    /// <summary>Maximum width/height of the WATERMARKED grid thumb (baked pixels, "-wt.jpg").</summary>
    public const int ThumbMaxDimension = 900;

    /// <summary>Maximum width/height of the CLEAN high-res derivative (covers / hero banners).</summary>
    public const int CoverMaxDimension = 1920;

    /// <summary>Watermark text stamped on previews (shown to clients before purchase).</summary>
    public const string WatermarkText = "© Shots By Marques";

    /// <summary>
    /// Saves a photo outside the web root and generates a WATERMARKED preview
    /// (SkiaSharp). Returns (fullPath, previewPath) as relative paths ("photos/{galleryId}/...").
    /// The full-resolution file is only served after payment (DownloadsController.File);
    /// the preview is what clients see in the gallery and checkout.
    /// </summary>
    public async Task<(string FilePath, string PreviewPath)> SaveAsync(IFormFile file, int galleryId)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            extension = ".jpg";

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var dir = Path.Combine(PhotosRoot, galleryId.ToString());
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, fileName);
        await using (var stream = File.Create(path))
        {
            await file.CopyToAsync(stream);
        }

        // Watermarked preview (1600px, lightbox) + watermarked thumb (900px,
        // grids) + watermarked cover (1920px, /Photos/Cover) — ALL derivatives
        // pre-baked at upload time. If any watermark fails we DELETE the
        // upload and let the error propagate: a watermarked derivative is
        // never optional, otherwise a clean original would be one "missing
        // file" away from leaking.
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var previewName = $"{baseName}-wm.jpg";
        var previewPath = Path.Combine(dir, previewName);
        try
        {
            GenerateWatermarkedDerivative(path, previewPath, PreviewMaxDimension, 88);
            GenerateWatermarkedDerivative(path, Path.Combine(dir, $"{baseName}-wt.jpg"), ThumbMaxDimension, 82);
            GenerateWatermarkedDerivative(path, Path.Combine(dir, $"{baseName}-wc.jpg"), CoverMaxDimension, 88);
        }
        catch
        {
            File.Delete(path);
            throw;
        }

        return ($"photos/{galleryId}/{fileName}", $"photos/{galleryId}/{previewName}");
    }

    /// <summary>
    /// Resizes the image to maxDimension and stamps a tiled diagonal watermark
    /// baked into the pixels — shared by the 1600px lightbox preview, the
    /// 900px grid thumb and the 1920px cover. EVERY public derivative of an
    /// uploaded photo (thumb, preview, cover) goes through this: NO clean
    /// pixels of a client photo ever reach the browser.
    /// </summary>
    public static void GenerateWatermarkedDerivative(string sourcePath, string targetPath, int maxDimension, int quality)
    {
        using var source = SKBitmap.Decode(sourcePath)
            ?? throw new InvalidOperationException("Could not decode image.");

        // Scale down keeping aspect ratio.
        var scale = Math.Min(1f, maxDimension / (float)Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var resized = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgb888x, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(resized))
        {
            canvas.Clear(SKColors.White);
            var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            canvas.DrawBitmap(source, new SKRect(0, 0, width, height), sampling);
        }

        // Tiled diagonal watermark.
        using (var canvas = new SKCanvas(resized))
        {
            using var font = new SKFont(SKTypeface.FromFamilyName("Arial"), width / 14f);
            using var paint = new SKPaint
            {
                Color = new SKColor(255, 255, 255, 42), // ~16% opacity
                IsAntialias = true
            };

            font.MeasureText(WatermarkText, out var bounds);
            var textWidth = bounds.Width;
            var textHeight = bounds.Height;
            var step = textWidth + width * 0.06f;

            canvas.Save();
            canvas.RotateDegrees(-28f, width / 2f, height / 2f);
            for (float x = -height; x < width + height; x += step)
            {
                for (float y = -height; y < height + textHeight; y += textHeight + height * 0.18f)
                {
                    canvas.DrawText(WatermarkText, x, y, SKTextAlign.Left, font, paint);
                }
            }
            canvas.Restore();
        }

        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        using var output = File.Create(targetPath);
        data.SaveTo(output);
    }

    /// <summary>
    /// WATERMARKED thumb for grid tiles and the lightbox initial frame
    /// (900px, quality 82). Generated on demand by /Photos/Thumb into
    /// "{base}-wt.jpg" — the same pipeline as the 1600px preview, at smaller
    /// scale: a screenshot of a grid tile captures the watermark.
    /// </summary>
    public static void GenerateWatermarkedThumb(string sourcePath, string targetPath) =>
        GenerateWatermarkedDerivative(sourcePath, targetPath, ThumbMaxDimension, 82);

    /// <summary>WATERMARKED 1600px preview for the lightbox (quality 88) —
    /// its relative path is stored as the photo's PreviewPath.</summary>
    public static void GenerateWatermarkedPreview(string sourcePath, string targetPath) =>
        GenerateWatermarkedDerivative(sourcePath, targetPath, PreviewMaxDimension, 88);

    /// <summary>WATERMARKED 1920px cover/hero derivative (quality 88) served by
    /// /Photos/Cover. Covers and page backgrounds are public, full-bleed
    /// imagery — screenshots/screen-recording of them must still capture the
    /// watermark, so they go through the same baked-watermark pipeline.</summary>
    public static void GenerateWatermarkedCover(string sourcePath, string targetPath) =>
        GenerateWatermarkedDerivative(sourcePath, targetPath, CoverMaxDimension, 88);

    /// <summary>
    /// Recreates any missing WATERMARKED derivative of an uploaded photo:
    /// the 1600px preview ("-wm.jpg"), the 900px thumb ("-wt.jpg") and the
    /// 1920px cover ("-wc.jpg"). Files already on disk are NOT regenerated.
    /// Called by the admin upload/update/publish flows so that /Photos/*,
    /// /Photos/public and /Photos/Cover always find their pre-baked files
    /// when viewers navigate — no SkiaSharp processing during live traffic.
    /// Legacy wwwroot sample paths are left untouched. Throws on generation
    /// errors — callers decide whether to surface them.
    /// </summary>
    public void EnsureWatermarkedDerivatives(string? relativePath, string? previewRelativePath)
    {
        if (string.IsNullOrEmpty(relativePath) || !relativePath.StartsWith("photos/"))
            return; // legacy wwwroot sample asset — clean by design

        var root = Path.GetFullPath(PhotosRoot);
        var fullOriginal = Path.GetFullPath(Path.Combine(PhotosRoot, relativePath["photos/".Length..]));
        if (!fullOriginal.StartsWith(root + Path.DirectorySeparatorChar) || !File.Exists(fullOriginal))
            return;

        var baseName = Path.Combine(
            Path.GetDirectoryName(fullOriginal) ?? root,
            Path.GetFileNameWithoutExtension(fullOriginal));

        var thumbPath = baseName + "-wt.jpg";
        if (!File.Exists(thumbPath))
            GenerateWatermarkedThumb(fullOriginal, thumbPath);

        var coverPath = baseName + "-wc.jpg";
        if (!File.Exists(coverPath))
            GenerateWatermarkedCover(fullOriginal, coverPath);

        if (!string.IsNullOrEmpty(previewRelativePath) && previewRelativePath.StartsWith("photos/"))
        {
            var previewPath = Path.GetFullPath(Path.Combine(PhotosRoot, previewRelativePath["photos/".Length..]));
            if (previewPath.StartsWith(root + Path.DirectorySeparatorChar) && !File.Exists(previewPath))
                GenerateWatermarkedPreview(fullOriginal, previewPath);
        }
    }

    /// <summary>
    /// Saves a public gallery cover into wwwroot (covers are meant to be public).
    /// </summary>
    public async Task<string> SaveCoverAsync(IFormFile file)    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".svg"))
            extension = ".jpg";

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var dir = Path.Combine(_env.WebRootPath, "uploads", "covers");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, fileName);
        await using (var stream = File.Create(path))
        {
            await file.CopyToAsync(stream);
        }
        return $"/uploads/covers/{fileName}";
    }

    /// <summary>Deletes an uploaded public cover/background file.</summary>
    public void DeletePublicCover(string? publicPath)
    {
        if (string.IsNullOrWhiteSpace(publicPath) || !publicPath.StartsWith("/uploads/covers/", StringComparison.OrdinalIgnoreCase))
            return;

        var relative = publicPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_env.WebRootPath, relative));
        var root = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads", "covers"));
        if (fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
            File.Delete(fullPath);
    }

    /// <summary>
    /// Deletes both the full-resolution file and its watermarked preview —
    /// plus the on-demand derivatives (watermarked thumb, any legacy clean
    /// thumb, and the clean high-res large) if one was generated.
    /// </summary>
    public void Delete(string? relativePath, string? previewPath = null)
    {
        DeleteFile(relativePath);
        DeleteFile(previewPath);
        DeleteThumb(relativePath);
    }

    private void DeleteThumb(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath) || !relativePath.StartsWith("photos/"))
            return;
        var fullPath = Path.GetFullPath(Path.Combine(PhotosRoot, relativePath["photos/".Length..]));
        if (!fullPath.StartsWith(Path.GetFullPath(PhotosRoot) + Path.DirectorySeparatorChar))
            return;
        var baseName = Path.Combine(Path.GetDirectoryName(fullPath) ?? string.Empty, Path.GetFileNameWithoutExtension(fullPath));
        // "-thumb.jpg": legacy clean thumbs (pre-watermark-baking) — swept on
        // delete; "-wt.jpg" thumb / "-wc.jpg" cover: the WATERMARKED
        // derivatives; "-large.jpg": legacy CLEAN cover — also swept on delete.
        foreach (var suffix in new[] { "-thumb.jpg", "-wt.jpg", "-wc.jpg", "-large.jpg" })
        {
            var file = baseName + suffix;
            if (File.Exists(file))
                File.Delete(file);
        }
    }

    private void DeleteFile(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath) || !relativePath.StartsWith("photos/"))
            return;
        var fullPath = Path.GetFullPath(Path.Combine(PhotosRoot, relativePath["photos/".Length..]));
        if (fullPath.StartsWith(Path.GetFullPath(PhotosRoot) + Path.DirectorySeparatorChar) && File.Exists(fullPath))
            File.Delete(fullPath);
    }
}
