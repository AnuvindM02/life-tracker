using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;
using SelfTracker.Domain.Services;

namespace SelfTracker.Application.Features;

// ---- Chapters ----

public sealed record DdiaChapterDto(
    int Id,
    DdiaEdition Edition,
    int Number,
    string Title,
    int PlannedWeekFrom,
    int PlannedWeekTo,
    string? Topics,
    string? AskOpsConnection,
    int? TotalPages,
    int PagesRead,
    ChapterStatus Status,
    DateOnly? StartedOn,
    DateOnly? FinishedOn,
    int? NoteId,
    NoteStatus? NoteStatus,
    int ReadingMinutes,
    bool IsUserModified,
    string RowVersion);

public sealed record DdiaChapterInput(
    DdiaEdition Edition,
    [property: Range(1, 50)] int Number,
    [property: Required, StringLength(200)] string Title,
    [property: Range(1, 52)] int PlannedWeekFrom,
    [property: Range(1, 52)] int PlannedWeekTo,
    [property: StringLength(2000)] string? Topics,
    [property: StringLength(2000)] string? AskOpsConnection,
    [property: Range(1, 2000)] int? TotalPages,
    ChapterStatus Status,
    string? RowVersion) : IVersioned;

public sealed class DdiaChapterSpec(IAppDbContext db) : ICrudSpec<DdiaChapter, DdiaChapterDto, DdiaChapterInput>
{
    public string Name => "DDIA chapter";

    public string DefaultSort => "number";

    public Expression<Func<DdiaChapter, DdiaChapterDto>> Projection => c => new DdiaChapterDto(
        c.Id, c.Edition, c.Number, c.Title, c.PlannedWeekFrom, c.PlannedWeekTo, c.Topics, c.AskOpsConnection, c.TotalPages, c.PagesRead, c.Status,
        c.StartedOn, c.FinishedOn,
        db.DdiaNotes.Where(n => n.ChapterId == c.Id).Select(n => (int?)n.Id).FirstOrDefault(),
        db.DdiaNotes.Where(n => n.ChapterId == c.Id).Select(n => (NoteStatus?)n.Status).FirstOrDefault(),
        db.ReadingLogs.Where(r => r.ChapterId == c.Id).Sum(r => (int?)r.Minutes) ?? 0,
        c.IsUserModified,
        Convert.ToBase64String(c.RowVersion));

    public IQueryable<DdiaChapter> Filter(IQueryable<DdiaChapter> query, IReadOnlyDictionary<string, string?> filters) =>
        filters.TryGetValue("edition", out var e) && Enum.TryParse<DdiaEdition>(e, true, out var edition) ? query.Where(c => c.Edition == edition) : query;

    public Task ValidateAsync(DdiaChapterInput input, DdiaChapter? existing, CancellationToken ct)
    {
        if (input.Edition == DdiaEdition.Unconfirmed)
        {
            throw RuleViolationException.For("edition", "A chapter belongs to the First or Second edition.");
        }

        if (input.PlannedWeekTo < input.PlannedWeekFrom)
        {
            throw RuleViolationException.For("plannedWeekTo", "The last planned week must be on or after the first.");
        }

        return Task.CompletedTask;
    }

    public async Task ValidateDeleteAsync(DdiaChapter entity, CancellationToken ct)
    {
        if (await db.ReadingLogs.AnyAsync(r => r.ChapterId == entity.Id, ct) || await db.DdiaNotes.AnyAsync(n => n.ChapterId == entity.Id, ct))
        {
            throw new ConflictException("This chapter has reading logs or a note; delete those first.");
        }
    }

    public void Apply(DdiaChapterInput input, DdiaChapter entity)
    {
        entity.Edition = input.Edition;
        entity.Number = input.Number;
        entity.Title = input.Title.Trim();
        entity.PlannedWeekFrom = input.PlannedWeekFrom;
        entity.PlannedWeekTo = input.PlannedWeekTo;
        entity.Topics = input.Topics;
        entity.AskOpsConnection = input.AskOpsConnection;
        entity.TotalPages = input.TotalPages;
        entity.Status = input.Status;
        if (entity.TotalPages is { } total && entity.PagesRead >= total && entity.FinishedOn is null && entity.StartedOn is not null)
        {
            entity.FinishedOn = entity.StartedOn;
        }
    }
}

