using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SelfTracker.Application.Abstractions;
using SelfTracker.Infrastructure;
using SelfTracker.Infrastructure.Persistence;
using Serilog.Context;

namespace SelfTracker.Api.Infrastructure;

/// <summary>X-Correlation-ID in and out; pushed into every log line for the request.</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string Header = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext ctx)
    {
        var id = ctx.Request.Headers[Header].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64)
        {
            id = Guid.NewGuid().ToString("N")[..16];
        }

        ctx.TraceIdentifier = id;
        ctx.Response.Headers[Header] = id;
        using (LogContext.PushProperty("CorrelationId", id))
        {
            await next(ctx);
        }
    }
}

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext ctx)
    {
        ctx.Response.Headers.XContentTypeOptions = "nosniff";
        ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
        ctx.Response.Headers.XFrameOptions = "SAMEORIGIN";
        return next(ctx);
    }
}

/// <summary>
/// Local-request guard (spec v1.1, 6.2). There are no cookies or auth, so instead of antiforgery tokens:
/// (1) Host must be a loopback name (blocks DNS rebinding);
/// (2) on unsafe methods an Origin header, if present, must be a loopback origin;
/// (3) unsafe methods must carry X-SelfTracker: 1, which a cross-site HTML form cannot send and which forces a CORS preflight.
/// </summary>
public sealed class LocalRequestGuardMiddleware(RequestDelegate next)
{
    public const string Header = "X-SelfTracker";

    private static readonly HashSet<string> LoopbackHosts = new(StringComparer.OrdinalIgnoreCase) { "localhost", "127.0.0.1", "[::1]", "::1" };

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!LoopbackHosts.Contains(ctx.Request.Host.Host))
        {
            await Reject(ctx, StatusCodes.Status400BadRequest, "Requests must target localhost.");
            return;
        }

        var unsafeMethod = !(HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method) || HttpMethods.IsOptions(ctx.Request.Method));
        if (unsafeMethod && ctx.Request.Path.StartsWithSegments("/api"))
        {
            var origin = ctx.Request.Headers.Origin.FirstOrDefault();
            if (!string.IsNullOrEmpty(origin) && !(Uri.TryCreate(origin, UriKind.Absolute, out var o) && LoopbackHosts.Contains(o.Host)))
            {
                await Reject(ctx, StatusCodes.Status403Forbidden, "Cross-origin requests are not allowed.");
                return;
            }

            if (ctx.Request.Headers[Header] != "1")
            {
                await Reject(ctx, StatusCodes.Status403Forbidden, $"Missing {Header} header. Use the app's own client.");
                return;
            }
        }

        await next(ctx);
    }

    private static Task Reject(HttpContext ctx, int status, string title)
    {
        ctx.Response.StatusCode = status;
        return ctx.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title }, (JsonSerializerOptions?)null, "application/problem+json");
    }
}

/// <summary>Maps application errors to RFC 9457 ProblemDetails.</summary>
public sealed class AppExceptionHandler(IProblemDetailsService problems, ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        ProblemDetails? pd = ex switch
        {
            NotFoundException => new ProblemDetails { Status = 404, Title = ex.Message },
            ConflictException => new ProblemDetails { Status = 409, Title = ex.Message },
            BadRequestException => new ProblemDetails { Status = 400, Title = ex.Message },
            FileTooLargeException => new ProblemDetails { Status = 413, Title = ex.Message },
            RuleViolationException rv => new ValidationProblemDetails(rv.Errors) { Status = 422, Title = rv.Message },
            DbUpdateConcurrencyException => new ProblemDetails
            {
                Status = 409,
                Title = "This record changed elsewhere. Reload it, or overwrite with your version.",
                Type = "https://selftracker.local/problems/concurrency",
            },
            DbUpdateException { InnerException: Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 } } =>
                new ProblemDetails { Status = 409, Title = "A record with the same unique value already exists." },
            BadHttpRequestException bad => new ProblemDetails { Status = bad.StatusCode, Title = bad.Message },
            OperationCanceledException when ctx.RequestAborted.IsCancellationRequested => new ProblemDetails { Status = 499, Title = "Request cancelled." },
            _ => null,
        };

        if (pd is null)
        {
            logger.LogError(ex, "Unhandled error on {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            pd = new ProblemDetails { Status = 500, Title = "Unexpected error. See logs/ with the correlation id." };
        }

        ctx.Response.StatusCode = pd.Status ?? 500;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = ctx, ProblemDetails = pd, Exception = ex });
    }
}

public sealed class DatabaseReadyCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            return HealthCheckResult.Unhealthy("Database not reachable.");
        }

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
        return pending == 0 ? HealthCheckResult.Healthy("Connected; migrations applied.") : HealthCheckResult.Unhealthy($"{pending} migrations pending.");
    }
}

public sealed class FileRootCheck(IFileStorage storage) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!storage.IsWritable())
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"File root {storage.Root} is not writable."));
        }

        var free = storage.FreeBytes();
        return Task.FromResult(free is < 1024L * 1024 * 1024
            ? HealthCheckResult.Degraded($"Less than 1 GB free on the file root disk ({free / (1024 * 1024)} MB).")
            : HealthCheckResult.Healthy($"Writable; {free / (1024 * 1024 * 1024)} GB free."));
    }
}

public static class HealthWriter
{
    public static Task WriteAsync(HttpContext ctx, HealthReport report)
    {
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString(), description = e.Value.Description }),
        });
    }
}

/// <summary>Reads KEY=VALUE pairs from the repo .env (the same file docker-compose uses) without overriding real env vars.</summary>
public static class DotEnv
{
    public static void AddTo(ConfigurationManager config, string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var values = new Dictionary<string, string?>();
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || !trimmed.Contains('='))
            {
                continue;
            }

            var key = trimmed[..trimmed.IndexOf('=')].Trim();
            var value = trimmed[(trimmed.IndexOf('=') + 1)..].Trim().Trim('"');
            if (Environment.GetEnvironmentVariable(key) is null && config[key] is null)
            {
                values[key] = value;
            }
        }

        config.AddInMemoryCollection(values);
    }
}
