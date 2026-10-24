using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.Data;

/// <summary>
/// Applies EF Core Migrations (creates/updates the database) and seeds
/// roles/users and sample content (session types, galleries with images).
/// </summary>
public static class DbSeeder
{
    public const string AdminEmail = "admin@shotsbymarques.pt";
    public const string AdminPassword = "Admin123!";
    public const string DemoClientEmail = "cliente@shotsbymarques.pt";
    public const string DemoClientPassword = "Cliente123!";

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

        // Legacy manually-patched schema blocks were removed — the EF model is
        // the single source of truth and the InitialCreate migration keeps the
        // database in sync with it.

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

        // The demo booking is fully paid (deposit + balance), so the demo private
        // gallery stays downloadable for the demo client.
        var demoBooking = await db.Bookings.FirstOrDefaultAsync(b => b.Notes == "Galeria privada de demonstração");
        if (demoBooking is not null && (!demoBooking.DepositPaid || !demoBooking.BalancePaid))
        {
            demoBooking.DepositPaid = true;
            demoBooking.BalancePaid = true;
            demoBooking.Location = string.IsNullOrEmpty(demoBooking.Location) ? "Porto" : demoBooking.Location;
            await db.SaveChangesAsync();
        }

        // Session photos are included in the session price — never sold separately.
        var sessionGallery = await db.Galleries.FirstOrDefaultAsync(g => g.Title == "Sessões de Carros");
        if (sessionGallery is not null)
        {
            var sessionPhotos = await db.Photos.Where(p => p.GalleryId == sessionGallery.Id && p.IsForSale).ToListAsync();
            foreach (var photo in sessionPhotos)
            {
                photo.IsForSale = false;
                photo.Price = 0m;
            }
            if (sessionPhotos.Count > 0)
                await db.SaveChangesAsync();
        }

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

        if (!await db.SessionTypes.AnyAsync())
        {
            await SeedRolesAsync(roleManager);
            await SeedAdminAsync(userManager);
            await SeedDemoClientAsync(userManager);
            await SeedContentAsync(db, env);
        }

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

    private static async Task SeedDemoClientAsync(UserManager<ApplicationUser> userManager)
    {
        if (await userManager.FindByEmailAsync(DemoClientEmail) is null)
        {
            var client = new ApplicationUser
            {
                UserName = DemoClientEmail,
                Email = DemoClientEmail,
                FirstName = "Cliente",
                LastName = "Demonstração"
            };
            await userManager.CreateAsync(client, DemoClientPassword);
        }
    }