public sealed record DdiaWeekChapter(int Id, int Number, string Title, ChapterStatus Status, int PagesRead, int? TotalPages);

public sealed record DdiaSummaryDto(
    DdiaEdition SettingsEdition,
    DdiaEdition DisplayEdition,
    bool ShowEditionWarning,
    string EditionWarning,
    int? PlanWeek,
    IReadOnlyList<DdiaWeekChapter> WeekChapters,
    int Chapters,
    int NotesDone,
    int Explained,
    int PagesPerDayMin,
    int PagesPerDayMax);

public sealed record GetDdiaSummary;

public sealed class GetDdiaSummaryHandler(IAppDbContext db, PlanContext plan) : IHandler<GetDdiaSummary, DdiaSummaryDto>
{
    public async Task<DdiaSummaryDto> HandleAsync(GetDdiaSummary r, CancellationToken ct)
    {
        var snap = await plan.LoadAsync(ct);
        var edition = DdiaRules.DisplayEdition(snap.Settings.DdiaEdition);
        var week = snap.Calendar is { } cal ? cal.Describe(snap.Today).Week : null;
        var chapters = await db.DdiaChapters.AsNoTracking().Where(c => c.Edition == edition).OrderBy(c => c.Number).ToListAsync(ct);
        var weekChapters = week is { } w
            ? chapters.Where(c => DdiaRules.CoversWeek(c.PlannedWeekFrom, c.PlannedWeekTo, w))
                .Select(c => new DdiaWeekChapter(c.Id, c.Number, c.Title, c.Status, c.PagesRead, c.TotalPages)).ToList()
            : [];
        return new DdiaSummaryDto(snap.Settings.DdiaEdition, edition, DdiaRules.ShowEditionWarning(snap.Settings.DdiaEdition, week), DdiaRules.EditionWarning,
            week, weekChapters, chapters.Count, chapters.Count(c => c.Status >= ChapterStatus.NoteDone), chapters.Count(c => c.Status == ChapterStatus.Explained),
            DdiaRules.PagesPerDayMin, DdiaRules.PagesPerDayMax);
    }
}

/// <summary>Edition-neutral DDIA plan tasks resolve their chapters at display time (spec v1.1, 4.5).</summary>
public sealed class DdiaTaskResolver(IAppDbContext db, PlanContext plan)
{
    public async Task<IReadOnlyList<PlanTaskDto>> ResolveAsync(IReadOnlyList<PlanTaskDto> tasks, CancellationToken ct)
    {
        if (!tasks.Any(t => t.Kind == PlanTaskKind.DerivedDdia))
        {
            return tasks;
        }

        var settingsEdition = (await plan.LoadAsync(ct)).Settings.DdiaEdition;
        var edition = DdiaRules.DisplayEdition(settingsEdition);
        var chapters = await db.DdiaChapters.AsNoTracking().Where(c => c.Edition == edition).OrderBy(c => c.Number)
            .Select(c => new { c.Number, c.Title, c.PlannedWeekFrom, c.PlannedWeekTo }).ToListAsync(ct);
        var suffix = settingsEdition == DdiaEdition.Unconfirmed ? " (2nd edition, unconfirmed)" : "";
        return tasks.Select(t => t.Kind == PlanTaskKind.DerivedDdia && t.PlanWeek is { } w
            ? t with
            {
                DdiaChapters = chapters.Where(c => DdiaRules.CoversWeek(c.PlannedWeekFrom, c.PlannedWeekTo, w))
                    .Select(c => $"Ch. {c.Number}: {c.Title}{suffix}").ToList(),
            }
            : t).ToList();
    }
}

