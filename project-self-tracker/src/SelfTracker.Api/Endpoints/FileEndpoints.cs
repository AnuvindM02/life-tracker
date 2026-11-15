using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using SelfTracker.Application.Abstractions;
using SelfTracker.Application.Features;
using SelfTracker.Domain.Common;
using SelfTracker.Infrastructure;

namespace SelfTracker.Api.Endpoints;

public sealed record BackupInfo(string FileName, long SizeBytes, DateTimeOffset CreatedAt);

public static class FileEndpoints
{
    public static void MapFileEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/files").WithTags("Files");

        g.MapPost("/", UploadAsync)
            .DisableAntiforgery() // local-request guard replaces antiforgery; see DECISIONS.md D-008
            .Accepts<IFormFileCollection>("multipart/form-data")
            .Produces<IReadOnlyList<UploadItemResult>>();

        g.MapGet("/", (string? q, FileKind? kind, string? entityType, int? entityId, string? tag, bool? unattached, [AsParameters] Paging paging,
            IHandler<ListFiles, PagedResult<FileAssetDto>> h, CancellationToken ct) =>
            h.HandleAsync(new ListFiles(q, kind, entityType, entityId, tag, unattached, paging.ToQuery()), ct));
        g.MapGet("/stats", (IHandler<GetFileStats, FileStatsDto> h, CancellationToken ct) => h.HandleAsync(new GetFileStats(), ct));
        g.MapPost("/maintenance/verify", (IHandler<VerifyFiles, VerifyReport> h, CancellationToken ct) => h.HandleAsync(new VerifyFiles(), ct));
        g.MapGet("/{id:int}", (int id, IHandler<GetFile, FileAssetDto> h, CancellationToken ct) => h.HandleAsync(new GetFile(id), ct));
        g.MapPatch("/{id:int}", (int id, UpdateFileRequest body, IHandler<UpdateFile, FileAssetDto> h, CancellationToken ct) =>
            h.HandleAsync(new UpdateFile(id, body), ct));
        g.MapDelete("/{id:int}", async (int id, IHandler<DeleteFile, bool> h, CancellationToken ct) =>
        {
            await h.HandleAsync(new DeleteFile(id), ct);
            return TypedResults.NoContent();
        });
        g.MapPost("/{id:int}/links", (int id, AddFileLinkRequest body, IHandler<AddFileLink, FileAssetDto> h, CancellationToken ct) =>
            h.HandleAsync(new AddFileLink(id, body), ct));
        g.MapDelete("/{id:int}/links", (int id, string entityType, int entityId, IHandler<RemoveFileLink, FileAssetDto> h, CancellationToken ct) =>
            h.HandleAsync(new RemoveFileLink(id, entityType, entityId), ct));

        g.MapGet("/{id:int}/content", async (int id, HttpContext ctx, IHandler<OpenFile, FileContent> h, CancellationToken ct) =>
        {
            var c = await h.HandleAsync(new OpenFile(id), ct);
            // Belt and braces: even an allowed inline type cannot run scripts.
            ctx.Response.Headers.ContentSecurityPolicy = c.ContentType.StartsWith("text/", StringComparison.Ordinal)
                ? "default-src 'none'; style-src 'unsafe-inline'; sandbox"
                : "default-src 'none'; img-src 'self'; media-src 'self'; object-src 'self'; style-src 'unsafe-inline'";
            if (!c.Inline)
            {
                return Results.File(c.Stream, "application/octet-stream", c.OriginalName, enableRangeProcessing: true);
            }

            ctx.Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline") { FileNameStar = c.OriginalName }.ToString();
            return Results.Stream(c.Stream, c.ContentType, enableRangeProcessing: true);
        });

