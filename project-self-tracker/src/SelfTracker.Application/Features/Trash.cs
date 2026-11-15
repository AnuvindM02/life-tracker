using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;

namespace SelfTracker.Application.Features;

public sealed record TrashItemDto(string EntityType, int Id, string Title, DateTimeOffset? DeletedAt, DateTimeOffset? PurgeAfter);

public sealed record ListTrash(string? EntityType);

public sealed class ListTrashHandler(EntityLookup lookup, PlanContext plan) : IHandler<ListTrash, IReadOnlyList<TrashItemDto>>
{
    public async Task<IReadOnlyList<TrashItemDto>> HandleAsync(ListTrash r, CancellationToken ct)
    {
        var retention = (await plan.LoadAsync(ct)).Settings.TrashRetentionDays;
        var types = string.IsNullOrWhiteSpace(r.EntityType)
            ? EntityRegistry.All.Where(t => t.Trashable)
            : [EntityRegistry.Require(r.EntityType)];
        var items = new List<TrashItemDto>();
        foreach (var type in types)
        {
            foreach (var e in await lookup.DeletedAsync(type, null, ct))
            {
                items.Add(new(type.Name, e.Id, type.Title(e), e.DeletedAt, e.DeletedAt?.AddDays(retention)));
            }
        }

        return items.OrderByDescending(i => i.DeletedAt).ToList();
    }
}

public sealed record RestoreTrashItem(string EntityType, int Id);

public sealed class RestoreTrashItemHandler(IAppDbContext db, EntityLookup lookup) : IHandler<RestoreTrashItem, TrashItemDto>
{
    public async Task<TrashItemDto> HandleAsync(RestoreTrashItem r, CancellationToken ct)
    {
        var type = EntityRegistry.Require(r.EntityType);
        var entity = await lookup.FindAnyAsync(type, r.Id, ct) ?? throw new NotFoundException($"{type.Name} {r.Id}");
        if (!entity.IsDeleted)
        {
            return new(type.Name, entity.Id, type.Title(entity), null, null);
        }

        if (entity is DailyLog log)
        {
            if (await db.DailyLogs.AnyAsync(l => l.Date == log.Date, ct))
            {
                throw new ConflictException($"Another daily log for {log.Date:yyyy-MM-dd} exists. Delete it first, then restore this one.");
            }

            // Children deleted together with the log come back with it.
            var stamp = log.DeletedAt;
            foreach (var tick in await db.DailyTicks.IgnoreQueryFilters().Where(t => t.DailyLogId == log.Id && t.IsDeleted && t.DeletedAt == stamp).ToListAsync(ct))
            {
                tick.IsDeleted = false;
                tick.DeletedAt = null;
            }

            foreach (var te in await db.TimeEntries.IgnoreQueryFilters().Where(t => t.DailyLogId == log.Id && t.IsDeleted && t.DeletedAt == stamp).ToListAsync(ct))
            {
                te.IsDeleted = false;
                te.DeletedAt = null;
            }
        }

        // Derived time entries (BR-19) deleted together with their source come back with it.
        var sourceStamp = entity.DeletedAt;
        foreach (var te in await db.TimeEntries.IgnoreQueryFilters()
            .Where(t => t.SourceEntityType == type.Name && t.SourceEntityId == entity.Id && t.IsDeleted && t.DeletedAt == sourceStamp).ToListAsync(ct))
        {
            te.IsDeleted = false;
            te.DeletedAt = null;
        }

        entity.IsDeleted = false;
        entity.DeletedAt = null;
        await db.SaveChangesAsync(ct);
        if (entity is ReadingLog reading)
        {
            await DdiaProgress.RecomputeChapterAsync(db, reading.ChapterId, ct);
            await db.SaveChangesAsync(ct);
        }

        return new(type.Name, entity.Id, type.Title(entity), null, null);
    }
}

public sealed record PurgeTrashItem(string EntityType, int Id);