    private static async Task SeedContentAsync(ApplicationDbContext db, IWebHostEnvironment env)
    {
        var photosDir = Path.Combine(env.WebRootPath, "sample-photos");
        Directory.CreateDirectory(photosDir);

        // ---- Session types (prices aligned with the Pixieset price list) ----
        var carSession = new SessionType
        {
            Name = "Sessão de Carros",
            Description = "Sessão dedicada ao teu automóvel — exteriores, detalhes e ambientes. Ideal para anúncios, paixão por carros ou lembranças do projeto.",
            DurationMinutes = 120,
            Price = 45m,
            SortOrder = 1,
            ImagePath = "/sample-photos/cars-cover.svg"
        };
        db.SessionTypes.Add(carSession);

        // ---- Photo packs (discounts on photo orders) ----
        db.PhotoPacks.AddRange(
            new PhotoPack { Name = "Pack 5 Fotos", MinPhotos = 5, DiscountPercent = 10m, SortOrder = 1 },
            new PhotoPack { Name = "Pack 10 Fotos", MinPhotos = 10, DiscountPercent = 15m, SortOrder = 2 },
            new PhotoPack { Name = "Pack 20 Fotos", MinPhotos = 20, DiscountPercent = 20m, SortOrder = 3 });

        // ---- Galleries ----
        // Sessões: as fotos estão incluídas no preço da sessão (não são vendidas à parte).
        var carsGallery = new Gallery
        {
            Title = "Sessões de Carros",
            Subtitle = "Detalhes, curvas e ambientes",
            Description = "Ensaios automóveis — do pormenor ao retrato completo do veículo. As fotos da sessão estão incluídas no preço.",
            EventDate = DateTime.UtcNow.AddMonths(-1),
            CoverPath = "/sample-photos/cars-cover.svg",
            IsPublished = true,
            IsSession = true
        };
        db.Galleries.Add(carsGallery);

        // Eventos: divididos por evento, cada um com as suas fotos (à venda).
        var classicosGallery = new Gallery
        {
            Title = "Encontro de Clássicos — Porto",
            Subtitle = "Clássicos e desportivos no Parque da Cidade",
            Description = "Cobertura do encontro de clássicos no Porto. Escolhe e compra as tuas fotos em alta resolução.",
            EventDate = DateTime.UtcNow.AddMonths(-2),
            CoverPath = "/sample-photos/event-classicos-cover.svg",
            IsPublished = true,
            IsSession = false
        };
        var nightrunGallery = new Gallery
        {
            Title = "Night Run — Lisboa",
            Subtitle = "Carros de rua ao cair da noite",
            Description = "Cobertura da Night Run de Lisboa. Escolhe e compra as tuas fotos em alta resolução.",
            EventDate = DateTime.UtcNow.AddMonths(-1).AddDays(5),
            CoverPath = "/sample-photos/event-nightrun-cover.svg",
            IsPublished = true,
            IsSession = false
        };
        var jdmGallery = new Gallery
        {
            Title = "JDM Gathering — Braga",
            Subtitle = "Japoneses, motores e comunidade",
            Description = "Cobertura do JDM Gathering em Braga. Escolhe e compra as tuas fotos em alta resolução.",
            EventDate = DateTime.UtcNow.AddDays(-12),
            CoverPath = "/sample-photos/event-jdm-cover.svg",
            IsPublished = true,
            IsSession = false
        };
        db.Galleries.AddRange(classicosGallery, nightrunGallery, jdmGallery);

        // Cover / session type images
        WriteImage(Path.Combine(photosDir, "cars-cover.svg"), "#1a1a2e", "#16213e", "SESSÕES DE CARROS");
        WriteImage(Path.Combine(photosDir, "events-cover.svg"), "#2d1b00", "#1a0e00", "EVENTOS");
        WriteImage(Path.Combine(photosDir, "event-classicos-cover.svg"), "#2d1b00", "#1a0e00", "CLÁSSICOS · PORTO");
        WriteImage(Path.Combine(photosDir, "event-nightrun-cover.svg"), "#0b1220", "#060a14", "NIGHT RUN · LISBOA");
        WriteImage(Path.Combine(photosDir, "event-jdm-cover.svg"), "#1a0e1f", "#0d0710", "JDM · BRAGA");

        // ---- Photos ----
        // Sessão de carros — fotos incluídas na sessão (sem preço de venda).
        for (int i = 1; i <= 6; i++)
        {
            var carBase = $"cars-{i}";
            WriteImage(Path.Combine(photosDir, $"{carBase}.svg"), "#101828", $"#{0x23 + i * 7:x2}{0x2e + i * 5:x2}48", $"CAR 0{i}");
            WriteImage(Path.Combine(photosDir, $"{carBase}-wm.svg"), "#101828", $"#{0x23 + i * 7:x2}{0x2e + i * 5:x2}48", $"CAR 0{i}", watermark: true);
            db.Photos.Add(new Photo
            {
                Gallery = carsGallery,
                Title = $"Sessão de carros — foto {i}",
                FilePath = $"/sample-photos/{carBase}.svg",
                PreviewPath = $"/sample-photos/{carBase}-wm.svg",
                Price = 0m,
                SortOrder = i,
                IsForSale = false
            });
        }

        // Eventos — fotos à venda, divididas por evento.
        var eventSeeds = new[]
        {
            (Gallery: classicosGallery, Prefix: "classicos", Label: "CLÁSSICOS", Display: "Clássicos", R: 0x5a, G: 0x2a, B: 0x00, Price: 3m),
            (Gallery: nightrunGallery, Prefix: "nightrun", Label: "NIGHT RUN", Display: "Night Run", R: 0x12, G: 0x22, B: 0x3a, Price: 3m),
            (Gallery: jdmGallery, Prefix: "jdm", Label: "JDM", Display: "JDM", R: 0x2a, G: 0x10, B: 0x38, Price: 3m)
        };
        foreach (var ev in eventSeeds)
        {
            for (int i = 1; i <= 4; i++)
            {
                var evBase = $"event-{ev.Prefix}-{i}";
                var baseColor = $"#{ev.R:x2}{ev.G:x2}{ev.B:x2}";
                var gradColor = $"#{Math.Min(0xf0, ev.R + i * 5):x2}{Math.Min(0xf0, ev.G + i * 3):x2}{Math.Min(0xf0, ev.B + i * 3):x2}";
                WriteImage(Path.Combine(photosDir, $"{evBase}.svg"), baseColor, gradColor, $"{ev.Label} 0{i}");
                WriteImage(Path.Combine(photosDir, $"{evBase}-wm.svg"), baseColor, gradColor, $"{ev.Label} 0{i}", watermark: true);
                db.Photos.Add(new Photo
                {
                    Gallery = ev.Gallery,
                    Title = $"{ev.Display} — foto {i}",
                    FilePath = $"/sample-photos/{evBase}.svg",
                    PreviewPath = $"/sample-photos/{evBase}-wm.svg",
                    Price = ev.Price,
                    SortOrder = i,
                    IsForSale = true
                });
            }
        }

        await db.SaveChangesAsync();

        // ---- Demo paid order (for the downloads page) ----
        var demoClient = await db.Users.FirstOrDefaultAsync(u => u.Email == DemoClientEmail);
        var order = new Order
        {
            OrderNumber = "SBM-DEMO-0001",
            ClientUserId = demoClient!.Id,
            Status = OrderStatus.Paid,
            StripeSessionId = "cs_demo_seed",
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            PaidAt = DateTime.UtcNow.AddDays(-3)
        };
        var demoPhotos = await db.Photos.Where(p => p.IsForSale).Take(5).ToListAsync();
        foreach (var p in demoPhotos)
        {
            order.Items.Add(new OrderItem
            {
                PhotoId = p.Id,
                Title = p.Title,
                Price = p.Price,
                FilePath = p.FilePath
            });
        }
        order.Subtotal = order.Items.Sum(i => i.Price);

        // Demo shows the photo pack discount (5+ photos → 10% off).
        var packs = await db.PhotoPacks.ToListAsync();
        var packResult = ShotsByMarques.Web.Services.PackService.BestPack(packs, order.Items.Count, order.Subtotal);
        order.DiscountAmount = packResult.Discount;
        order.PackName = packResult.PackName;
        order.Total = packResult.Total;
        db.Orders.Add(order);

        // ---- Private gallery linked to a booking (photo delivery demo) ----
        var booking = new Booking
        {
            ClientUserId = demoClient.Id,
            SessionTypeId = carSession.Id,
            Start = DateTime.UtcNow.AddDays(7).Date.AddHours(10),
            Status = BookingStatus.Confirmed,
            Price = carSession.Price,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            Notes = "Galeria privada de demonstração"
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        // Photos are INCLUDED in the session price — the client already paid the
        // session, so these are free to download (no watermark, no extra payment).
        var privateGallery = new Gallery
        {
            Title = "A tua sessão — BMW M3",
            Subtitle = "Galeria privada da tua marcação",
            Description = "As fotos da tua sessão. Só tu consegues ver e descarregar esta galeria — já estão incluídas no preço da sessão.",
            EventDate = DateTime.UtcNow.AddDays(7),
            CoverPath = "/sample-photos/cars-cover.svg",
            IsPublished = true,
            IsPrivate = true,
            IsSession = true,
            BookingId = booking.Id
        };
        db.Galleries.Add(privateGallery);
        for (int i = 1; i <= 4; i++)
        {
            var baseName = $"cars-{i}";
            db.Photos.Add(new Photo
            {
                Gallery = privateGallery,
                Title = $"BMW M3 — foto {i}",
                FilePath = $"/sample-photos/{baseName}.svg",
                PreviewPath = $"/sample-photos/{baseName}-wm.svg",
                Price = 0m,
                SortOrder = i,
                IsForSale = false
            });
        }
        await db.SaveChangesAsync();
    }

    private static void WriteImage(string path, string colorA, string colorB, string label, bool watermark = false)
    {
        if (File.Exists(path)) return;

        var wm = watermark
            ? "<g transform='rotate(-18 600 400)'><text x='600' y='400' font-family='Arial' font-size='44' fill='rgba(255,255,255,0.22)' text-anchor='middle'>© Shots By Marques</text></g>"
            : string.Empty;

        var svg = $@"<svg xmlns='http://www.w3.org/2000/svg' width='1200' height='800' viewBox='0 0 1200 800'>
<defs><linearGradient id='g' x1='0' y1='0' x2='1' y2='1'><stop offset='0' stop-color='{colorA}'/><stop offset='1' stop-color='{colorB}'/></linearGradient></defs>
<rect width='1200' height='800' fill='url(#g)'/>
<circle cx='600' cy='400' r='150' fill='rgba(255,255,255,0.06)'/>
<rect x='440' y='330' width='320' height='200' rx='14' fill='rgba(255,255,255,0.08)'/>
<circle cx='600' cy='410' r='40' fill='rgba(255,255,255,0.16)'/>
<text x='600' y='300' font-family='Arial' font-size='42' fill='rgba(255,255,255,0.9)' text-anchor='middle'>{label}</text>
<text x='600' y='730' font-family='Arial' font-size='22' fill='rgba(255,255,255,0.5)' text-anchor='middle'>Shots By Marques</text>
{wm}</svg>";
        File.WriteAllText(path, svg);
    }
}
