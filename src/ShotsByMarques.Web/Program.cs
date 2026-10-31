using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
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
    options.SignIn.RequireConfirmedAccount = true;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
});

// ---- External authentication (Google) ----
// Credentials come from configuration only - never hardcoded. In Development
// they live in user-secrets (`dotnet user-secrets set ...`); in production
// they arrive as environment variables (Authentication__Google__ClientId /
// Authentication__Google__ClientSecret). When either value is missing the
// Google scheme is simply not registered and the "Entrar com Google" button
// stays hidden, so the app keeps working unconfigured.
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication().AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/signin-google";
    });
}

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
    options.ValueCountLimit = 4096; // many files in one multipart upload
});

// ---- Application services ----
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<DiscountCodeService>();
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

// ---- Reverse proxy (Render) ----
// Render's edge proxy terminates SSL and forwards requests to the container
// over plain HTTP, adding X-Forwarded-For / X-Forwarded-Proto headers. Trust
// those headers so the app sees the real client IP and the correct https
// scheme. The proxy connects from a dynamic private address, so the known
// proxy/network lists are cleared to accept the headers from any hop.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // X-ForwardedHost: the proxy also forwards the PUBLIC host header (e.g. an
    // ngrok domain). Without it, Request.Host (and any absolute redirect URL
    // built from it) would still be the local bind address — the classic
    // "Stripe redirects me to localhost" bug on mobile.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Process the proxy's forwarded headers first, before any middleware that
// reads the request scheme or client IP (HSTS, cookies, redirects).
app.UseForwardedHeaders();

// Migrations + seeding run inside DatabaseStartupService (IHostedService) once
// the host starts for real.

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// No app-level HTTPS redirection: Render's edge proxy terminates SSL and
// already redirects HTTP to HTTPS, while the container itself has no TLS
// endpoint - UseHttpsRedirection could only log "Failed to determine the
// https port for redirect." without ever being able to act on it.
app.UseStaticFiles();
app.UseRouting();
app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
