using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;
using SelfTracker.Domain.Services;

namespace SelfTracker.Application.Features;

public sealed record TickDto(int Id, TickType Type, string EvidenceNote, string? EvidenceUrl, string? LinkedEntityType, int? LinkedEntityId, int AttachmentCount, string RowVersion);

public sealed record TimeEntryDto(
    int Id,
    DateOnly Date,
    int? DailyLogId,
    TimeCategory Category,
    int Minutes,
    string? Note,
    TimeSource Source,
    string? SourceEntityType,
    int? SourceEntityId,
    string RowVersion);

public sealed record CategoryMinutes(TimeCategory Category, int Minutes);

public sealed record DailyLogDto(
    int Id,
    DateOnly Date,
    int PlanDay,
    DayType? DayType,
    bool IsTravelDay,
    TravelRole TravelRole,
    bool LoggedLate,
    int? Energy,
    decimal? SleepHours,
    TimeOnly? WakeTime,
    TimeOnly? LightsOutTime,
    string? DsaLine,
    string? CoreLine,
    string? BuildLine,
    string? TomorrowDsa,
    string? TomorrowBuild,
    string? NotesMd,
    bool RitualOpenTracker,
    bool RitualReadWeekTarget,
    bool RitualWriteTargets,
    bool RitualPhoneAway,
    bool RitualStartTimer,
    bool CloseUpdateTracker,
    bool CloseTomorrowTargets,
    bool ClosePrep,
    bool CloseScreensOff,
    IReadOnlyList<int> BlocksDone,
    GymStatus? GymStatus,
    string? GymSession,
    string? GymNote,
    bool DsaTick,
    TickDto? CoreTick,
    TickDto? BuildTick,
    IReadOnlyList<TimeEntryDto> TimeEntries,
    IReadOnlyList<CategoryMinutes> MinutesByCategory,
    MvdResult Mvd,
    int AttachmentCount,
    string RowVersion);

public sealed record DailyLogSummaryDto(
    int Id,
    DateOnly Date,
    DayType? DayType,
    bool IsTravelDay,
    bool LoggedLate,
    int? Energy,
    decimal? SleepHours,
    bool DsaTick,
    bool CoreTick,
    bool BuildTick,
    int TotalMinutes,
    GymStatus? GymStatus);

public sealed record DailyLogInput(
    DayType? DayType,
    bool? IsTravelDay,
    [property: Range(1, 5)] int? Energy,
    [property: Range(0, 24)] decimal? SleepHours,
    TimeOnly? WakeTime,
    TimeOnly? LightsOutTime,
    [property: StringLength(1000)] string? DsaLine,
    [property: StringLength(1000)] string? CoreLine,
    [property: StringLength(1000)] string? BuildLine,
    [property: StringLength(500)] string? TomorrowDsa,
    [property: StringLength(500)] string? TomorrowBuild,
    string? NotesMd,
    bool RitualOpenTracker,
    bool RitualReadWeekTarget,
    bool RitualWriteTargets,
    bool RitualPhoneAway,
    bool RitualStartTimer,
    bool CloseUpdateTracker,
    bool CloseTomorrowTargets,
    bool ClosePrep,
    bool CloseScreensOff,
    int[]? BlocksDone,
    GymStatus? GymStatus,
    [property: StringLength(120)] string? GymSession,
    [property: StringLength(2000)] string? GymNote,
    string? RowVersion) : IVersioned;

public sealed record CreateDailyLogRequest([property: Required] DateOnly Date, DailyLogInput Log);

/// <summary>Shared reads and helpers for the daily log module.</summary>
public sealed class DailyLogService(IAppDbContext db, PlanContext plan, IClock clock)
{
    public async Task<DailyLog> EnsureAsync(DateOnly date, CancellationToken ct)
    {
        var log = await db.DailyLogs.FirstOrDefaultAsync(l => l.Date == date, ct);
        if (log is not null)
        {
            return log;
        }

        var snap = await plan.LoadAsync(ct);
        log = new DailyLog
        {
            Date = date,
            DayType = null,
            IsTravelDay = snap.Travel.IsTravelDay(date),
            LoggedLate = date < snap.Today,
        };
        db.DailyLogs.Add(log);
        await db.SaveChangesAsync(ct);
        return log;
    }

