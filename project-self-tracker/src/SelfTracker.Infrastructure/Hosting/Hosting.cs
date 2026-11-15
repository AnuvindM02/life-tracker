using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SelfTracker.Application.Features;
using SelfTracker.Infrastructure.Persistence;
using SelfTracker.Infrastructure.Seeding;

namespace SelfTracker.Infrastructure.Hosting;

/// <summary>Waits for SQL Server (retry with backoff), applies migrations when asked, then seeds (spec 3.2, 11).</summary>
public sealed class DatabaseInitializer(AppDbContext db, Seeder seeder, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(bool migrate, CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(1);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (migrate)
                {
                    await db.Database.MigrateAsync(ct);
                }
                else if (!await db.Database.CanConnectAsync(ct))
                {
                    throw new InvalidOperationException("Database is not reachable yet.");
                }

                break;
            }
            catch (Exception ex) when (attempt < 8 && ex is SqlException or InvalidOperationException or TimeoutException)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}): {Message}. Retrying in {Delay}s", attempt, ex.Message, delay.TotalSeconds);
                await Task.Delay(delay, ct);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 15));
            }
        }

        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count > 0)
        {
            logger.LogWarning("{Count} migrations are pending. Start with --migrate (or in Development) to apply them.", pending.Count);
            return;
        }

        await seeder.SeedAsync(ct);
    }
}

/// <summary>Purges Trash items older than TrashRetentionDays: once shortly after startup, then daily (spec 4.15).</summary>
public sealed class TrashPurgeService(IServiceScopeFactory scopes, TimeProvider time, ILogger<TrashPurgeService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), time, stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromHours(24), time);
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var settings = await db.AppSettings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
            if (settings is null)
            {
                return 0;
            }

            var purger = scope.ServiceProvider.GetRequiredService<Purger>();
            var count = await purger.PurgeExpiredAsync(time.GetUtcNow().AddDays(-settings.TrashRetentionDays), ct);
            if (count > 0)
            {
                logger.LogInformation("Trash purge removed {Count} expired items", count);
            }

            return count;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Trash purge failed");
            return 0;
        }
    }
}