public static class DdiaProgress
{
    /// <summary>Pages read, start and finish dates and the Reading status follow the reading log.</summary>
    public static async Task RecomputeChapterAsync(IAppDbContext db, int chapterId, CancellationToken ct)
    {
        var chapter = await db.DdiaChapters.FirstOrDefaultAsync(c => c.Id == chapterId, ct);
        if (chapter is null)
        {
            return;
        }

        var logs = await db.ReadingLogs.Where(r => r.ChapterId == chapterId).Select(r => new { r.Date, r.PagesRead }).ToListAsync(ct);
        chapter.PagesRead = logs.Sum(l => l.PagesRead);
        chapter.StartedOn = logs.Count > 0 ? logs.Min(l => l.Date) : null;
        if (logs.Count > 0)
        {
            chapter.Status = DdiaRules.AfterReading(chapter.Status);
        }

        chapter.FinishedOn = chapter.TotalPages is { } total && chapter.PagesRead >= total && logs.Count > 0
            ? logs.OrderBy(l => l.Date).Aggregate((Sum: 0, Date: (DateOnly?)null), (acc, l) => acc.Sum >= total ? acc : (acc.Sum + l.PagesRead, l.Date)).Date
            : null;
    }
}

// ---- Reading log ----

public sealed record ReadingLogDto(int Id, DateOnly Date, int ChapterId, string ChapterTitle, int ChapterNumber, DdiaEdition Edition, int PagesRead, int Minutes, string? Note, string RowVersion);

public sealed record ReadingLogInput(
    [property: Required] DateOnly Date,
    int ChapterId,
    [property: Range(0, 300)] int PagesRead,
    [property: Range(0, 600)] int Minutes,
    [property: StringLength(500)] string? Note,
    string? RowVersion) : IVersioned;

public sealed class ReadingLogService(IAppDbContext db, PlanContext plan, DerivedTime time, IClock clock)
{
    public const string SourceType = "ReadingLog";

    public static readonly Expression<Func<ReadingLog, ReadingLogDto>> Projection = r => new ReadingLogDto(
        r.Id, r.Date, r.ChapterId, r.Chapter!.Title, r.Chapter.Number, r.Chapter.Edition, r.PagesRead, r.Minutes, r.Note, Convert.ToBase64String(r.RowVersion));

    public Task<PagedResult<ReadingLogDto>> ListAsync(int? chapterId, DateOnly? from, DateOnly? to, ListQuery list, CancellationToken ct)
    {
        var q = db.ReadingLogs.AsNoTracking();
        if (chapterId is { } c) q = q.Where(r => r.ChapterId == c);
        if (from is { } f) q = q.Where(r => r.Date >= f);
        if (to is { } t) q = q.Where(r => r.Date <= t);
        return q.ApplySort(list.Sort, "-date").Select(Projection).ToPagedAsync(list, ct);
    }

    public async Task<ReadingLogDto> SaveAsync(int? id, ReadingLogInput input, CancellationToken ct)
    {
        if (input.Date > (await plan.LoadAsync(ct)).Today)
        {
            throw RuleViolationException.For("date", "Reading cannot be logged for a future date.");
        }

        if (input.PagesRead == 0 && input.Minutes == 0)
        {
            throw RuleViolationException.For("pagesRead", "Log pages, minutes or both.");
        }

        var chapter = await db.DdiaChapters.FirstOrDefaultAsync(c => c.Id == input.ChapterId, ct)
            ?? throw RuleViolationException.For("chapterId", "Choose a chapter.");
        ReadingLog log;
        int? previousChapter = null;
        if (id is { } existingId)
        {
            log = await db.ReadingLogs.FirstOrDefaultAsync(r => r.Id == existingId, ct) ?? throw new NotFoundException($"Reading log {existingId}");
            db.ExpectVersion(log, input.RowVersion);
            previousChapter = log.ChapterId != chapter.Id ? log.ChapterId : null;
        }
        else
        {
            log = db.ReadingLogs.Add(new ReadingLog()).Entity;
        }

        log.Date = input.Date;
        log.ChapterId = chapter.Id;
        log.PagesRead = input.PagesRead;
        log.Minutes = input.Minutes;
        log.Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();

        await Transactions.RunAsync(db, async () =>
        {
            await db.SaveChangesAsync(ct);
            await time.SyncAsync(SourceType, log.Id, log.Date, log.Minutes, TimeCategory.DdiaDesign, $"DDIA reading: ch. {chapter.Number} {chapter.Title}, {log.PagesRead} pages", ct);
            await db.SaveChangesAsync(ct);
            await DdiaProgress.RecomputeChapterAsync(db, chapter.Id, ct);
            if (previousChapter is { } prev)
            {
                await DdiaProgress.RecomputeChapterAsync(db, prev, ct);
            }

            await db.SaveChangesAsync(ct);
        }, ct);
        return await db.ReadingLogs.AsNoTracking().Where(r => r.Id == log.Id).Select(Projection).FirstAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var log = await db.ReadingLogs.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException($"Reading log {id}");
        var now = clock.UtcNow;
        log.SoftDelete(now);
        await time.SoftDeleteAsync(SourceType, id, now, ct);
        await db.SaveChangesAsync(ct);
        await DdiaProgress.RecomputeChapterAsync(db, log.ChapterId, ct);
        await db.SaveChangesAsync(ct);
    }
}

