using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;
using SelfTracker.Domain.Services;

namespace SelfTracker.Application.Features;

public sealed record FileLinkDto(int Id, string EntityType, int EntityId, string? Label);

public sealed record FileAssetDto(
    int Id,
    Guid StorageKey,
    string OriginalName,
    string DisplayTitle,
    string? Description,
    string ContentType,
    FileKind Kind,
    long SizeBytes,
    string Sha256,
    DateTimeOffset UploadedAt,
    string? Tags,
    bool InlineAllowed,
    IReadOnlyList<FileLinkDto> Links,
    string RowVersion);

public enum UploadStatus { Stored, Duplicate, Rejected }

public sealed record UploadItemResult(string OriginalName, UploadStatus Status, FileAssetDto? File, string? Message);

public static class FileProjections
{
    public static readonly Expression<Func<FileAsset, FileAssetDto>> Dto = f => new FileAssetDto(
        f.Id, f.StorageKey, f.OriginalName, f.DisplayTitle, f.Description, f.ContentType, f.Kind, f.SizeBytes, f.Sha256, f.UploadedAt, f.Tags,
        false,
        f.Links.Where(l => !l.IsDeleted).Select(l => new FileLinkDto(l.Id, l.EntityType, l.EntityId, l.Label)).ToList(),
        Convert.ToBase64String(f.RowVersion));

    public static FileAssetDto WithInline(this FileAssetDto dto) => dto with { InlineAllowed = FileRules.InlineAllowed(dto.ContentType) };
}

public sealed record UploadTarget(string? EntityType, int? EntityId, string? Label);

/// <summary>
/// Atomic upload (spec 6.1): stream to temp while hashing, sniff the type, detect duplicates,
/// move into {yyyy}/{MM}/{guid}{ext}, then insert the row; any failure removes the file.
/// </summary>
public sealed class FileUploadService(
    IAppDbContext db,
    IFileStorage storage,
    PlanContext plan,
    EntityLookup lookup,
    IClock clock,
    ILogger<FileUploadService> logger)
{
    public async Task ValidateTargetAsync(UploadTarget target, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(target.EntityType) && target.EntityId is null)
        {
            return;
        }

        var type = EntityRegistry.Require(target.EntityType);
        if (!type.Attachable || target.EntityId is null)
        {
            throw new BadRequestException($"Files cannot be attached to '{target.EntityType}'.");
        }

        if (!await lookup.ExistsAsync(type, target.EntityId.Value, ct))
        {
            throw new NotFoundException($"{type.Name} {target.EntityId}");
        }
    }

    public async Task<UploadItemResult> StoreAsync(Stream content, string? clientName, UploadTarget target, CancellationToken ct)
    {
        var settings = (await plan.LoadAsync(ct)).Settings;
        var name = FileRules.SanitizeName(clientName);
        var ext = FileRules.SafeExtension(name);
        if (FileRules.IsBlocked(ext) && !settings.AllowDangerousExtensions)
        {
            return new(name, UploadStatus.Rejected, null, $"Files with the {ext} extension are blocked. An override exists in Settings.");
        }

        var maxBytes = (long)settings.MaxUploadMb * 1024 * 1024;
        var temp = await storage.WriteTempAsync(content, maxBytes, ct);
        try
        {
            var existing = await db.FileAssets.AsNoTracking().Where(f => f.Sha256 == temp.Sha256)
                .Select(FileProjections.Dto).FirstOrDefaultAsync(ct);
            if (existing is not null)
            {
                storage.DeleteTemp(temp.TempPath);
                return new(name, UploadStatus.Duplicate, existing.WithInline(),
                    $"This file is already stored as \"{existing.DisplayTitle}\". Link the existing file instead of storing another copy.");
            }

            var contentType = FileRules.Sniff(temp.Head, ext);
            var key = Guid.NewGuid();
            var now = clock.UtcNow;
            var relative = $"{now:yyyy}/{now:MM}/{key:N}{ext}";
            storage.Commit(temp.TempPath, relative);
            try
            {
                var asset = new FileAsset
                {
                    StorageKey = key,
                    RelativePath = relative,
                    OriginalName = name,
                    DisplayTitle = Path.GetFileNameWithoutExtension(name) is { Length: > 0 } t ? t : name,
                    ContentType = contentType,
                    Kind = FileRules.KindFor(contentType, ext),
                    SizeBytes = temp.SizeBytes,
                    Sha256 = temp.Sha256,
                    UploadedAt = now,
                };
                if (target.EntityId is { } id && EntityRegistry.Find(target.EntityType) is { } type)
                {
                    asset.Links.Add(new FileLink { EntityType = type.Name, EntityId = id, Label = target.Label });
                }

                db.FileAssets.Add(asset);
                await db.SaveChangesAsync(ct);
                var dto = await db.FileAssets.AsNoTracking().Where(f => f.Id == asset.Id).Select(FileProjections.Dto).FirstAsync(ct);
                return new(name, UploadStatus.Stored, dto.WithInline(), null);
            }
            catch
            {
                storage.Delete(relative);
                throw;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Upload of a file failed; temp file removed");
            storage.DeleteTemp(temp.TempPath);
            throw;
        }
        finally
        {
            storage.DeleteTemp(temp.TempPath);
        }
    }
}