    public async Task<DailyLogDto> GetDtoAsync(int id, CancellationToken ct)
    {
        var log = await db.DailyLogs.AsNoTracking()
            .Include(l => l.Ticks)
            .Include(l => l.TimeEntries)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException($"Daily log {id}");
        var snap = await plan.LoadAsync(ct);

        var tickIds = log.Ticks.Select(t => t.Id).ToList();
        var tickFiles = await db.FileLinks.AsNoTracking()
            .Where(l => l.EntityType == "DailyTick" && tickIds.Contains(l.EntityId))
            .GroupBy(l => l.EntityId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var logFiles = await db.FileLinks.CountAsync(l => l.EntityType == "DailyLog" && l.EntityId == log.Id, ct);

        TickDto? Tick(TickType type) => log.Ticks.Where(t => t.Type == type).Select(t => new TickDto(
            t.Id, t.Type, t.EvidenceNote, t.EvidenceUrl, t.LinkedEntityType, t.LinkedEntityId, tickFiles.GetValueOrDefault(t.Id), t.Version())).FirstOrDefault();

        var entries = log.TimeEntries.OrderBy(t => t.Id).Select(ToDto).ToList();
        var dsa = await db.DsaAttempts.AsNoTracking().Where(a => a.Date == log.Date)
            .Select(a => new { a.IsCounted, a.Type, Completed = a.ReviewItemId != null }).ToListAsync(ct);
        var byCategory = entries.GroupBy(e => e.Category).Select(g => new CategoryMinutes(g.Key, g.Sum(e => e.Minutes))).ToList();
        var core = Tick(TickType.Core);
        var build = Tick(TickType.Build);
        var mvd = MvdRules.Evaluate(new MvdInput(
            log.IsTravelDay,
            Minutes(byCategory, TimeCategory.Dsa),
            HasDsaReview: dsa.Any(a => a.Type == AttemptType.ColdReview),
            Minutes(byCategory, TimeCategory.DdiaDesign),
            core?.EvidenceNote ?? log.CoreLine,
            Minutes(byCategory, TimeCategory.MainTrack),
            build is not null,
            string.Join('\n', new[] { log.DsaLine, log.CoreLine, log.BuildLine, log.NotesMd }.Where(s => !string.IsNullOrWhiteSpace(s)))));

        return new DailyLogDto(
            log.Id, log.Date, snap.Calendar?.PlanDay(log.Date) ?? 0, log.DayType, log.IsTravelDay, snap.Travel.RoleFor(log.Date), log.LoggedLate,
            log.Energy, log.SleepHours, log.WakeTime, log.LightsOutTime, log.DsaLine, log.CoreLine, log.BuildLine, log.TomorrowDsa,
            log.TomorrowBuild, log.NotesMd, log.RitualOpenTracker, log.RitualReadWeekTarget, log.RitualWriteTargets, log.RitualPhoneAway,
            log.RitualStartTimer, log.CloseUpdateTracker, log.CloseTomorrowTargets, log.ClosePrep, log.CloseScreensOff,
            ParseIds(log.BlocksDoneCsv), log.GymStatus, log.GymSession, log.GymNote,
            DsaTick: TickRules.DsaTick(dsa.Count(a => a.IsCounted), dsa.Count(a => a.Completed)), core, build, entries, byCategory, mvd, logFiles, log.Version());
    }

    public static TimeEntryDto ToDto(TimeEntry t) =>
        new(t.Id, t.Date, t.DailyLogId, t.Category, t.Minutes, t.Note, t.Source, t.SourceEntityType, t.SourceEntityId, t.Version());

    public static IReadOnlyList<int> ParseIds(string? csv) =>
        (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s, out var i) ? i : 0).Where(i => i > 0).ToList();

