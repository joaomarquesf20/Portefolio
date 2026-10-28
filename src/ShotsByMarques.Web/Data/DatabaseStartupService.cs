namespace ShotsByMarques.Web.Data;

/// <summary>
/// Applies pending EF Core Migrations (creating the database on first run) and
/// seeds the essential admin account when the application host actually starts.
/// Registered as an IHostedService so that design-time tooling (`dotnet ef`,
/// Visual Studio designers) — which builds the host without ever starting it —
/// does not mutate the database as a side effect.
/// </summary>
public sealed class DatabaseStartupService(
    IServiceProvider services,
    ILogger<DatabaseStartupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = services.CreateScope();
            await DbSeeder.SeedAsync(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration/seeding failed. Check the database connection string in appsettings.json.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
