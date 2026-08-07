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

        // Watermarked preview (always JPEG, resized).
        // If the watermark fails we DELETE the upload and let the error propagate —
        // the preview must NEVER expose the full-resolution original.
        var previewName = $"{Path.GetFileNameWithoutExtension(fileName)}-wm.jpg";
        var previewPath = Path.Combine(dir, previewName);
        try
        {
            GenerateWatermarkedPreview(path, previewPath);
        }
        catch
        {
            File.Delete(path);
            throw;
        }

        return ($"photos/{galleryId}/{fileName}", $"photos/{galleryId}/{previewName}");
    }

    /// <summary>
    /// Resizes the image to PreviewMaxDimension and stamps a tiled diagonal
    /// watermark so the preview clearly belongs to Shots By Marques.
    /// </summary>
    public static void GenerateWatermarkedPreview(string sourcePath, string targetPath)
    {
        using var source = SKBitmap.Decode(sourcePath)
            ?? throw new InvalidOperationException("Could not decode image.");

        // Scale down keeping aspect ratio.
        var scale = Math.Min(1f, PreviewMaxDimension / (float)Math.Max(source.Width, source.Height));
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
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 88);
        using var output = File.Create(targetPath);
        data.SaveTo(output);
    }

    /// <summary>
    /// Saves a public gallery cover into wwwroot (covers are meant to be public).
    /// </summary>
    public async Task<string> SaveCoverAsync(IFormFile file)
    {
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

    /// <summary>
    /// Deletes both the full-resolution file and its watermarked preview.
    /// </summary>
    public void Delete(string? relativePath, string? previewPath = null)
    {
        DeleteFile(relativePath);
        DeleteFile(previewPath);
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
