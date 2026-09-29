using Microsoft.EntityFrameworkCore;
using SrSimplifyRoutine.Web.Data;

namespace SrSimplifyRoutine.Web.Services;

public sealed class UnverifiedAccountCleanup(IServiceScopeFactory scopes, ILogger<UnverifiedAccountCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
                await using var db = await factory.CreateDbContextAsync(stoppingToken);
                var cutoff = DateTime.UtcNow.AddDays(-7);
                var removed = await db.Users.Where(x => !x.EmailConfirmed && x.CreatedUtc < cutoff).ExecuteDeleteAsync(stoppingToken);
                if (removed > 0) logger.LogInformation("Removed {Count} expired unverified accounts.", removed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Unverified-account cleanup failed."); }
        }
    }
}