public static class Transactions
{
    /// <summary>Run a unit of work in one transaction inside the SQL retry strategy (user transactions need it).</summary>
    public static Task RunAsync(IAppDbContext db, Func<Task> work, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await work();
            await tx.CommitAsync(ct);
        });
    }
}

// ---- Chapter notes (Template C) ----

public sealed record InterviewQaDto(int Id, string Question, string? Answer60sMd, int SortOrder);

public sealed record DdiaNoteDto(
    int Id,
    int ChapterId,
    DdiaEdition Edition,
    int ChapterNumber,
    string ChapterTitle,
    string? KeyIdea1,
    string? KeyIdea2,
    string? KeyIdea3,
    string? KeyIdea4,
    string? KeyIdea5,
    string? TradeOffsMd,
    string? FailureModesMd,
    string? AskOpsDecision,
    int? ExplanationRating,
    string? ExplanationNotes,
    NoteStatus Status,
    IReadOnlyList<string> Missing,
    IReadOnlyList<InterviewQaDto> Questions,
    ChapterStatus ChapterStatus,
    DateTimeOffset UpdatedAt,
    string RowVersion);

public sealed record InterviewQaInput([property: StringLength(500)] string? Question, string? Answer60sMd);

public sealed record DdiaNoteInput(
    [property: StringLength(2000)] string? KeyIdea1,
    [property: StringLength(2000)] string? KeyIdea2,
    [property: StringLength(2000)] string? KeyIdea3,
    [property: StringLength(2000)] string? KeyIdea4,
    [property: StringLength(2000)] string? KeyIdea5,
    string? TradeOffsMd,
    string? FailureModesMd,
    [property: StringLength(2000)] string? AskOpsDecision,
    [property: Range(1, 5)] int? ExplanationRating,
    [property: StringLength(2000)] string? ExplanationNotes,
    InterviewQaInput[]? Questions,
    string? RowVersion);

public sealed class DdiaNoteService(IAppDbContext db, IClock clock)
{
    public async Task<DdiaNoteDto?> GetByChapterAsync(int chapterId, CancellationToken ct)
    {
        var id = await db.DdiaNotes.Where(n => n.ChapterId == chapterId).Select(n => (int?)n.Id).FirstOrDefaultAsync(ct);
        return id is { } noteId ? await GetAsync(noteId, ct) : null;
    }

    public async Task<DdiaNoteDto> GetAsync(int id, CancellationToken ct)
    {
        var n = await db.DdiaNotes.AsNoTracking().Include(x => x.Chapter).Include(x => x.Questions)
            .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException($"DDIA note {id}");
        var qs = n.Questions.OrderBy(q => q.SortOrder).ThenBy(q => q.Id).ToList();
        return new DdiaNoteDto(n.Id, n.ChapterId, n.Chapter!.Edition, n.Chapter.Number, n.Chapter.Title, n.KeyIdea1, n.KeyIdea2, n.KeyIdea3, n.KeyIdea4,
            n.KeyIdea5, n.TradeOffsMd, n.FailureModesMd, n.AskOpsDecision, n.ExplanationRating, n.ExplanationNotes, n.Status, DdiaRules.MissingFromNote(Content(n, qs)),
            qs.Select(q => new InterviewQaDto(q.Id, q.Question, q.Answer60sMd, q.SortOrder)).ToList(), n.Chapter.Status, n.UpdatedAt, n.Version());
    }