    public void Apply(DailyLogInput input, DailyLog log, PlanSnapshot snap)
    {
        log.DayType = input.DayType;
        log.IsTravelDay = input.IsTravelDay ?? snap.Travel.IsTravelDay(log.Date);
        log.Energy = input.Energy;
        log.SleepHours = input.SleepHours;
        log.WakeTime = input.WakeTime;
        log.LightsOutTime = input.LightsOutTime;
        log.DsaLine = input.DsaLine;
        log.CoreLine = input.CoreLine;
        log.BuildLine = input.BuildLine;
        log.TomorrowDsa = input.TomorrowDsa;
        log.TomorrowBuild = input.TomorrowBuild;
        log.NotesMd = input.NotesMd;
        log.RitualOpenTracker = input.RitualOpenTracker;
        log.RitualReadWeekTarget = input.RitualReadWeekTarget;
        log.RitualWriteTargets = input.RitualWriteTargets;
        log.RitualPhoneAway = input.RitualPhoneAway;
        log.RitualStartTimer = input.RitualStartTimer;
        log.CloseUpdateTracker = input.CloseUpdateTracker;
        log.CloseTomorrowTargets = input.CloseTomorrowTargets;
        log.ClosePrep = input.ClosePrep;
        log.CloseScreensOff = input.CloseScreensOff;
        log.BlocksDoneCsv = input.BlocksDone is { Length: > 0 } ids ? string.Join(',', ids.Distinct().Order()) : null;
        log.GymStatus = input.GymStatus;
        log.GymSession = input.GymSession;
        log.GymNote = input.GymNote;
    }

    public DateTimeOffset Now => clock.UtcNow;

    private static int Minutes(List<CategoryMinutes> list, TimeCategory c) => list.Where(x => x.Category == c).Sum(x => x.Minutes);
}

public sealed record ListDailyLogs(DateOnly? From, DateOnly? To, DayType? Type, bool? InProgress, ListQuery List);

public sealed class ListDailyLogsHandler(IAppDbContext db) : IHandler<ListDailyLogs, PagedResult<DailyLogSummaryDto>>
{
    public async Task<PagedResult<DailyLogSummaryDto>> HandleAsync(ListDailyLogs r, CancellationToken ct)
    {
        var q = db.DailyLogs.AsNoTracking();
        if (r.From is { } from)
        {
            q = q.Where(l => l.Date >= from);
        }

        if (r.To is { } to)
        {
            q = q.Where(l => l.Date <= to);
        }

        if (r.Type is { } type)
        {
            q = q.Where(l => l.DayType == type);
        }

        if (r.InProgress == true)
        {
            q = q.Where(l => l.DayType == null);
        }

        return await q.ApplySort(r.List.Sort, "-date")
            .Select(l => new DailyLogSummaryDto(
                l.Id, l.Date, l.DayType, l.IsTravelDay, l.LoggedLate, l.Energy, l.SleepHours,
                db.DsaAttempts.Any(a => a.Date == l.Date && (a.IsCounted || a.ReviewItemId != null)),
                l.Ticks.Any(t => t.Type == TickType.Core),
                l.Ticks.Any(t => t.Type == TickType.Build),
                l.TimeEntries.Sum(t => (int?)t.Minutes) ?? 0,
                l.GymStatus))
            .ToPagedAsync(r.List, ct);
    }
}

public sealed record GetDailyLog(int Id);

public sealed class GetDailyLogHandler(DailyLogService logs) : IHandler<GetDailyLog, DailyLogDto>
{
    public Task<DailyLogDto> HandleAsync(GetDailyLog r, CancellationToken ct) => logs.GetDtoAsync(r.Id, ct);
}

public sealed record GetDailyLogByDate(DateOnly Date);

