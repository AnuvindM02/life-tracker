using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SelfTracker.Application.Abstractions;
using SelfTracker.Application.Features;
using SelfTracker.Infrastructure.Data;
using SelfTracker.Infrastructure.Files;
using SelfTracker.Infrastructure.Hosting;
using SelfTracker.Infrastructure.Persistence;
using SelfTracker.Infrastructure.Seeding;

namespace SelfTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config, AppPaths paths)
    {
        services.AddSingleton(paths);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(
            DatabaseConnection.Resolve(config),
            sql => sql.EnableRetryOnFailure(3).UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<FileStorageFactory>();
        services.AddScoped<IFileStorage>(sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var root = db.AppSettings.AsNoTracking().OrderBy(s => s.Id).Select(s => s.FileRoot).FirstOrDefault();
            return sp.GetRequiredService<FileStorageFactory>().For(root);
        });
        services.AddScoped<Seeder>();
        services.AddScoped<IDataPorter, DataPorter>();
        services.AddScoped<DatabaseInitializer>();
        services.AddHostedService<TrashPurgeService>();
        return services;
    }
}

/// <summary>Local folders. data/ lives at the repository root (or SELFTRACKER_DATA) and is never committed.</summary>
public sealed class AppPaths(string dataRoot)
{
    public string DataRoot { get; } = Path.GetFullPath(dataRoot);

    public string Backups => Path.Combine(DataRoot, "backups");

    public string Logs => Path.Combine(DataRoot, "..", "logs");

    /// <summary>Resolve a FileRoot setting: absolute as-is, relative to the repository root otherwise.</summary>
    public string ResolveFileRoot(string? fileRoot)
    {
        var value = string.IsNullOrWhiteSpace(fileRoot) ? "data/files" : fileRoot;
        return Path.IsPathRooted(value) ? value : Path.GetFullPath(Path.Combine(DataRoot, "..", value));
    }

    public static AppPaths Detect(string contentRoot)
    {
        var env = Environment.GetEnvironmentVariable("SELFTRACKER_DATA");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return new AppPaths(env);
        }

        var dir = new DirectoryInfo(contentRoot);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SelfTracker.slnx")))
        {
            dir = dir.Parent;
        }

        return new AppPaths(Path.Combine(dir?.FullName ?? contentRoot, "data"));
    }
}

public sealed class FileStorageFactory(AppPaths paths)
{
    private readonly Dictionary<string, LocalFileStorage> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    public LocalFileStorage For(string? fileRoot)
    {
        var full = paths.ResolveFileRoot(fileRoot);
        lock (_lock)
        {
            if (!_cache.TryGetValue(full, out var storage))
            {
                storage = new LocalFileStorage(full);
                _cache[full] = storage;
            }

            return storage;
        }
    }
}

public static class DatabaseConnection
{
    /// <summary>
    /// ConnectionStrings:Default wins; otherwise build one from MSSQL_SA_PASSWORD / DB_PORT / DB_NAME
    /// (the same .env docker-compose uses), so secrets never live in appsettings.
    /// </summary>
    public static string Resolve(IConfiguration config)
    {
        var explicitCs = config.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(explicitCs))
        {
            return explicitCs;
        }

        var password = config["MSSQL_SA_PASSWORD"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("No database configured. Copy .env.example to .env (MSSQL_SA_PASSWORD) or set ConnectionStrings__Default.");
        }

        var host = config["DB_HOST"] ?? "127.0.0.1";
        var port = config["DB_PORT"] ?? "1433";
        var name = config["DB_NAME"] ?? "SelfTracker";
        return $"Server={host},{port};Database={name};User Id=sa;Password={password};TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=False";
    }
}
