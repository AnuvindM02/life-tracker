using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;
using SelfTracker.Domain.Services;

namespace SelfTracker.Application.Features;

// ---- Weekly hours (BR-12) ----

public sealed record HourTargetDto(string Label, IReadOnlyList<TimeCategory> Categories, decimal MinHours, decimal MaxHours, bool IsTotal, bool IsStretch, bool IsGuidance, decimal ActualHours);

public sealed record WeeklyHoursDto(DateOnly WeekStart, string Stage, int? PlanWeek, int TotalMinutes, IReadOnlyList<CategoryMinutes> ByCategory, IReadOnlyList<HourTargetDto> Targets);

public sealed class WeeklyHoursService(IAppDbContext db, PlanContext plan)
{
    public async Task<WeeklyHoursDto> ForWeekAsync(DateOnly weekStart, CancellationToken ct)
    {
        weekStart = PlanRules.MondayOf(weekStart);
        var snap = await plan.LoadAsync(ct);
        var entries = await db.TimeEntries.AsNoTracking().Where(t => t.Date >= weekStart && t.Date <= weekStart.AddDays(6))
            .GroupBy(t => t.Category).Select(g => new CategoryMinutes(g.Key, g.Sum(t => t.Minutes))).ToListAsync(ct);

        // Stage by the plan week of the calendar week's first plan day (BR-12).
        int? week = null;
        var phaseNumber = 1;
        if (snap.Calendar is { } cal)
        {
            var first = weekStart < cal.Start ? cal.Start : weekStart;
            var info = cal.Describe(first);
            week = info.Week;
            phaseNumber = info.Phase?.Number ?? 4;
        }

        var stage = PlanRules.HourStage(week ?? 1, phaseNumber);
        var stages = stage switch
        {
            "A" => new[] { "A" },
            "B" => ["B", "Phase1"],
            _ => [stage, "Full"],
        };
        var rows = await db.WeeklyHourTargets.AsNoTracking().Where(t => stages.Contains(t.Stage)).OrderBy(t => t.SortOrder).ToListAsync(ct);
        var total = entries.Sum(e => e.Minutes);
        var targets = rows.Select(t =>
        {
            var cats = (t.Categories ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => Enum.TryParse<TimeCategory>(c.Trim(), out var v) ? v : (TimeCategory?)null).Where(c => c != null).Select(c => c!.Value).ToList();
            var minutes = t.IsTotal ? total : entries.Where(e => cats.Contains(e.Category)).Sum(e => e.Minutes);
            return new HourTargetDto(t.Label, cats, t.MinHours, t.MaxHours, t.IsTotal, t.IsStretch, stage == "B" && t.Stage == "Phase1", Math.Round(minutes / 60m, 1));
        }).ToList();
        return new WeeklyHoursDto(weekStart, stage, week, total, entries.OrderBy(e => e.Category).ToList(), targets);
    }
}

// ---- Weekly review (Template D) ----

public sealed record TagCount(string Tag, int Count);

public sealed record WeeklySnapshot(
    DateOnly WeekStart,
    int LoggedDays,
    int ActiveDays,
    int MvdDays,
    int RestDays,
    int DsaAttempts,
    int CountedAttempts,
    int ColdReviews,
    double? MedianMinutes,
    IReadOnlyList<TagCount> RepeatedMistakes,
    IReadOnlyList<string> DdiaChapters,
    int PagesRead,
    int NotesCompleted,
    int DrillsPracticed,
    int BuildTicks,
    int GymSessions,
    IReadOnlyList<DateOnly> TripsNextFortnight,
    decimal? SleepAverage,
    double? EnergyAverage,
    WeeklyHoursDto Hours);

public sealed record WeeklyReviewDto(
    int Id,
    DateOnly WeekStart,
    int? Energy,
    int? Sleep,
    int? Mood,
    string? ShippedMd,
    string? BlockedMd,
    string? NextSliceMd,
    string? RecoveryNote,
    string? RepeatedRequirement,
    string? Start,
    string? Stop,
    string? Keep,
    WeeklySnapshot? Snapshot,
    int AttachmentCount,
    string RowVersion);