    public async Task<DdiaNoteDto> UpsertAsync(int chapterId, DdiaNoteInput input, CancellationToken ct)
    {
        var chapter = await db.DdiaChapters.FirstOrDefaultAsync(c => c.Id == chapterId, ct) ?? throw new NotFoundException($"DDIA chapter {chapterId}");
        var note = await db.DdiaNotes.Include(n => n.Questions).FirstOrDefaultAsync(n => n.ChapterId == chapterId, ct);
        if (note is null)
        {
            note = db.DdiaNotes.Add(new DdiaNote { ChapterId = chapterId }).Entity;
        }
        else
        {
            db.ExpectVersion(note, input.RowVersion);
        }

        static string? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        note.KeyIdea1 = T(input.KeyIdea1);
        note.KeyIdea2 = T(input.KeyIdea2);
        note.KeyIdea3 = T(input.KeyIdea3);
        note.KeyIdea4 = T(input.KeyIdea4);
        note.KeyIdea5 = T(input.KeyIdea5);
        note.TradeOffsMd = T(input.TradeOffsMd);
        note.FailureModesMd = T(input.FailureModesMd);
        note.AskOpsDecision = T(input.AskOpsDecision);
        note.ExplanationRating = input.ExplanationRating;
        note.ExplanationNotes = T(input.ExplanationNotes);

        // Questions are replaced as a set, keeping order.
        var wanted = (input.Questions ?? []).Where(q => !string.IsNullOrWhiteSpace(q.Question) || !string.IsNullOrWhiteSpace(q.Answer60sMd)).ToList();
        var now = clock.UtcNow;
        var current = note.Questions.OrderBy(q => q.SortOrder).ThenBy(q => q.Id).ToList();
        for (var i = 0; i < Math.Max(wanted.Count, current.Count); i++)
        {
            if (i >= wanted.Count)
            {
                current[i].SoftDelete(now);
                continue;
            }

            var qa = i < current.Count ? current[i] : note.Questions.AddAndReturn(new InterviewQa());
            qa.Question = (wanted[i].Question ?? "").Trim();
            qa.Answer60sMd = T(wanted[i].Answer60sMd);
            qa.SortOrder = i;
        }

        var live = note.Questions.Where(q => !q.IsDeleted).OrderBy(q => q.SortOrder).ToList();
        var complete = DdiaRules.MissingFromNote(Content(note, live)).Count == 0;
        note.Status = complete ? NoteStatus.Complete : NoteStatus.Draft;
        chapter.Status = DdiaRules.AfterNote(chapter.Status, complete, note.ExplanationRating);
        await db.SaveChangesAsync(ct);
        return await GetAsync(note.Id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var note = await db.DdiaNotes.Include(n => n.Questions).FirstOrDefaultAsync(n => n.Id == id, ct) ?? throw new NotFoundException($"DDIA note {id}");
        var now = clock.UtcNow;
        note.SoftDelete(now);
        note.Questions.ForEach(q => q.SoftDelete(now));
        await db.SaveChangesAsync(ct);
    }

    private static NoteContent Content(DdiaNote n, IEnumerable<InterviewQa> qs) => new(
        [n.KeyIdea1, n.KeyIdea2, n.KeyIdea3, n.KeyIdea4, n.KeyIdea5], n.TradeOffsMd, n.FailureModesMd, n.AskOpsDecision,
        qs.Select(q => (q.Question, q.Answer60sMd)).ToList());
}

internal static class ListExtensions
{
    public static T AddAndReturn<T>(this List<T> list, T item)
    {
        list.Add(item);
        return item;
    }
}

public sealed record DdiaNoteSummaryDto(int Id, int ChapterId, DdiaEdition Edition, int ChapterNumber, string ChapterTitle, NoteStatus Status, int? ExplanationRating, DateTimeOffset UpdatedAt);

// ---- Design drills ----

public sealed record DesignDrillDto(
    int Id,
    int Number,
    string Prompt,
    string Group,
    int? PlannedWeek,
    DrillStatus Status,
    string? ClarifyNotes,
    string? EstimateNotes,
    string? ApiDataNotes,
    string? HighLevelNotes,
    string? DeepDiveNotes,
    string? TradeOffNotes,
    bool SpokenMock,
    DateOnly? PracticedOn,
    int Minutes,
    DateOnly? ReviewedOn,
    int AttachmentCount,
    string RowVersion);

public sealed record DesignDrillInput(
    [property: Range(1, 999)] int Number,
    [property: Required, StringLength(200)] string Prompt,
    [property: StringLength(20)] string? Group,
    [property: Range(1, 52)] int? PlannedWeek,
    DrillStatus Status,
    string? ClarifyNotes,
    string? EstimateNotes,
    string? ApiDataNotes,
    string? HighLevelNotes,
    string? DeepDiveNotes,
    string? TradeOffNotes,
    bool SpokenMock,
    DateOnly? PracticedOn,
    [property: Range(0, 600)] int Minutes,
    string? RowVersion) : IVersioned;

public sealed class DesignDrillService(IAppDbContext db, PlanContext plan, DerivedTime time, IClock clock)
{
    public const string SourceType = "DesignDrill";

