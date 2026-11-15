using Microsoft.EntityFrameworkCore;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;
using SelfTracker.Domain.Services;

namespace SelfTracker.Application.Features;

public sealed record NudgeDto(string Key, string Title, string Message, string ActionLabel, string ActionLink);

public sealed record ScheduleBlockTodayDto(int Id, string Time, string Title, string Rule, bool IsStretch, bool Done);

public sealed record GymTodayDto(string SessionName, bool IsOptional, string? Notes);

public sealed record MetricsDto(ActiveWindow Active30, MissStatus Misses);

public sealed record TodayView(
    DateOnly Date,
    bool SetupCompleted,
    PlanDayDto? PlanDay,
    DdiaEdition DdiaEdition,
    IReadOnlyList<PlanTaskDto> WeekTasks,
    IReadOnlyList<PlanTaskDto> NextTasks,
    IReadOnlyList<ScheduleBlockTodayDto> ScheduleBlocks,
    GymTodayDto? Gym,
    DailyLogDto? Log,
    MetricsDto? Metrics,
    IReadOnlyList<NudgeDto> Nudges,
    IReadOnlyList<DsaProblemBrief> DsaProblems,
    IReadOnlyList<DsaReviewDto> ReviewsDue);

public sealed record DsaProblemBrief(int Id, string Title, string TopicName, ProblemStatus Status, int? LastConfidence);

public sealed record GetToday(DateOnly? Date);

public sealed class GetTodayHandler(
    IAppDbContext db,
    PlanContext plan,
    DailyLogService logs,
    IHandler<ListDsaReviews, IReadOnlyList<DsaReviewDto>> reviews,
    PlanTaskEnricher enricher,
    NudgeService nudges)
    : IHandler<GetToday, TodayView>
{
    public async Task<TodayView> HandleAsync(GetToday r, CancellationToken ct)
    {
        var snap = await plan.LoadAsync(ct);
        var date = r.Date ?? snap.Today;
        if (snap.Calendar is null || !snap.Settings.SetupCompleted)
        {
            return new TodayView(date, false, null, snap.Settings.DdiaEdition, [], [], [], null, null, null, [], [], []);
        }

        var cal = snap.Calendar;
        var dayDto = PlanDayDto.From(snap, date);
        var day = Math.Clamp(Math.Max(dayDto.Day, 1), 1, PlanCalendar.PlanLength);

        var weekTasks = await db.PlanTasks.AsNoTracking()
            .Where(t => t.DayFrom <= day && t.DayTo >= day)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Track)
            .Select(ListPlanTasksHandler.Projection).ToListAsync(ct);
        weekTasks = [.. await enricher.EnrichAsync(weekTasks, ct)];
        var weekIds = weekTasks.Select(t => t.Id).ToList();
        var candidates = await db.PlanTasks.AsNoTracking()
            .Where(t => t.DayFrom <= day && !weekIds.Contains(t.Id)
                && (t.Kind != PlanTaskKind.Manual
                    || ((t.StatusOverride ?? t.Status) != PlanTaskStatus.Done
                        && (t.StatusOverride ?? t.Status) != PlanTaskStatus.Deferred
                        && (t.StatusOverride ?? t.Status) != PlanTaskStatus.Skipped)))
            .OrderBy(t => t.DayFrom).ThenBy(t => t.SortOrder).Take(40)
            .Select(ListPlanTasksHandler.Projection).ToListAsync(ct);
        var nextTasks = (await enricher.EnrichAsync(candidates, ct)).Where(t => PlanRules.IsOpen(t.Status)).Take(5).ToList();

        var logId = await db.DailyLogs.Where(l => l.Date == date).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct);
        var log = logId is { } id ? await logs.GetDtoAsync(id, ct) : null;
        var done = log?.BlocksDone.ToHashSet() ?? [];

        var blocks = await db.ScheduleBlocks.AsNoTracking()
            .Where(b => b.SchedulePhase == dayDto.SchedulePhase)
            .OrderBy(b => b.SortOrder).ToListAsync(ct);
        var blockDtos = blocks.Select(b => new ScheduleBlockTodayDto(
            b.Id, b.TimeLabel ?? FormatTime(b.StartTime, b.EndTime), b.Title, b.Rule, b.IsStretch, done.Contains(b.Id))).ToList();

        var gym = await db.GymPlanDays.AsNoTracking()
            .Where(g => g.SchedulePhase == dayDto.SchedulePhase && g.DayOfWeek == date.DayOfWeek)
            .OrderBy(g => g.IsOptional)
            .Select(g => new GymTodayDto(g.SessionName, g.IsOptional, g.Notes)).FirstOrDefaultAsync(ct);

        var logMap = await LogMapAsync(db, date.AddDays(-60), date, ct);
        var lastPauseEnd = snap.Pauses.Where(p => p.EndDate < date).Select(p => (DateOnly?)p.EndDate).Max();
        var metrics = new MetricsDto(DayStates.ActiveInLast30(date, cal, logMap), DayStates.MissStatus(date, cal, logMap, lastPauseEnd));

        var dsaProblems = dayDto.Week is { } week
            ? await db.DsaProblems.AsNoTracking().Where(p => p.PlanWeek == week).OrderBy(p => p.SortOrder)
                .Select(p => new DsaProblemBrief(p.Id, p.Title, p.Topic!.Name, p.Status,
                    db.DsaAttempts.Where(a => a.ProblemId == p.Id).OrderByDescending(a => a.Date).ThenByDescending(a => a.Id).Select(a => (int?)a.Confidence).FirstOrDefault()))
                .ToListAsync(ct)
            : [];
        var due = (await reviews.HandleAsync(new ListDsaReviews(ReviewBucket.Overdue, null), ct))
            .Concat(await reviews.HandleAsync(new ListDsaReviews(ReviewBucket.Due, null), ct)).ToList();

        return new TodayView(date, true, dayDto, snap.Settings.DdiaEdition, weekTasks, nextTasks, blockDtos, gym, log, metrics,
            await nudges.ComputeAsync(date, ct), dsaProblems, due);
    }

    public static async Task<Dictionary<DateOnly, DayType?>> LogMapAsync(IAppDbContext db, DateOnly from, DateOnly to, CancellationToken ct) =>
        await db.DailyLogs.AsNoTracking().Where(l => l.Date >= from && l.Date <= to)
            .Select(l => new { l.Date, l.DayType }).ToDictionaryAsync(x => x.Date, x => x.DayType, ct);

    private static string FormatTime(TimeOnly? start, TimeOnly? end) =>
        start is null ? "" : end is null ? $"{start:HH\\:mm}" : $"{start:HH\\:mm}-{end:HH\\:mm}";
}

