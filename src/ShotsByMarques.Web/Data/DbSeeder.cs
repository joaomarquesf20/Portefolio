using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.Data;

/// <summary>
/// Applies EF Core Migrations (creates/updates the database) and seeds the
/// essential admin account. No demo content is seeded: galleries, photos,
/// orders, bookings and demo users are entirely user-managed from the admin
/// area, so anything deleted there stays deleted.
/// </summary>
public static class DbSeeder
{
    public const string AdminEmail = "marquesjoao2006@gmail.com";
    public const string AdminPassword = "Admin123!";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var env = services.GetRequiredService<IWebHostEnvironment>();

        // Applies pending EF Core Migrations — creates the DB on first run and
        // any schema change afterwards. All schema evolution must go through
        // `dotnet ef migrations add ...` (no more manual SQL patches here).
        await db.Database.MigrateAsync();

        // ---- Cleanup on every startup (keeps existing dev databases in sync) ----
        // Only car photography is offered — portrait/lifestyle sessions are no longer available.
        var legacyLifestyle = await db.SessionTypes
            .Where(s => s.Name == "Sessão Lifestyle" || s.Name == "Cobertura de Eventos")
            .ToListAsync();
        foreach (var legacy in legacyLifestyle)
        {
            // If a booking still references it, deactivate instead of deleting (FK Restrict).
            var referenced = await db.Bookings.AnyAsync(b => b.SessionTypeId == legacy.Id);
            if (referenced) legacy.IsActive = false;
            else db.SessionTypes.Remove(legacy);
        }
        if (legacyLifestyle.Count > 0)
            await db.SaveChangesAsync();

        var lifestyleImg = Path.Combine(env.WebRootPath, "sample-photos", "lifestyle-cover.svg");
        if (File.Exists(lifestyleImg)) File.Delete(lifestyleImg);

        // Keep the explicit gallery type in sync for pre-existing dev databases:
        // galleries linked to a booking (or with no photos for sale) are sessions.
        var mislabelled = await db.Galleries
            .Include(g => g.Photos)
            .Where(g => !g.IsSession && (g.BookingId != null || !g.Photos.Any(p => p.IsForSale)))
            .ToListAsync();
        foreach (var g in mislabelled)
            g.IsSession = true;
        if (mislabelled.Count > 0)
            await db.SaveChangesAsync();

        // ---- First-run defaults (brand-new database only) ----
        // Offering configuration (a starting session type + photo packs) is
        // seeded once so a fresh install can take bookings immediately. It is
        // never re-created afterwards — even if every session type is deleted,
        // the offering is rebuilt manually from the admin.
        if (!await db.Users.AnyAsync())
        {
            db.SessionTypes.Add(new SessionType
            {
                Name = "Sessão de Carros",
                Description = "Sessão dedicada ao teu automóvel — exteriores, detalhes e ambientes. Ideal para anúncios, paixão por carros ou lembranças do projeto.",
                DurationMinutes = 120,
                Price = 45m,
                SortOrder = 1,
                ImagePath = "/sample-photos/cars-cover.svg"
            });
            db.PhotoPacks.AddRange(
                new PhotoPack { Name = "Pack 5 Fotos", MinPhotos = 5, DiscountPercent = 10m, SortOrder = 1 },
                new PhotoPack { Name = "Pack 10 Fotos", MinPhotos = 10, DiscountPercent = 15m, SortOrder = 2 },
                new PhotoPack { Name = "Pack 20 Fotos", MinPhotos = 20, DiscountPercent = 20m, SortOrder = 3 });
            await db.SaveChangesAsync();
        }

        // ---- Essential accounts (idempotent on every startup) ----
        await SeedRolesAsync(roleManager);
        await SeedAdminAsync(userManager);

        // Mini-session type — required for event-day mini bookings (created on
        // fresh and existing databases). Price/duration are editable in the admin.
        var miniSessionType = await db.SessionTypes.FirstOrDefaultAsync(s => s.IsMiniSessionType);
        if (miniSessionType is null)
        {
            db.SessionTypes.Add(new SessionType
            {
                Name = "Mini Sessão — Evento",
                Description = "Mini sessão no dia de um evento, só para o teu carro. As fotos ficam incluídas no preço.",
                DurationMinutes = 30,
                Price = 25m,
                SortOrder = 99,
                IsActive = true,
                IsMiniSessionType = true
            });
            await db.SaveChangesAsync();
        }
        else if (miniSessionType.Description.Contains("30 min", StringComparison.OrdinalIgnoreCase)
                 || miniSessionType.Description.Contains("≈30", StringComparison.OrdinalIgnoreCase))
        {
            miniSessionType.Description = "Mini sessão no dia de um evento, só para o teu carro. As fotos ficam incluídas no preço.";
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));
    }

    private static async Task SeedAdminAsync(UserManager<ApplicationUser> userManager)
    {
        if (await userManager.FindByEmailAsync(AdminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                FirstName = "João",
                LastName = "Marques"
            };
            var result = await userManager.CreateAsync(admin, AdminPassword);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}