    public Expression<Func<DesignDrill, DesignDrillDto>> Projection => d => new DesignDrillDto(
        d.Id, d.Number, d.Prompt, d.Group, d.PlannedWeek, d.Status, d.ClarifyNotes, d.EstimateNotes, d.ApiDataNotes, d.HighLevelNotes, d.DeepDiveNotes,
        d.TradeOffNotes, d.SpokenMock, d.PracticedOn, d.Minutes, d.ReviewedOn,
        db.FileLinks.Count(l => l.EntityType == SourceType && l.EntityId == d.Id),
        Convert.ToBase64String(d.RowVersion));

    public async Task<IReadOnlyList<DesignDrillDto>> ListAsync(DrillStatus? status, CancellationToken ct)
    {
        var q = db.DesignDrills.AsNoTracking();
        if (status is { } s) q = q.Where(d => d.Status == s);
        return await q.OrderBy(d => d.Number).Select(Projection).ToListAsync(ct);
    }

    public async Task<DesignDrillDto> GetAsync(int id, CancellationToken ct) =>
        await db.DesignDrills.AsNoTracking().Where(d => d.Id == id).Select(Projection).FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Design drill {id}");

    public async Task<DesignDrillDto> SaveAsync(int? id, DesignDrillInput input, CancellationToken ct)
    {
        static string? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        var snap = await plan.LoadAsync(ct);
        if (input.PracticedOn > snap.Today)
        {
            throw RuleViolationException.For("practicedOn", "Pick today or an earlier date.");
        }

        var segments = new[] { input.ClarifyNotes, input.EstimateNotes, input.ApiDataNotes, input.HighLevelNotes, input.DeepDiveNotes, input.TradeOffNotes };
        if (input.Status == DrillStatus.Written && DdiaRules.MissingForWritten(segments) is { Count: > 0 } missing)
        {
            throw RuleViolationException.For("status", $"A written drill covers all six parts of the 45-minute framework. Missing: {string.Join(", ", missing)}.");
        }

        DesignDrill drill;
        if (id is { } existingId)
        {
            drill = await db.DesignDrills.FirstOrDefaultAsync(d => d.Id == existingId, ct) ?? throw new NotFoundException($"Design drill {existingId}");
            db.ExpectVersion(drill, input.RowVersion);
        }
        else
        {
            drill = db.DesignDrills.Add(new DesignDrill()).Entity;
        }

        drill.Number = input.Number;
        drill.Prompt = input.Prompt.Trim();
        drill.Group = T(input.Group) ?? "";
        drill.PlannedWeek = input.PlannedWeek;
        drill.Status = input.Status;
        drill.ClarifyNotes = T(input.ClarifyNotes);
        drill.EstimateNotes = T(input.EstimateNotes);
        drill.ApiDataNotes = T(input.ApiDataNotes);
        drill.HighLevelNotes = T(input.HighLevelNotes);
        drill.DeepDiveNotes = T(input.DeepDiveNotes);
        drill.TradeOffNotes = T(input.TradeOffNotes);
        drill.SpokenMock = input.SpokenMock || input.Status == DrillStatus.SpokenMock;
        drill.PracticedOn = input.PracticedOn;
        drill.Minutes = input.Minutes;
        drill.ReviewedOn = input.Status == DrillStatus.Reviewed ? drill.ReviewedOn ?? snap.Today : null;

        await Transactions.RunAsync(db, async () =>
        {
            await db.SaveChangesAsync(ct);
            await time.SyncAsync(SourceType, drill.Id, drill.PracticedOn, drill.Minutes, TimeCategory.DdiaDesign, $"Design drill {drill.Number}: {drill.Prompt}", ct);
            await db.SaveChangesAsync(ct);
        }, ct);
        return await GetAsync(drill.Id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var drill = await db.DesignDrills.FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException($"Design drill {id}");
        var now = clock.UtcNow;
        drill.SoftDelete(now);
        await time.SoftDeleteAsync(SourceType, id, now, ct);
        await db.SaveChangesAsync(ct);
    }
}

// ---- Articulation ----

public sealed record ArticulationLogDto(
    int Id,
    DateOnly Date,
    ArticulationKind Kind,
    string Prompt,
    string? AnswerMd,
    string? TradeOffsMd,
    string? EvidenceMd,
    int? SelfRating,
    string? Notes,
    int Minutes,
    int AttachmentCount,
    string RowVersion);

public sealed record ArticulationLogInput(
    [property: Required] DateOnly Date,
    ArticulationKind Kind,
    [property: Required, StringLength(500)] string Prompt,
    string? AnswerMd,
    string? TradeOffsMd,
    string? EvidenceMd,
    [property: Range(1, 5)] int? SelfRating,
    [property: StringLength(2000)] string? Notes,
    [property: Range(0, 600)] int Minutes,
    string? RowVersion) : IVersioned;

public sealed class ArticulationService(IAppDbContext db, PlanContext plan, DerivedTime time, IClock clock)
{
    public const string SourceType = "ArticulationLog";