public sealed class GetDailyLogByDateHandler(IAppDbContext db, DailyLogService logs) : IHandler<GetDailyLogByDate, DailyLogDto>
{
    public async Task<DailyLogDto> HandleAsync(GetDailyLogByDate r, CancellationToken ct)
    {
        var id = await db.DailyLogs.Where(l => l.Date == r.Date).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Daily log for {r.Date:yyyy-MM-dd}");
        return await logs.GetDtoAsync(id, ct);
    }
}

public sealed record EnsureDailyLog(DateOnly Date);

/// <summary>Creates an In progress log for the date if none exists (spec v1.1, 4.3).</summary>
public sealed class EnsureDailyLogHandler(DailyLogService logs) : IHandler<EnsureDailyLog, DailyLogDto>
{
    public async Task<DailyLogDto> HandleAsync(EnsureDailyLog r, CancellationToken ct)
    {
        var log = await logs.EnsureAsync(r.Date, ct);
        return await logs.GetDtoAsync(log.Id, ct);
    }
}

public sealed class CreateDailyLogHandler(IAppDbContext db, DailyLogService logs, PlanContext plan) : IHandler<CreateDailyLogRequest, DailyLogDto>
{
    public async Task<DailyLogDto> HandleAsync(CreateDailyLogRequest r, CancellationToken ct)
    {
        if (await db.DailyLogs.AnyAsync(l => l.Date == r.Date, ct))
        {
            throw new ConflictException($"A daily log for {r.Date:yyyy-MM-dd} already exists; edit it instead.");
        }

        var snap = await plan.LoadAsync(ct);
        if (r.Date > snap.Today)
        {
            throw RuleViolationException.For("date", "Future dates cannot be logged.");
        }

        var log = new DailyLog { Date = r.Date, LoggedLate = r.Date < snap.Today };
        logs.Apply(r.Log, log, snap);
        db.DailyLogs.Add(log);
        await db.SaveChangesAsync(ct);
        return await logs.GetDtoAsync(log.Id, ct);
    }
}

public sealed record UpdateDailyLog(int Id, DailyLogInput Log);

public sealed class UpdateDailyLogHandler(IAppDbContext db, DailyLogService logs, PlanContext plan) : IHandler<UpdateDailyLog, DailyLogDto>
{
    public async Task<DailyLogDto> HandleAsync(UpdateDailyLog r, CancellationToken ct)
    {
        var log = await db.DailyLogs.FirstOrDefaultAsync(l => l.Id == r.Id, ct) ?? throw new NotFoundException($"Daily log {r.Id}");
        db.ExpectVersion(log, r.Log.RowVersion);
        logs.Apply(r.Log, log, await plan.LoadAsync(ct));
        await db.SaveChangesAsync(ct);
        return await logs.GetDtoAsync(log.Id, ct);
    }
}

public sealed record DeleteDailyLog(int Id);

/// <summary>Soft-deletes the log with its ticks and time entries (same timestamp, so they restore together).</summary>
public sealed class DeleteDailyLogHandler(IAppDbContext db, IClock clock) : IHandler<DeleteDailyLog, bool>
{
    public async Task<bool> HandleAsync(DeleteDailyLog r, CancellationToken ct)
    {
        var log = await db.DailyLogs.Include(l => l.Ticks).Include(l => l.TimeEntries).FirstOrDefaultAsync(l => l.Id == r.Id, ct)
            ?? throw new NotFoundException($"Daily log {r.Id}");
        var now = clock.UtcNow;
        log.SoftDelete(now);
        log.Ticks.ForEach(t => t.SoftDelete(now));
        log.TimeEntries.ForEach(t => t.SoftDelete(now));
        await db.SaveChangesAsync(ct);
        return true;
    }
}

// ---- Ticks (BR-06) ----