        g.MapGet("/{id:int}/download", async (int id, IHandler<OpenFile, FileContent> h, CancellationToken ct) =>
        {
            var c = await h.HandleAsync(new OpenFile(id), ct);
            return Results.File(c.Stream, c.ContentType.Split(';')[0], c.OriginalName, enableRangeProcessing: true);
        });
    }

    /// <summary>Streaming multipart upload: large videos never load fully into memory (spec 6.2).</summary>
    private static async Task<IResult> UploadAsync(
        HttpContext ctx,
        string? entityType,
        int? entityId,
        string? label,
        FileUploadService uploads,
        PlanContext plan,
        CancellationToken ct)
    {
        if (!MediaTypeHeaderValue.TryParse(ctx.Request.ContentType, out var mediaType)
            || !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase)
            || HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value is not { Length: > 0 } boundary)
        {
            return Results.Problem(statusCode: 400, title: "Expected multipart/form-data with one or more files.");
        }

        var settings = (await plan.LoadAsync(ct)).Settings;
        if (ctx.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            // Per-file limit is enforced while streaming; allow several max-size files per request.
            limit.MaxRequestBodySize = (long)settings.MaxUploadMb * 1024 * 1024 * 10;
        }

        var target = new UploadTarget(entityType, entityId, label);
        await uploads.ValidateTargetAsync(target, ct);

        var reader = new MultipartReader(boundary, ctx.Request.Body);
        var results = new List<UploadItemResult>();
        while (await reader.ReadNextSectionAsync(ct) is { } section)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var cd) || !cd.IsFileDisposition())
            {
                continue;
            }

            var name = cd.FileNameStar.HasValue ? cd.FileNameStar.Value : HeaderUtilities.RemoveQuotes(cd.FileName).Value;
            results.Add(await uploads.StoreAsync(section.Body, name, target, ct));
        }

        return results.Count == 0
            ? Results.Problem(statusCode: 400, title: "No files found in the upload.")
            : Results.Ok(results);
    }

    public static void MapDataEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/data").WithTags("Data");
        g.MapPost("/export", (IDataPorter porter, AppPaths paths, CancellationToken ct) => porter.ExportAsync(paths.Backups, ct));
        g.MapPost("/backups", (IDataPorter porter, AppPaths paths, CancellationToken ct) => porter.ExportAsync(paths.Backups, ct));
        g.MapGet("/backups", (AppPaths paths) => Directory.Exists(paths.Backups)
            ? Directory.GetFiles(paths.Backups, "selftracker-backup-*.zip")
                .Select(f => new FileInfo(f)).OrderByDescending(f => f.CreationTimeUtc)
                .Select(f => new BackupInfo(f.Name, f.Length, f.CreationTimeUtc)).ToList()
            : []);
        g.MapGet("/backups/{name}", (string name, AppPaths paths) =>
        {
            var safe = Path.GetFileName(name);
            var path = Path.Combine(paths.Backups, safe);
            return safe.StartsWith("selftracker-backup-", StringComparison.Ordinal) && File.Exists(path)
                ? Results.File(path, "application/zip", safe)
                : Results.Problem(statusCode: 404, title: "Backup not found.");
        });
        g.MapPost("/import", async (HttpContext ctx, bool? dryRun, IDataPorter porter, CancellationToken ct) =>
        {
            if (dryRun == false)
            {
                return Results.Problem(statusCode: 501, title: "Full restore ships with M8; use dryRun=true to inspect a backup.");
            }

            if (ctx.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            {
                limit.MaxRequestBodySize = null;
            }

            var form = await ctx.Request.ReadFormAsync(ct);
            var file = form.Files.Count > 0 ? form.Files[0] : null;
            if (file is null)
            {
                return Results.Problem(statusCode: 400, title: "Attach the backup .zip as a file.");
            }

            await using var stream = file.OpenReadStream();
            await using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            return Results.Ok(await porter.InspectAsync(buffer, ct));
        }).DisableAntiforgery().Accepts<IFormFile>("multipart/form-data").Produces<ImportSummary>();
    }
}