/// <summary>Permanent delete. Also removes file links that point at the record, and the physical file for a FileAsset.</summary>
public sealed class Purger(IAppDbContext db, IFileStorage storage, EntityLookup lookup, ILogger<Purger> logger)
{
    public async Task PurgeAsync(EntityTypeInfo type, Entity entity, CancellationToken ct)
    {
        string? physical = null;
        switch (entity)
        {
            case DsaAttempt attempt:
                db.DsaReviewItems.RemoveRange(await db.DsaReviewItems.IgnoreQueryFilters().Where(r => r.SourceAttemptId == attempt.Id).ToListAsync(ct));
                foreach (var r in await db.DsaReviewItems.IgnoreQueryFilters().Where(r => r.CompletedAttemptId == attempt.Id).ToListAsync(ct))
                {
                    r.CompletedAttemptId = null;
                }

                break;
            case DsaProblem problem:
                // Attempts keep their history as free-text attempts; review ladders go.
                db.DsaReviewItems.RemoveRange(await db.DsaReviewItems.IgnoreQueryFilters().Where(r => r.ProblemId == problem.Id).ToListAsync(ct));
                foreach (var a in await db.DsaAttempts.IgnoreQueryFilters().Where(a => a.ProblemId == problem.Id).ToListAsync(ct))
                {
                    a.ProblemId = null;
                    a.TitleFreeText = problem.Title;
                }

                break;
            case DdiaChapter chapter when await db.ReadingLogs.IgnoreQueryFilters().AnyAsync(r => r.ChapterId == chapter.Id, ct)
                || await db.DdiaNotes.IgnoreQueryFilters().AnyAsync(n => n.ChapterId == chapter.Id, ct):
                throw new ConflictException("This chapter still has reading logs or a note (some may be in the Trash). Purge them first.");
            case DsaTopic topic when await db.DsaProblems.IgnoreQueryFilters().AnyAsync(p => p.TopicId == topic.Id, ct):
                throw new ConflictException("This topic still has problems (some may be in the Trash). Purge or move them first.");
            case FileAsset file:
                db.FileLinks.RemoveRange(await db.FileLinks.IgnoreQueryFilters().Where(l => l.FileAssetId == file.Id).ToListAsync(ct));
                physical = file.RelativePath;
                break;
            case DailyLog log:
                var tickIds = await db.DailyTicks.IgnoreQueryFilters().Where(t => t.DailyLogId == log.Id).Select(t => t.Id).ToListAsync(ct);
                db.FileLinks.RemoveRange(await db.FileLinks.IgnoreQueryFilters()
                    .Where(l => l.EntityType == "DailyTick" && tickIds.Contains(l.EntityId)).ToListAsync(ct));
                db.DailyTicks.RemoveRange(await db.DailyTicks.IgnoreQueryFilters().Where(t => t.DailyLogId == log.Id).ToListAsync(ct));
                db.TimeEntries.RemoveRange(await db.TimeEntries.IgnoreQueryFilters().Where(t => t.DailyLogId == log.Id).ToListAsync(ct));
                break;
        }

        db.FileLinks.RemoveRange(await db.FileLinks.IgnoreQueryFilters()
            .Where(l => l.EntityType == type.Name && l.EntityId == entity.Id).ToListAsync(ct));
        db.TimeEntries.RemoveRange(await db.TimeEntries.IgnoreQueryFilters()
            .Where(t => t.SourceEntityType == type.Name && t.SourceEntityId == entity.Id).ToListAsync(ct));
        db.Entry(entity).State = EntityState.Deleted;
        await db.SaveChangesAsync(ct);

        if (physical is not null)
        {
            try
            {
                storage.Delete(physical);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Purged file row {FileId} but could not delete its content; Verify will report it as an orphan", entity.Id);
            }
        }
    }

    public async Task<int> PurgeExpiredAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        var count = 0;
        foreach (var type in EntityRegistry.All.Where(t => t.Trashable))
        {
            foreach (var e in await lookup.DeletedAsync(type, cutoff, ct))
            {
                await PurgeAsync(type, e, ct);
                count++;
            }
        }

        return count;
    }
}

public sealed class PurgeTrashItemHandler(EntityLookup lookup, Purger purger) : IHandler<PurgeTrashItem, bool>
{
    public async Task<bool> HandleAsync(PurgeTrashItem r, CancellationToken ct)
    {
        var type = EntityRegistry.Require(r.EntityType);
        if (!type.Trashable)
        {
            throw new BadRequestException($"{type.Name} records are not purged individually.");
        }

        var entity = await lookup.FindAnyAsync(type, r.Id, ct) ?? throw new NotFoundException($"{type.Name} {r.Id}");
        if (!entity.IsDeleted)
        {
            throw new ConflictException("Only items in the Trash can be deleted permanently. Delete it first.");
        }

        await purger.PurgeAsync(type, entity, ct);
        return true;
    }
}

public sealed record DeleteFile(int Id);

public sealed class DeleteFileHandler(IAppDbContext db, IClock clock) : IHandler<DeleteFile, bool>
{
    public async Task<bool> HandleAsync(DeleteFile r, CancellationToken ct)
    {
        var f = await db.FileAssets.FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new NotFoundException($"File {r.Id}");
        f.SoftDelete(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