public sealed record TickRequest(
    [property: StringLength(2000)] string? EvidenceNote,
    [property: StringLength(500)] string? EvidenceUrl,
    [property: StringLength(40)] string? LinkedEntityType,
    int? LinkedEntityId,
    int[]? AttachmentFileIds,
    string? RowVersion);

public sealed record SetTick(int LogId, TickType Type, TickRequest Body);

public sealed class SetTickHandler(IAppDbContext db, DailyLogService logs, EntityLookup lookup) : IHandler<SetTick, DailyLogDto>
{
    public async Task<DailyLogDto> HandleAsync(SetTick r, CancellationToken ct)
    {
        if (r.Type == TickType.Dsa)
        {
            throw new BadRequestException(TickRules.DsaDerivedMessage);
        }

        var log = await db.DailyLogs.Include(l => l.Ticks).FirstOrDefaultAsync(l => l.Id == r.LogId, ct)
            ?? throw new NotFoundException($"Daily log {r.LogId}");
        var body = r.Body;

        if (!string.IsNullOrWhiteSpace(body.LinkedEntityType))
        {
            var type = EntityRegistry.Find(body.LinkedEntityType);
            if (type is null || body.LinkedEntityId is null || !await lookup.ExistsAsync(type, body.LinkedEntityId.Value, ct))
            {
                throw RuleViolationException.For("linkedEntityType",
                    $"Linked {body.LinkedEntityType} {body.LinkedEntityId} was not found (notes, drills and ADRs arrive in later milestones).");
            }
        }

        var newFiles = (body.AttachmentFileIds ?? []).Distinct().ToList();
        if (newFiles.Count > 0 && await db.FileAssets.CountAsync(f => newFiles.Contains(f.Id), ct) != newFiles.Count)
        {
            throw RuleViolationException.For("attachmentFileIds", "One or more attached files were not found.");
        }

        var tick = log.Ticks.FirstOrDefault(t => t.Type == r.Type);
        var existingFiles = tick is null ? 0 : await db.FileLinks.CountAsync(l => l.EntityType == "DailyTick" && l.EntityId == tick.Id, ct);
        var errors = TickRules.Validate(new TickEvidenceInput(r.Type, body.EvidenceNote, body.EvidenceUrl, body.LinkedEntityType,
            body.LinkedEntityId, existingFiles + newFiles.Count));
        if (errors.Count > 0)
        {
            throw new RuleViolationException(new Dictionary<string, string[]> { ["evidence"] = [.. errors] },
                "This tick needs evidence (Tracker is the truth).");
        }

        if (tick is null)
        {
            tick = new DailyTick { Type = r.Type };
            log.Ticks.Add(tick);
        }
        else
        {
            db.ExpectVersion(tick, body.RowVersion);
        }

        tick.EvidenceNote = body.EvidenceNote!.Trim();
        tick.EvidenceUrl = string.IsNullOrWhiteSpace(body.EvidenceUrl) ? null : body.EvidenceUrl.Trim();
        tick.LinkedEntityType = string.IsNullOrWhiteSpace(body.LinkedEntityType) ? null : EntityRegistry.Find(body.LinkedEntityType)?.Name;
        tick.LinkedEntityId = tick.LinkedEntityType is null ? null : body.LinkedEntityId;

        // Tick and its evidence links are written in one transaction (the retrying strategy owns it).
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.SaveChangesAsync(ct);
            foreach (var fileId in newFiles)
            {
                if (!await db.FileLinks.AnyAsync(l => l.FileAssetId == fileId && l.EntityType == "DailyTick" && l.EntityId == tick.Id, ct))
                {
                    db.FileLinks.Add(new FileLink { FileAssetId = fileId, EntityType = "DailyTick", EntityId = tick.Id, Label = $"{r.Type} tick evidence" });
                }
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return await logs.GetDtoAsync(log.Id, ct);
    }
}

public sealed record ClearTick(int LogId, TickType Type);

public sealed class ClearTickHandler(IAppDbContext db, DailyLogService logs, IClock clock) : IHandler<ClearTick, DailyLogDto>
{
    public async Task<DailyLogDto> HandleAsync(ClearTick r, CancellationToken ct)
    {
        if (r.Type == TickType.Dsa)
        {
            throw new BadRequestException(TickRules.DsaDerivedMessage);
        }

        var tick = await db.DailyTicks.FirstOrDefaultAsync(t => t.DailyLogId == r.LogId && t.Type == r.Type, ct);
        if (tick is not null)
        {
            tick.SoftDelete(clock.UtcNow);
            await db.SaveChangesAsync(ct);
        }

        return await logs.GetDtoAsync(r.LogId, ct);
    }
}

// ---- Time entries (BR-12; derived entries arrive with BR-19 sources) ----

public sealed record TimeEntryInput(
    TimeCategory Category,
    [property: Range(1, 1440)] int Minutes,
    [property: StringLength(500)] string? Note,
    TimeSource Source,
    string? RowVersion) : IVersioned;

public sealed record AddTimeEntry(DateOnly Date, TimeEntryInput Body);

public sealed class AddTimeEntryHandler(IAppDbContext db, DailyLogService logs, PlanContext plan) : IHandler<AddTimeEntry, DailyLogDto>
{
    public async Task<DailyLogDto> HandleAsync(AddTimeEntry r, CancellationToken ct)
    {
        if (r.Body.Source == TimeSource.Derived)
        {
            throw RuleViolationException.For("source", "Derived time entries are created by their source records.");
        }

        if (r.Date > (await plan.LoadAsync(ct)).Today)
        {
            throw RuleViolationException.For("date", "Time cannot be logged for a future date.");
        }

        var log = await logs.EnsureAsync(r.Date, ct);
        db.TimeEntries.Add(new TimeEntry
        {
            Date = r.Date,
            DailyLogId = log.Id,
            Category = r.Body.Category,
            Minutes = r.Body.Minutes,
            Note = r.Body.Note,
            Source = r.Body.Source,
        });
        await db.SaveChangesAsync(ct);
        return await logs.GetDtoAsync(log.Id, ct);
    }
}

public sealed record UpdateTimeEntry(int Id, TimeEntryInput Body);

public sealed class UpdateTimeEntryHandler(IAppDbContext db, DailyLogService logs) : IHandler<UpdateTimeEntry, DailyLogDto>
{
    public async Task<DailyLogDto> HandleAsync(UpdateTimeEntry r, CancellationToken ct)
    {
        var entry = await db.TimeEntries.FirstOrDefaultAsync(t => t.Id == r.Id, ct) ?? throw new NotFoundException($"Time entry {r.Id}");
        if (entry.Source == TimeSource.Derived)
        {
            throw new ConflictException("This time entry is derived from another record; edit that record instead (BR-19).");
        }

        db.ExpectVersion(entry, r.Body.RowVersion);
        entry.Category = r.Body.Category;
        entry.Minutes = r.Body.Minutes;
        entry.Note = r.Body.Note;
        await db.SaveChangesAsync(ct);
        return await logs.GetDtoAsync(entry.DailyLogId ?? 0, ct);
    }
}

public sealed record DeleteTimeEntry(int Id);

public sealed class DeleteTimeEntryHandler(IAppDbContext db, DailyLogService logs, IClock clock) : IHandler<DeleteTimeEntry, DailyLogDto>
{
    public async Task<DailyLogDto> HandleAsync(DeleteTimeEntry r, CancellationToken ct)
    {
        var entry = await db.TimeEntries.FirstOrDefaultAsync(t => t.Id == r.Id, ct) ?? throw new NotFoundException($"Time entry {r.Id}");
        if (entry.Source == TimeSource.Derived)
        {
            throw new ConflictException("This time entry is derived from another record; delete that record instead (BR-19).");
        }

        entry.SoftDelete(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return await logs.GetDtoAsync(entry.DailyLogId ?? 0, ct);
    }
}