public sealed record HeatmapCell(DateOnly Date, DayState State, int PlanDay, bool IsTravel);

public sealed record HeatmapView(DateOnly From, DateOnly To, IReadOnlyList<HeatmapCell> Cells);

public sealed record GetHeatmap(DateOnly? From, DateOnly? To);

public sealed class GetHeatmapHandler(IAppDbContext db, PlanContext plan) : IHandler<GetHeatmap, HeatmapView>
{
    public async Task<HeatmapView> HandleAsync(GetHeatmap r, CancellationToken ct)
    {
        var snap = await plan.LoadAsync(ct);
        var cal = snap.RequireCalendar();
        var from = r.From ?? cal.Start;
        var to = r.To ?? cal.DateForDay(PlanCalendar.PlanLength);
        if (to < from || to.DayNumber - from.DayNumber > 800)
        {
            throw new BadRequestException("Choose a range of at most 800 days.");
        }

        var logs = await GetTodayHandler.LogMapAsync(db, from, to, ct);
        var cells = new List<HeatmapCell>(to.DayNumber - from.DayNumber + 1);
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var has = logs.TryGetValue(d, out var type);
            cells.Add(new HeatmapCell(d, DayStates.For(d, snap.Today, cal, has, type), cal.PlanDay(d), snap.Travel.IsTravelDay(d)));
        }

        return new HeatmapView(from, to, cells);
    }
}

public sealed record GetMetrics(DateOnly? Date);

public sealed class GetMetricsHandler(IAppDbContext db, PlanContext plan) : IHandler<GetMetrics, MetricsDto>
{
    public async Task<MetricsDto> HandleAsync(GetMetrics r, CancellationToken ct)
    {
        var snap = await plan.LoadAsync(ct);
        var cal = snap.RequireCalendar();
        var date = r.Date ?? snap.Today;
        var map = await GetTodayHandler.LogMapAsync(db, cal.Start < date.AddDays(-400) ? date.AddDays(-400) : cal.Start, date, ct);
        var lastPauseEnd = snap.Pauses.Where(p => p.EndDate < date).Select(p => (DateOnly?)p.EndDate).Max();
        return new MetricsDto(DayStates.ActiveInLast30(date, cal, map), DayStates.MissStatus(date, cal, map, lastPauseEnd));
    }
}
