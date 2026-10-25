using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using ShotsByMarques.Web.Data;
using ShotsByMarques.Web.Models;
using ShotsByMarques.Web.Resources;
using ShotsByMarques.Web.Services;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
    
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
});

// ---- Localization (pt-PT default, en-US optional) ----
// Resources are embedded with their full namespace name (ShotsByMarques.Web.Resources.SharedResource)
builder.Services.AddLocalization();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("pt-PT"),
        new CultureInfo("en-US")
    };
    options.DefaultRequestCulture = new RequestCulture("pt-PT");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders.Remove(
        options.RequestCultureProviders.OfType<Microsoft.AspNetCore.Localization.AcceptLanguageHeaderRequestCultureProvider>().First());
});

builder.Services.AddControllersWithViews();

// ---- Large photo uploads ----
// 66+ high-resolution photos in one request can easily exceed Kestrel's
// default 30 MB request body limit, which resets the connection mid-upload.
// Allow up to 2 GB per request and drop the multipart form limit to match.
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 2L * 1024 * 1024 * 1024; // 2 GB
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 2L * 1024 * 1024 * 1024; // 2 GB
});

// ---- Application services ----
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<StripeCheckoutService>();
builder.Services.AddScoped<PhotoStorageService>();
builder.Services.AddScoped<AvailabilityCalendarService>();
builder.Services.AddScoped<SitePageBackgroundService>();
builder.Services.AddSingleton<BookingCalendarService>();

// Travel fee settings (base location, free radius, price per extra km)
builder.Services.Configure<ShotsByMarques.Web.Models.TravelSettings>(
    builder.Configuration.GetSection("Travel"));

// Applies EF Core Migrations + seeds the database when the host actually starts.
// Implemented as IHostedService so design-time tooling (`dotnet ef`) — which
// builds the host without starting it — never performs database operations
// as a side effect.
builder.Services.AddHostedService<ShotsByMarques.Web.Data.DatabaseStartupService>();

var app = builder.Build();

// Migrations + seeding run inside DatabaseStartupService (IHostedService) once
// the host starts for real.

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