public sealed record ListFiles(string? Q, FileKind? Kind, string? EntityType, int? EntityId, string? Tag, bool? Unattached, ListQuery List);

public sealed class ListFilesHandler(IAppDbContext db) : IHandler<ListFiles, PagedResult<FileAssetDto>>
{
    public async Task<PagedResult<FileAssetDto>> HandleAsync(ListFiles r, CancellationToken ct)
    {
        var q = db.FileAssets.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(r.Q))
        {
            var term = r.Q.Trim();
            q = q.Where(f => f.DisplayTitle.Contains(term) || f.OriginalName.Contains(term)
                || (f.Description != null && f.Description.Contains(term)) || (f.Tags != null && f.Tags.Contains(term)));
        }

        if (r.Kind is { } kind)
        {
            q = q.Where(f => f.Kind == kind);
        }

        if (!string.IsNullOrWhiteSpace(r.EntityType))
        {
            var type = EntityRegistry.Require(r.EntityType).Name;
            q = q.Where(f => f.Links.Any(l => !l.IsDeleted && l.EntityType == type && (r.EntityId == null || l.EntityId == r.EntityId)));
        }

        if (!string.IsNullOrWhiteSpace(r.Tag))
        {
            q = q.Where(f => f.Tags != null && f.Tags.Contains(r.Tag));
        }

        if (r.Unattached == true)
        {
            q = q.Where(f => !f.Links.Any(l => !l.IsDeleted));
        }

        var page = await q.ApplySort(r.List.Sort, "-uploadedAt").Select(FileProjections.Dto).ToPagedAsync(r.List, ct);
        return page with { Items = page.Items.Select(i => i.WithInline()).ToList() };
    }
}

public sealed record GetFile(int Id);

public sealed class GetFileHandler(IAppDbContext db) : IHandler<GetFile, FileAssetDto>
{
    public async Task<FileAssetDto> HandleAsync(GetFile r, CancellationToken ct) =>
        (await db.FileAssets.AsNoTracking().Where(f => f.Id == r.Id).Select(FileProjections.Dto).FirstOrDefaultAsync(ct)
         ?? throw new NotFoundException($"File {r.Id}")).WithInline();
}

public sealed record FileContent(Stream Stream, string ContentType, string OriginalName, bool Inline);

public sealed record OpenFile(int Id);

public sealed class OpenFileHandler(IAppDbContext db, IFileStorage storage) : IHandler<OpenFile, FileContent>
{
    public async Task<FileContent> HandleAsync(OpenFile r, CancellationToken ct)
    {
        var f = await db.FileAssets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.Id, ct)
            ?? throw new NotFoundException($"File {r.Id}");
        if (!storage.Exists(f.RelativePath))
        {
            throw new NotFoundException($"The stored content of file {r.Id} (run Maintenance > Verify)");
        }

        var inline = FileRules.InlineAllowed(f.ContentType);
        // Markdown is previewed as plain text so the browser never renders it.
        var type = f.ContentType == "text/markdown" ? "text/plain; charset=utf-8"
            : f.ContentType == "text/plain" ? "text/plain; charset=utf-8" : f.ContentType;
        return new FileContent(storage.OpenRead(f.RelativePath), type, f.OriginalName, inline);
    }
}

public sealed record UpdateFileRequest(
    [property: Required, StringLength(200)] string DisplayTitle,
    [property: StringLength(2000)] string? Description,
    [property: StringLength(500)] string? Tags,
    string? RowVersion) : IVersioned;

public sealed record UpdateFile(int Id, UpdateFileRequest Body);

public sealed class UpdateFileHandler(IAppDbContext db, IHandler<GetFile, FileAssetDto> get) : IHandler<UpdateFile, FileAssetDto>
{
    public async Task<FileAssetDto> HandleAsync(UpdateFile r, CancellationToken ct)
    {
        var f = await db.FileAssets.FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new NotFoundException($"File {r.Id}");
        db.ExpectVersion(f, r.Body.RowVersion);
        f.DisplayTitle = r.Body.DisplayTitle.Trim();
        f.Description = r.Body.Description;
        f.Tags = NormalizeTags(r.Body.Tags);
        await db.SaveChangesAsync(ct);
        return await get.HandleAsync(new GetFile(r.Id), ct);
    }

    private static string? NormalizeTags(string? tags)
    {
        var parts = (tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant()).Distinct().ToList();
        return parts.Count == 0 ? null : string.Join(',', parts);
    }
}

public sealed record AddFileLinkRequest([property: Required] string EntityType, int EntityId, [property: StringLength(120)] string? Label);

public sealed record AddFileLink(int FileId, AddFileLinkRequest Body);

