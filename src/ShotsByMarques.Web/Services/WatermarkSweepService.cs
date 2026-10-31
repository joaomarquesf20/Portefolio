using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;

namespace ShotsByMarques.Web.Services;

/// <summary>
/// ONE-TIME derivative sweep (runs once per machine, marker-gated in
/// App_Data/photos). Guarantees the countersignature rule "no clean public
/// photo pixels anywhere":
///   a) deletes legacy CLEAN "-large.jpg" derivatives baked by the old
///      /Photos/Cover (pre-watermark covers, e.g. Expoclássicos / Caramulo);
///   b) RE-BAKES all watermarked derivatives ("-wm.jpg" 1600px, "-wt.jpg"
///      900px, "-wc.jpg" 1920px) from the untouched originals, overwriting
///      any legacy derivative file that may have been produced before the
///      watermarked pipeline landed;
///   c) normalizes PreviewPath rows to the watermarked "-wm.jpg" file.
/// Re-bake is tmp-file + atomic move so a viewer mid-download never gets a
/// truncated JPEG while rendering. Subsequent startups skip the sweep — the
/// admin upload/update/publish flows and the /Photos endpoints keep things
/// self-healing afterwards. Delete the marker file to force a full re-sweep.
/// Administrative covers (gallery covers / session-type images in
/// wwwroot/uploads/covers) are deliberately untouched: they are marketing
/// assets chosen by the owner, never derivatives of client photos.
/// </summary>
public sealed class WatermarkSweepService : Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<WatermarkSweepService> _logger;

    public WatermarkSweepService(IServiceScopeFactory scopes, ILogger<WatermarkSweepService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var storage = scope.ServiceProvider.GetRequiredService<PhotoStorageService>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await RunSweepAsync(storage, db, stoppingToken);
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Watermark sweep failed; the /Photos endpoints remain self-healing.");
        }
    }

    private async Task RunSweepAsync(PhotoStorageService storage, ApplicationDbContext db, CancellationToken token)
    {
        // tmp file + atomic Move: a viewer mid-download never sees a
        // truncated JPEG while the re-bake overwrites an existing file.
        static void Rebake(string original, string baseName, string suffix, int maxDim, int quality)
        {
            var target = baseName + suffix;
            var tmp = target + ".tmp-sweep";
            PhotoStorageService.GenerateWatermarkedDerivative(original, tmp, maxDim, quality);
            if (File.Exists(tmp))
            {
                if (File.Exists(target)) File.Delete(target);
                File.Move(tmp, target);
            }
        }

        var root = storage.PhotosRoot;
        if (!Directory.Exists(root))
            return;

        var marker = Path.Combine(root, ".wm-sweep-v2");
        if (File.Exists(marker))
            return;

        var photos = await db.Photos
            .Where(p => p.FilePath != null && p.FilePath.StartsWith("photos/"))
            .ToListAsync(token);

        var rewritten = 0;
        foreach (var photo in photos)
        {
            token.ThrowIfCancellationRequested();

            var original = Path.GetFullPath(Path.Combine(root, photo.FilePath["photos/".Length..]));
            if (!original.StartsWith(root + Path.DirectorySeparatorChar) || !File.Exists(original))
                continue;

            var baseName = Path.Combine(
                Path.GetDirectoryName(original) ?? root,
                Path.GetFileNameWithoutExtension(original));

            // (a) legacy CLEAN cover derivative of the old /Photos/Cover.
            var legacyClean = baseName + "-large.jpg";
            if (File.Exists(legacyClean))
            {
                try { File.Delete(legacyClean); }
                catch { /* locked/temporary — the next remove-sweep will retry */ }
            }

            // (b) re-bake all watermarked derivatives (tmp + atomic Move).
            try
            {
                Rebake(original, baseName, "-wm.jpg", 1600, 88);
                Rebake(original, baseName, "-wt.jpg", 900, 82);
                Rebake(original, baseName, "-wc.jpg", 1920, 88);
            }
            catch
            {
                continue; // keep serving — endpoints self-heal this file later
            }

            // (c) DB drift: PreviewPath must point at the watermarked file.
            var relBase = "photos/" + Path.GetRelativePath(root, baseName).Replace('\\', '/');
            var wmRelative = relBase + "-wm.jpg";
            if (photo.PreviewPath != wmRelative)
            {
                photo.PreviewPath = wmRelative;
                rewritten++;
            }

            await Task.Delay(10, token); // ease CPU on cold boot with big galleries
        }

        if (rewritten > 0)
            await db.SaveChangesAsync(token);

        await File.WriteAllTextAsync(marker, $"v2 {DateTime.UtcNow:O}", token);
        _logger.LogInformation("Watermark sweep done: {PhotoCount} photo(s) checked, {Rewritten} PreviewPath row(s) normalized.", photos.Count, rewritten);
    }
}