public sealed record WeeklyReviewInput(
    [property: Range(1, 5)] int? Energy,
    [property: Range(1, 5)] int? Sleep,
    [property: Range(1, 5)] int? Mood,
    string? ShippedMd,
    string? BlockedMd,
    string? NextSliceMd,
    [property: StringLength(1000)] string? RecoveryNote,
    [property: StringLength(500)] string? RepeatedRequirement,
    [property: StringLength(1000)] string? Start,
    [property: StringLength(1000)] string? Stop,
    [property: StringLength(1000)] string? Keep,
    string? RowVersion);

public sealed class WeeklyReviewService(IAppDbContext db, PlanContext plan, WeeklyHoursService hours, IClock clock)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    /// <summary>Computed fields of Template D for a Monday-Sunday week (BR-12 weeks).</summary>
    public async Task<WeeklySnapshot> PrefillAsync(DateOnly weekStart, CancellationToken ct)
    {
        weekStart = PlanRules.MondayOf(weekStart);
        var end = weekStart.AddDays(6);
        var snap = await plan.LoadAsync(ct);
        var logs = await db.DailyLogs.AsNoTracking().Where(l => l.Date >= weekStart && l.Date <= end)
            .Select(l => new { l.DayType, l.GymStatus, l.SleepHours, l.Energy, Build = l.Ticks.Any(t => t.Type == TickType.Build) }).ToListAsync(ct);
        var attempts = await db.DsaAttempts.AsNoTracking().Where(a => a.Date >= weekStart && a.Date <= end)
            .Select(a => new { a.Type, a.IsCounted, a.TotalMin }).ToListAsync(ct);
        var tags = await db.DsaAttemptTags.AsNoTracking().Where(t => t.DsaAttempt!.Date >= weekStart && t.DsaAttempt.Date <= end)
            .Select(t => t.Tag!.Name).ToListAsync(ct);
        var repeated = tags.GroupBy(t => t).Where(g => g.Count() >= 2).Select(g => new TagCount(g.Key, g.Count())).OrderByDescending(t => t.Count).ToList();

        var edition = DdiaRules.DisplayEdition(snap.Settings.DdiaEdition);
        var week = snap.Calendar?.Describe(weekStart < (snap.Calendar?.Start ?? weekStart) ? snap.Calendar!.Start : weekStart).Week;
        var chapters = week is { } w
            ? (await db.DdiaChapters.AsNoTracking().Where(c => c.Edition == edition).ToListAsync(ct))
                .Where(c => DdiaRules.CoversWeek(c.PlannedWeekFrom, c.PlannedWeekTo, w))
                .Select(c => $"Ch. {c.Number}: {c.Title} ({c.Status})").ToList()
            : [];
        var pages = await db.ReadingLogs.AsNoTracking().Where(r => r.Date >= weekStart && r.Date <= end).SumAsync(r => (int?)r.PagesRead, ct) ?? 0;
        var weekStartUtc = new DateTimeOffset(weekStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var notes = await db.DdiaNotes.AsNoTracking().CountAsync(n => n.Status == NoteStatus.Complete && n.UpdatedAt >= weekStartUtc && n.UpdatedAt < weekStartUtc.AddDays(7), ct);
        var drills = await db.DesignDrills.AsNoTracking().CountAsync(d => d.PracticedOn >= weekStart && d.PracticedOn <= end, ct);
        var sleep = logs.Where(l => l.SleepHours != null).Select(l => l.SleepHours!.Value).ToList();
        var energy = logs.Where(l => l.Energy != null).Select(l => l.Energy!.Value).ToList();

        return new WeeklySnapshot(
            weekStart,
            logs.Count,
            logs.Count(l => l.DayType is DayType.Full or DayType.Mvd),
            logs.Count(l => l.DayType == DayType.Mvd),
            logs.Count(l => l.DayType == DayType.Rest),
            attempts.Count,
            attempts.Count(a => a.IsCounted),
            attempts.Count(a => a.Type == AttemptType.ColdReview),
            DsaRules.Median(attempts.Where(a => a.Type != AttemptType.ColdReview).Select(a => a.TotalMin)),
            repeated,
            chapters,
            pages,
            notes,
            drills,
            logs.Count(l => l.Build),
            logs.Count(l => l.GymStatus is GymStatus.Done or GymStatus.Modified or GymStatus.Walk or GymStatus.Zone2),
            snap.Travel.TripsBetween(end.AddDays(1), end.AddDays(14)).ToList(),
            sleep.Count > 0 ? Math.Round(sleep.Average(), 1) : null,
            energy.Count > 0 ? Math.Round(energy.Average(), 1) : null,
            await hours.ForWeekAsync(weekStart, ct));
    }

    public async Task<WeeklyReviewDto?> GetAsync(DateOnly weekStart, CancellationToken ct)
    {
        weekStart = PlanRules.MondayOf(weekStart);
        var r = await db.WeeklyReviews.AsNoTracking().FirstOrDefaultAsync(x => x.WeekStart == weekStart, ct);
        return r is null ? null : await ToDtoAsync(r, ct);
    }

    public async Task<IReadOnlyList<WeeklyReviewDto>> ListAsync(CancellationToken ct)
    {
        var rows = await db.WeeklyReviews.AsNoTracking().OrderByDescending(r => r.WeekStart).Take(60).ToListAsync(ct);
        var list = new List<WeeklyReviewDto>();
        foreach (var r in rows)
        {
            list.Add(await ToDtoAsync(r, ct));
        }

        return list;
    }

    public async Task<WeeklyReviewDto> SaveAsync(DateOnly weekStart, WeeklyReviewInput input, CancellationToken ct)
    {
        weekStart = PlanRules.MondayOf(weekStart);
        if (weekStart > PlanRules.MondayOf((await plan.LoadAsync(ct)).Today))
        {
            throw RuleViolationException.For("weekStart", "Future weeks cannot be reviewed yet.");
        }

        var r = await db.WeeklyReviews.FirstOrDefaultAsync(x => x.WeekStart == weekStart, ct);
        if (r is null)
        {
            r = db.WeeklyReviews.Add(new WeeklyReview { WeekStart = weekStart }).Entity;
        }
        else
        {
            db.ExpectVersion(r, input.RowVersion);
        }

        static string? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        r.Energy = input.Energy;
        r.Sleep = input.Sleep;
        r.Mood = input.Mood;
        r.ShippedMd = T(input.ShippedMd);
        r.BlockedMd = T(input.BlockedMd);
        r.NextSliceMd = T(input.NextSliceMd);
        r.RecoveryNote = T(input.RecoveryNote);
        r.RepeatedRequirement = T(input.RepeatedRequirement);
        r.Start = T(input.Start);
        r.Stop = T(input.Stop);
        r.Keep = T(input.Keep);
        r.SnapshotJson = JsonSerializer.Serialize(await PrefillAsync(weekStart, ct), Json);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(r, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var r = await db.WeeklyReviews.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException($"Weekly review {id}");
        r.SoftDelete(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private async Task<WeeklyReviewDto> ToDtoAsync(WeeklyReview r, CancellationToken ct)
    {
        var files = await db.FileLinks.CountAsync(l => l.EntityType == "WeeklyReview" && l.EntityId == r.Id, ct);
        var snapshot = r.SnapshotJson is null ? null : JsonSerializer.Deserialize<WeeklySnapshot>(r.SnapshotJson, Json);
        return new WeeklyReviewDto(r.Id, r.WeekStart, r.Energy, r.Sleep, r.Mood, r.ShippedMd, r.BlockedMd, r.NextSliceMd, r.RecoveryNote, r.RepeatedRequirement,
            r.Start, r.Stop, r.Keep, snapshot, files, r.Version());
    }
}

// ---- Monthly review ----

public sealed record MonthlyPrefill(
    DateOnly Month,
    int PlanDay,
    string? PhaseName,
    int PhaseTasksDone,
    int PhaseTasksTotal,
    int ExitCriteriaDone,
    int ExitCriteriaTotal,
    int ActiveDays,
    int CountedDsa,
    IReadOnlyList<TagCount> MistakeThemes,
    int PostingsCaptured,
    int PostingsTarget,
    IReadOnlyList<SomedayDto> SomedayParked);

public sealed record MonthlyReviewDto(
    int Id,
    DateOnly Month,
    string? PhaseProgressMd,
    string? MistakeThemesMd,
    int PostingsCaptured,
    bool LighterWeekNeeded,
    string? SomedayTriageMd,
    int AttachmentCount,
    string RowVersion);

public sealed record MonthlyReviewInput(
    string? PhaseProgressMd,
    string? MistakeThemesMd,
    [property: Range(0, 1000)] int PostingsCaptured,
    bool LighterWeekNeeded,
    string? SomedayTriageMd,
    string? RowVersion);

public sealed class MonthlyReviewService(IAppDbContext db, PlanContext plan, PlanTaskEnricher tasks, IClock clock)
{
    public const int PostingsTarget = 20;

    public async Task<MonthlyPrefill> PrefillAsync(DateOnly month, CancellationToken ct)
    {
        month = PlanRules.FirstOfMonth(month);
        var end = month.AddMonths(1).AddDays(-1);
        var snap = await plan.LoadAsync(ct);
        var at = snap.Today < end ? snap.Today : end;
        var info = snap.Calendar?.Describe(at);
        var phase = info?.Phase?.Number ?? 1;
        var all = await tasks.AllAsync(ct);
        var inPhase = all.Where(t => t.PhaseNumber == phase).ToList();
        var exits = await db.ExitCriteria.AsNoTracking().Where(e => e.Phase!.Number == phase).Select(e => e.Done).ToListAsync(ct);
        var active = await db.DailyLogs.AsNoTracking().CountAsync(l => l.Date >= month && l.Date <= end && (l.DayType == DayType.Full || l.DayType == DayType.Mvd), ct);
        var counted = await db.DsaAttempts.AsNoTracking().CountAsync(a => a.Date >= month && a.Date <= end && a.IsCounted, ct);
        var themes = (await db.DsaAttemptTags.AsNoTracking().Where(t => t.DsaAttempt!.Date >= month && t.DsaAttempt.Date <= end).Select(t => t.Tag!.Name).ToListAsync(ct))
            .GroupBy(t => t).Select(g => new TagCount(g.Key, g.Count())).OrderByDescending(t => t.Count).Take(8).ToList();
        var parked = await db.SomedayItems.AsNoTracking().Where(s => s.Status == SomedayStatus.Parked).OrderBy(s => s.CreatedAt).Select(SomedayService.Projection).ToListAsync(ct);
        return new MonthlyPrefill(month, info?.Day ?? 0, info?.Phase?.Name, inPhase.Count(t => t.Status == PlanTaskStatus.Done), inPhase.Count,
            exits.Count(d => d), exits.Count, active, counted, themes, 0, PostingsTarget, parked);
    }

    public async Task<MonthlyReviewDto?> GetAsync(DateOnly month, CancellationToken ct)
    {
        month = PlanRules.FirstOfMonth(month);
        return await db.MonthlyReviews.AsNoTracking().Where(m => m.Month == month).Select(Dto()).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<MonthlyReviewDto>> ListAsync(CancellationToken ct) =>
        await db.MonthlyReviews.AsNoTracking().OrderByDescending(m => m.Month).Select(Dto()).ToListAsync(ct);

    public async Task<MonthlyReviewDto> SaveAsync(DateOnly month, MonthlyReviewInput input, CancellationToken ct)
    {
        month = PlanRules.FirstOfMonth(month);
        if (month > PlanRules.FirstOfMonth((await plan.LoadAsync(ct)).Today))
        {
            throw RuleViolationException.For("month", "Future months cannot be reviewed yet.");
        }

        var m = await db.MonthlyReviews.FirstOrDefaultAsync(x => x.Month == month, ct);
        if (m is null)
        {
            m = db.MonthlyReviews.Add(new MonthlyReview { Month = month }).Entity;
        }
        else
        {
            db.ExpectVersion(m, input.RowVersion);
        }

        static string? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        m.PhaseProgressMd = T(input.PhaseProgressMd);
        m.MistakeThemesMd = T(input.MistakeThemesMd);
        m.PostingsCaptured = input.PostingsCaptured;
        m.LighterWeekNeeded = input.LighterWeekNeeded;
        m.SomedayTriageMd = T(input.SomedayTriageMd);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(month, ct))!;
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var m = await db.MonthlyReviews.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException($"Monthly review {id}");
        m.SoftDelete(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private System.Linq.Expressions.Expression<Func<MonthlyReview, MonthlyReviewDto>> Dto() => m => new MonthlyReviewDto(
        m.Id, m.Month, m.PhaseProgressMd, m.MistakeThemesMd, m.PostingsCaptured, m.LighterWeekNeeded, m.SomedayTriageMd,
        db.FileLinks.Count(l => l.EntityType == "MonthlyReview" && l.EntityId == m.Id), Convert.ToBase64String(m.RowVersion));
}

// ---- Someday list ----

public sealed record SomedayDto(int Id, string Title, string? NotesMd, SomedayCategory Category, SomedayStatus Status, DateOnly CreatedOn, DateOnly? ReviewedOn, int? PromotedTaskId, string RowVersion);

public sealed record SomedayInput([property: Required, StringLength(200)] string Title, string? NotesMd, SomedayCategory Category, SomedayStatus Status, string? RowVersion) : IVersioned;

public enum SomedayTriage { Keep, Drop }

public sealed record SomedayTriageInput(SomedayTriage Action);

public sealed class SomedaySpec : ICrudSpec<SomedayItem, SomedayDto, SomedayInput>
{
    public string Name => "Someday item";

    public string DefaultSort => "-createdAt";

    public System.Linq.Expressions.Expression<Func<SomedayItem, SomedayDto>> Projection => SomedayService.Projection;

    public IQueryable<SomedayItem> Filter(IQueryable<SomedayItem> query, IReadOnlyDictionary<string, string?> filters) =>
        filters.TryGetValue("status", out var s) && Enum.TryParse<SomedayStatus>(s, true, out var status) ? query.Where(x => x.Status == status) : query;

    public void Apply(SomedayInput input, SomedayItem entity)
    {
        entity.Title = input.Title.Trim();
        entity.NotesMd = string.IsNullOrWhiteSpace(input.NotesMd) ? null : input.NotesMd.Trim();
        entity.Category = input.Category;
        entity.Status = input.Status;
    }
}

public sealed class SomedayService(IAppDbContext db, PlanContext plan)
{
    public static readonly System.Linq.Expressions.Expression<Func<SomedayItem, SomedayDto>> Projection = s => new SomedayDto(
        s.Id, s.Title, s.NotesMd, s.Category, s.Status, DateOnly.FromDateTime(s.CreatedAt.UtcDateTime), s.ReviewedOn, s.PromotedTaskId, Convert.ToBase64String(s.RowVersion));

    /// <summary>Promote an idea into a manual plan task in the current phase (one week window).</summary>
    public async Task<SomedayDto> PromoteAsync(int id, CancellationToken ct)
    {
        var item = await db.SomedayItems.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException($"Someday item {id}");
        if (item.Status == SomedayStatus.Promoted)
        {
            throw new ConflictException("This idea is already a plan task.");
        }

        var snap = await plan.LoadAsync(ct);
        var info = snap.RequireCalendar().Describe(snap.Today);
        var day = Math.Clamp(Math.Max(info.Day, 1), 1, PlanCalendar.PlanLength);
        var phaseNumber = info.Phase?.Number ?? 4;
        var phaseId = await db.Phases.Where(p => p.Number == phaseNumber).Select(p => p.Id).FirstAsync(ct);
        var task = new PlanTask
        {
            PhaseId = phaseId,
            Track = PlanTrack.Other,
            Kind = PlanTaskKind.Manual,
            Title = item.Title,
            Description = item.NotesMd,
            DayFrom = day,
            DayTo = Math.Min(day + 6, PlanCalendar.PlanLength),
            SortOrder = 900,
        };
        db.PlanTasks.Add(task);
        await db.SaveChangesAsync(ct);
        item.Status = SomedayStatus.Promoted;
        item.ReviewedOn = snap.Today;
        item.PromotedTaskId = task.Id;
        await db.SaveChangesAsync(ct);
        return await db.SomedayItems.AsNoTracking().Where(s => s.Id == id).Select(Projection).FirstAsync(ct);
    }

    /// <summary>Monthly triage: keep (stays parked, marked reviewed) or drop.</summary>
    public async Task<SomedayDto> TriageAsync(int id, SomedayTriage action, CancellationToken ct)
    {
        var item = await db.SomedayItems.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException($"Someday item {id}");
        item.ReviewedOn = (await plan.LoadAsync(ct)).Today;
        item.Status = action == SomedayTriage.Drop ? SomedayStatus.Dropped : SomedayStatus.Parked;
        await db.SaveChangesAsync(ct);
        return await db.SomedayItems.AsNoTracking().Where(s => s.Id == id).Select(Projection).FirstAsync(ct);
    }
}