public sealed class AddFileLinkHandler(IAppDbContext db, FileUploadService uploads, IHandler<GetFile, FileAssetDto> get) : IHandler<AddFileLink, FileAssetDto>
{
    public async Task<FileAssetDto> HandleAsync(AddFileLink r, CancellationToken ct)
    {
        var target = new UploadTarget(r.Body.EntityType, r.Body.EntityId, r.Body.Label);
        await uploads.ValidateTargetAsync(target, ct);
        var file = await db.FileAssets.Include(f => f.Links).FirstOrDefaultAsync(f => f.Id == r.FileId, ct)
            ?? throw new NotFoundException($"File {r.FileId}");
        var type = EntityRegistry.Require(r.Body.EntityType).Name;
        if (!file.Links.Any(l => l.EntityType == type && l.EntityId == r.Body.EntityId))
        {
            file.Links.Add(new FileLink { EntityType = type, EntityId = r.Body.EntityId, Label = r.Body.Label });
            await db.SaveChangesAsync(ct);
        }

        return await get.HandleAsync(new GetFile(r.FileId), ct);
    }
}

public sealed record RemoveFileLink(int FileId, string EntityType, int EntityId);

public sealed class RemoveFileLinkHandler(IAppDbContext db, IHandler<GetFile, FileAssetDto> get) : IHandler<RemoveFileLink, FileAssetDto>
{
    public async Task<FileAssetDto> HandleAsync(RemoveFileLink r, CancellationToken ct)
    {
        var type = EntityRegistry.Require(r.EntityType).Name;
        var links = await db.FileLinks.Where(l => l.FileAssetId == r.FileId && l.EntityType == type && l.EntityId == r.EntityId).ToListAsync(ct);
        db.FileLinks.RemoveRange(links);
        await db.SaveChangesAsync(ct);
        return await get.HandleAsync(new GetFile(r.FileId), ct);
    }
}

public sealed record FileKindCount(FileKind Kind, int Count, long Bytes);

public sealed record FileStatsDto(
    long TotalBytes,
    int TotalFiles,
    IReadOnlyList<FileKindCount> ByKind,
    IReadOnlyList<FileAssetDto> Largest,
    int UnattachedCount,
    long? FreeDiskBytes,
    bool LowDiskWarning,
    string FileRoot);

public sealed record GetFileStats;

public sealed class GetFileStatsHandler(IAppDbContext db, IFileStorage storage) : IHandler<GetFileStats, FileStatsDto>
{
    public const long LowDiskBytes = 5L * 1024 * 1024 * 1024;

    public async Task<FileStatsDto> HandleAsync(GetFileStats r, CancellationToken ct)
    {
        var byKind = await db.FileAssets.AsNoTracking().GroupBy(f => f.Kind)
            .Select(g => new FileKindCount(g.Key, g.Count(), g.Sum(f => f.SizeBytes))).ToListAsync(ct);
        var largest = await db.FileAssets.AsNoTracking().OrderByDescending(f => f.SizeBytes).Take(10).Select(FileProjections.Dto).ToListAsync(ct);
        var unattached = await db.FileAssets.CountAsync(f => !f.Links.Any(l => !l.IsDeleted), ct);
        var free = storage.FreeBytes();
        return new FileStatsDto(byKind.Sum(k => k.Bytes), byKind.Sum(k => k.Count), byKind, largest.Select(l => l.WithInline()).ToList(),
            unattached, free, free is < LowDiskBytes, storage.Root);
    }
}

public sealed record VerifyIssue(string Kind, string Path, int? FileId, string Detail);

public sealed record VerifyReport(int FilesChecked, int Orphans, int Missing, int HashMismatches, IReadOnlyList<VerifyIssue> Issues);

public sealed record VerifyFiles;

/// <summary>Orphaned files (disk, no row), missing files (row, no disk) and SHA-256 mismatches (BR-17).</summary>
public sealed class VerifyFilesHandler(IAppDbContext db, IFileStorage storage) : IHandler<VerifyFiles, VerifyReport>
{
    public async Task<VerifyReport> HandleAsync(VerifyFiles r, CancellationToken ct)
    {
        var rows = await db.FileAssets.IgnoreQueryFilters().AsNoTracking()
            .Select(f => new { f.Id, f.RelativePath, f.Sha256 }).ToListAsync(ct);
        var known = rows.Select(x => x.RelativePath.Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var issues = new List<VerifyIssue>();
        foreach (var path in storage.EnumerateFiles())
        {
            if (!known.Contains(path))
            {
                issues.Add(new("Orphan", path, null, "File on disk has no database row."));
            }
        }

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            if (!storage.Exists(row.RelativePath))
            {
                issues.Add(new("Missing", row.RelativePath, row.Id, "Database row has no file on disk."));
                continue;
            }

            var hash = await storage.ComputeSha256Async(row.RelativePath, ct);
            if (!string.Equals(hash, row.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new("HashMismatch", row.RelativePath, row.Id, $"Stored SHA-256 {row.Sha256[..12]}..., on disk {hash[..12]}..."));
            }
        }

        return new VerifyReport(rows.Count, issues.Count(i => i.Kind == "Orphan"), issues.Count(i => i.Kind == "Missing"),
            issues.Count(i => i.Kind == "HashMismatch"), issues);
    }
}