    public Expression<Func<ArticulationLog, ArticulationLogDto>> Projection => a => new ArticulationLogDto(
        a.Id, a.Date, a.Kind, a.Prompt, a.AnswerMd, a.TradeOffsMd, a.EvidenceMd, a.SelfRating, a.Notes, a.Minutes,
        db.FileLinks.Count(l => l.EntityType == SourceType && l.EntityId == a.Id), Convert.ToBase64String(a.RowVersion));

    public Task<PagedResult<ArticulationLogDto>> ListAsync(ArticulationKind? kind, DateOnly? from, DateOnly? to, ListQuery list, CancellationToken ct)
    {
        var q = db.ArticulationLogs.AsNoTracking();
        if (kind is { } k) q = q.Where(a => a.Kind == k);
        if (from is { } f) q = q.Where(a => a.Date >= f);
        if (to is { } t) q = q.Where(a => a.Date <= t);
        return q.ApplySort(list.Sort, "-date").Select(Projection).ToPagedAsync(list, ct);
    }

    public async Task<ArticulationLogDto> GetAsync(int id, CancellationToken ct) =>
        await db.ArticulationLogs.AsNoTracking().Where(a => a.Id == id).Select(Projection).FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Articulation log {id}");

    public async Task<ArticulationLogDto> SaveAsync(int? id, ArticulationLogInput input, CancellationToken ct)
    {
        static string? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        if (input.Date > (await plan.LoadAsync(ct)).Today)
        {
            throw RuleViolationException.For("date", "Pick today or an earlier date.");
        }

        ArticulationLog a;
        if (id is { } existingId)
        {
            a = await db.ArticulationLogs.FirstOrDefaultAsync(x => x.Id == existingId, ct) ?? throw new NotFoundException($"Articulation log {existingId}");
            db.ExpectVersion(a, input.RowVersion);
        }
        else
        {
            a = db.ArticulationLogs.Add(new ArticulationLog()).Entity;
        }

        a.Date = input.Date;
        a.Kind = input.Kind;
        a.Prompt = input.Prompt.Trim();
        a.AnswerMd = T(input.AnswerMd);
        a.TradeOffsMd = T(input.TradeOffsMd);
        a.EvidenceMd = T(input.EvidenceMd);
        a.SelfRating = input.SelfRating;
        a.Notes = T(input.Notes);
        a.Minutes = input.Minutes;

        await Transactions.RunAsync(db, async () =>
        {
            await db.SaveChangesAsync(ct);
            await time.SyncAsync(SourceType, a.Id, a.Date, a.Minutes, TimeCategory.DdiaDesign, $"Articulation: {a.Prompt}", ct);
            await db.SaveChangesAsync(ct);
        }, ct);
        return await GetAsync(a.Id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var a = await db.ArticulationLogs.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException($"Articulation log {id}");
        var now = clock.UtcNow;
        a.SoftDelete(now);
        await time.SoftDeleteAsync(SourceType, id, now, ct);
        await db.SaveChangesAsync(ct);
    }
}

public sealed record ArticulationPromptDto(int Id, ArticulationKind Kind, string Text, int SortOrder, bool IsActive, string RowVersion);

public sealed record ArticulationPromptInput(ArticulationKind Kind, [property: Required, StringLength(500)] string Text, int SortOrder, bool IsActive, string? RowVersion) : IVersioned;

public sealed class ArticulationPromptSpec : ICrudSpec<ArticulationPrompt, ArticulationPromptDto, ArticulationPromptInput>
{
    public string Name => "Articulation prompt";

