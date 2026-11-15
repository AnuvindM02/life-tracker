using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using SelfTracker.Api.Endpoints;
using SelfTracker.Api.Infrastructure;
using SelfTracker.Application;
using SelfTracker.Infrastructure;
using SelfTracker.Infrastructure.Hosting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var paths = AppPaths.Detect(builder.Environment.ContentRootPath);
DotEnv.AddTo(builder.Configuration, Path.Combine(paths.DataRoot, "..", ".env"));

builder.Host.UseSerilog((ctx, services, log) => log
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {CorrelationId}{NewLine}{Exception}")
    .WriteTo.File(Path.Combine(paths.DataRoot, "..", "logs", "selftracker-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14));

// Localhost only (spec section 2). Any configured non-loopback URL is replaced.
// Inside the app container (SELFTRACKER_LISTEN_ANY=true) Kestrel must accept the Docker port mapping; compose publishes
// that port on 127.0.0.1 only, and the local-request guard still requires a loopback Host header (DECISIONS D-020).
var port = builder.Configuration.GetValue<int?>("API_PORT") ?? 5080;
var listenAny = builder.Configuration.GetValue<bool>("SELFTRACKER_LISTEN_ANY");
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.WebHost.ConfigureKestrel(k =>
    {
        if (listenAny)
        {
            k.ListenAnyIP(port);
        }
        else
        {
            k.ListenLocalhost(port);
        }

        k.Limits.MaxRequestBodySize = 30 * 1024 * 1024; // raised per request on the upload endpoint only
    });
}

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict; // numbers are numbers in the OpenAPI contract
});
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
{
    c.ProblemDetails.Extensions["correlationId"] = c.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddValidation();
builder.Services.AddOpenApi(o => o.AddDocumentTransformer((doc, _, _) =>
{
    doc.Info.Title = "Project Self Tracker API";
    doc.Info.Version = "v1";
    return Task.CompletedTask;
}));
builder.Services.AddCors(o => o.AddPolicy("vite", p => p
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition", "X-Correlation-ID")));
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadyCheck>("database", tags: ["ready"])
    .AddCheck<FileRootCheck>("file-root", tags: ["ready"]);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, paths);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(o => o.GetLevel = (ctx, _, ex) =>
    ex is not null || ctx.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
    : ctx.Request.Path.StartsWithSegments("/health") ? Serilog.Events.LogEventLevel.Verbose
    : Serilog.Events.LogEventLevel.Information);
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<LocalRequestGuardMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseCors("vite");
}

app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference("/scalar");
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready"), ResponseWriter = HealthWriter.WriteAsync });

var api = app.MapGroup("/api/v1");
api.MapSettingsEndpoints();
api.MapPlanEndpoints();
api.MapScheduleEndpoints();
api.MapDailyLogEndpoints();
api.MapFileEndpoints();
api.MapTrashEndpoints();
api.MapViewEndpoints();
api.MapDataEndpoints();
api.MapDsaEndpoints();
api.MapDdiaEndpoints();
api.MapPlanExtraEndpoints();
api.MapReviewEndpoints();
app.MapFallback("/api/{**rest}", () => Results.Problem(statusCode: 404, title: "No such API route."));

// Local "prod": the built SPA is served from wwwroot (npm run build writes there).
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

var migrate = app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing") || args.Contains("--migrate");
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(migrate, CancellationToken.None);
}

if (args.Contains("--migrate-only"))
{
    return;
}

await app.RunAsync();

public partial class Program;