    public string DefaultSort => "sortOrder";

    public Expression<Func<ArticulationPrompt, ArticulationPromptDto>> Projection =>
        p => new ArticulationPromptDto(p.Id, p.Kind, p.Text, p.SortOrder, p.IsActive, Convert.ToBase64String(p.RowVersion));

    public void Apply(ArticulationPromptInput input, ArticulationPrompt entity)
    {
        entity.Kind = input.Kind;
        entity.Text = input.Text.Trim();
        entity.SortOrder = input.SortOrder;
        entity.IsActive = input.IsActive;
    }
}

// ---- Artifacts a Core tick can link to (BR-06) ----

public sealed record LinkableArtifact(string EntityType, int Id, string Label);

public sealed record ListLinkableArtifacts(DateOnly Date);

public sealed class ListLinkableArtifactsHandler(IAppDbContext db) : IHandler<ListLinkableArtifacts, IReadOnlyList<LinkableArtifact>>
{
    public async Task<IReadOnlyList<LinkableArtifact>> HandleAsync(ListLinkableArtifacts r, CancellationToken ct)
    {
        var notes = await db.DdiaNotes.AsNoTracking().OrderByDescending(n => n.UpdatedAt).Take(30)
            .Select(n => new LinkableArtifact("DdiaNote", n.Id, "Note: " + (n.Chapter!.Edition == DdiaEdition.First ? "1st" : "2nd") + " ed. ch. " + n.Chapter.Number + " " + n.Chapter.Title))
            .ToListAsync(ct);
        var drills = await db.DesignDrills.AsNoTracking().Where(d => d.PracticedOn == r.Date || d.Status != DrillStatus.NotStarted).OrderByDescending(d => d.PracticedOn).Take(20)
            .Select(d => new LinkableArtifact("DesignDrill", d.Id, "Drill " + d.Number + ": " + d.Prompt)).ToListAsync(ct);
        var talks = await db.ArticulationLogs.AsNoTracking().Where(a => a.Date >= r.Date.AddDays(-7) && a.Date <= r.Date).OrderByDescending(a => a.Date).Take(20)
            .Select(a => new LinkableArtifact("ArticulationLog", a.Id, "Articulation: " + a.Prompt)).ToListAsync(ct);
        return [.. notes, .. drills, .. talks];
    }
}
