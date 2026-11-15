using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Entities;
using SelfTracker.Domain.Services;

namespace SelfTracker.Application.Features;

public enum ReviewBucket { Due, Overdue, Upcoming, All }

public sealed record DsaReviewDto(
    int Id,
    int ProblemId,
    string ProblemTitle,
    string TopicName,
    DateOnly DueDate,
    int IntervalDays,
    ReviewStatus Status,
    int SourceAttemptId,
    int SourceConfidence,
    int DaysOverdue,
    string? SkipReason,
    string? RescheduleReason,
    string RowVersion);

public sealed record ListDsaReviews(ReviewBucket Bucket, int? ProblemId);

public sealed class ListDsaReviewsHandler(IAppDbContext db, PlanContext plan) : IHandler<ListDsaReviews, IReadOnlyList<DsaReviewDto>>
{
    public async Task<IReadOnlyList<DsaReviewDto>> HandleAsync(ListDsaReviews r, CancellationToken ct)
    {
        var today = (await plan.LoadAsync(ct)).Today;
        var q = db.DsaReviewItems.AsNoTracking();
        q = r.Bucket switch
        {
            ReviewBucket.Due => q.Where(x => x.Status == ReviewStatus.Pending && x.DueDate == today),
            ReviewBucket.Overdue => q.Where(x => x.Status == ReviewStatus.Pending && x.DueDate < today),
            ReviewBucket.Upcoming => q.Where(x => x.Status == ReviewStatus.Pending && x.DueDate > today),
            _ => q,
        };
        if (r.ProblemId is { } pid)
        {
            q = q.Where(x => x.ProblemId == pid);
        }

        var rows = await q.OrderBy(x => x.DueDate).ThenBy(x => x.Id).Take(300)
            .Select(x => new
            {
                x.Id,
                x.ProblemId,
                Title = x.Problem!.Title,
                Topic = x.Problem.Topic!.Name,
                x.DueDate,
                x.IntervalDays,
                x.Status,
                x.SourceAttemptId,
                SourceConfidence = x.SourceAttempt!.Confidence,
                x.SkipReason,
                x.RescheduleReason,
                x.RowVersion,
            })
            .ToListAsync(ct);
        return rows.Select(x => new DsaReviewDto(x.Id, x.ProblemId, x.Title, x.Topic, x.DueDate, x.IntervalDays, x.Status, x.SourceAttemptId,
            x.SourceConfidence, x.Status == ReviewStatus.Pending ? Math.Max(0, today.DayNumber - x.DueDate.DayNumber) : 0, x.SkipReason,
            x.RescheduleReason, Convert.ToBase64String(x.RowVersion))).ToList();
    }
}

public sealed record SkipReviewRequest([property: Required, StringLength(500, MinimumLength = 3)] string Reason);

public sealed record SkipReview(int Id, SkipReviewRequest Body);

public sealed class SkipReviewHandler(IAppDbContext db) : IHandler<SkipReview, bool>
{
    public async Task<bool> HandleAsync(SkipReview r, CancellationToken ct)
    {
        var item = await db.DsaReviewItems.FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new NotFoundException($"Review {r.Id}");
        if (item.Status != ReviewStatus.Pending)
        {
            throw new ConflictException("Only pending reviews can be skipped.");
        }

        item.Status = ReviewStatus.Skipped;
        item.SkipReason = r.Body.Reason.Trim();
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed record RescheduleReviewRequest([property: Required] DateOnly DueDate, [property: Required, StringLength(500, MinimumLength = 3)] string Reason);

public sealed record RescheduleReview(int Id, RescheduleReviewRequest Body);

public sealed class RescheduleReviewHandler(IAppDbContext db, PlanContext plan) : IHandler<RescheduleReview, bool>
{
    public async Task<bool> HandleAsync(RescheduleReview r, CancellationToken ct)
    {
        var item = await db.DsaReviewItems.FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new NotFoundException($"Review {r.Id}");
        if (item.Status != ReviewStatus.Pending)
        {
            throw new ConflictException("Only pending reviews can be rescheduled.");
        }

        if (r.Body.DueDate < (await plan.LoadAsync(ct)).Today)
        {
            throw RuleViolationException.For("dueDate", "Pick today or a later date.");
        }

        item.DueDate = r.Body.DueDate;
        item.RescheduleReason = r.Body.Reason.Trim();
        await db.SaveChangesAsync(ct);
        return true;
    }
}

// ---- Progress (BR-10) and weakest problems ----

public sealed record CumulativePoint(DateOnly Date, int PlanDay, int Cumulative);

public sealed record WeekCount(DateOnly WeekStart, int Count);

public sealed record WeekAverage(DateOnly WeekStart, double? Average, int Attempts);

public sealed record TopicCoverage(int TopicId, string Topic, bool IsDeferred, int Total, int Attempted, int Solved, int Deferred);

public sealed record TagWeekCount(string Tag, DateOnly WeekStart, int Count);

public sealed record DsaProgressDto(
    int CountedTotal,
    int AttemptsTotal,
    int PlanDay,
    MarkerBand NextMarker,
    IReadOnlyList<MarkerBand> Markers,
    string Caveat,
    IReadOnlyList<CumulativePoint> Cumulative,
    IReadOnlyList<WeekCount> NewProblemsByWeek,
    int WeeklyGuideMin,
    int WeeklyGuideMax,
    IReadOnlyList<TopicCoverage> Topics,
    double? MedianMinutes,
    IReadOnlyList<WeekAverage> ConfidenceByWeek,
    IReadOnlyList<TagWeekCount> RepeatedMistakes,
    int ColdReviewsDone,
    int ReviewsDueOrOverdue);

public sealed record GetDsaProgress;

public sealed class GetDsaProgressHandler(IAppDbContext db, PlanContext plan) : IHandler<GetDsaProgress, DsaProgressDto>
{
    public const int Weeks = 12;

    public static DateOnly MondayOf(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    public async Task<DsaProgressDto> HandleAsync(GetDsaProgress r, CancellationToken ct)
    {
        var snap = await plan.LoadAsync(ct);
        var cal = snap.RequireCalendar();
        var today = snap.Today;
        var attempts = await db.DsaAttempts.AsNoTracking()
            .Select(a => new { a.Id, a.Date, a.Type, a.IsCounted, a.TotalMin, a.Confidence, a.ProblemId })
            .ToListAsync(ct);

        var counted = attempts.Where(a => a.IsCounted).GroupBy(a => a.Date).OrderBy(g => g.Key).ToList();
        var running = 0;
        var cumulative = counted.Select(g => new CumulativePoint(g.Key, cal.PlanDay(g.Key), running += g.Count())).ToList();

        var thisMonday = MondayOf(today);
        var weekStarts = Enumerable.Range(0, Weeks).Select(i => thisMonday.AddDays(-7 * (Weeks - 1 - i))).ToList();

        var firstAttempt = attempts.Where(a => a.ProblemId != null && a.Type != AttemptType.ColdReview)
            .GroupBy(a => a.ProblemId!.Value).Select(g => g.Min(a => a.Date)).ToList();
        var newByWeek = weekStarts.Select(w => new WeekCount(w, firstAttempt.Count(d => d >= w && d < w.AddDays(7)))).ToList();

        var fresh = attempts.Where(a => a.Type != AttemptType.ColdReview).ToList();
        var confidence = weekStarts.Select(w =>
        {
            var inWeek = fresh.Where(a => a.Date >= w && a.Date < w.AddDays(7)).ToList();
            return new WeekAverage(w, inWeek.Count == 0 ? null : Math.Round(inWeek.Average(a => a.Confidence), 2), inWeek.Count);
        }).ToList();

        var topics = await db.DsaTopics.AsNoTracking().OrderBy(t => t.SortOrder)
            .Select(t => new TopicCoverage(t.Id, t.Name, t.IsDeferred,
                t.Problems.Count(p => !p.IsDeleted),
                t.Problems.Count(p => !p.IsDeleted && (p.Status == ProblemStatus.Attempted || p.Status == ProblemStatus.Solved)),
                t.Problems.Count(p => !p.IsDeleted && p.Status == ProblemStatus.Solved),
                t.Problems.Count(p => !p.IsDeleted && p.Status == ProblemStatus.Deferred)))
            .ToListAsync(ct);

        var since = weekStarts[^8];
        var tagRows = await db.DsaAttemptTags.AsNoTracking()
            .Where(x => x.DsaAttempt!.Date >= since && x.Tag!.Kind == TagKind.Mistake)
            .Select(x => new { x.Tag!.Name, x.DsaAttempt!.Date })
            .ToListAsync(ct);
        var repeatedNames = tagRows.GroupBy(t => t.Name).Where(g => g.Count() >= 2).Select(g => g.Key).ToHashSet();
        var repeated = tagRows.Where(t => repeatedNames.Contains(t.Name))
            .GroupBy(t => (t.Name, Week: MondayOf(t.Date)))
            .Select(g => new TagWeekCount(g.Key.Name, g.Key.Week, g.Count()))
            .OrderBy(t => t.WeekStart).ThenByDescending(t => t.Count).ToList();

        var due = await db.DsaReviewItems.CountAsync(x => x.Status == ReviewStatus.Pending && x.DueDate <= today, ct);
        var planDay = cal.PlanDay(today);
        return new DsaProgressDto(
            attempts.Count(a => a.IsCounted), attempts.Count, planDay, DsaRules.MarkerFor(Math.Max(planDay, 1)), DsaRules.Markers, DsaRules.MarkerCaveat,
            cumulative, newByWeek, DsaRules.WeeklyNewMin, DsaRules.WeeklyNewMax, topics,
            DsaRules.Median(attempts.Where(a => a.IsCounted && a.Type != AttemptType.ColdReview).Select(a => a.TotalMin)),
            confidence, repeated, attempts.Count(a => a.Type == AttemptType.ColdReview && a.IsCounted), due);
    }
}

public sealed record WeakProblemDto(int ProblemId, string Title, string TopicName, int LastConfidence, DateOnly LastAttemptOn, int Attempts, DateOnly? NextReviewDue);

public sealed record GetWeakest(int Take);

/// <summary>Problems whose latest attempt was rated 3 or lower, oldest review first (Week 13 cold redo list).</summary>
public sealed class GetWeakestHandler(IAppDbContext db) : IHandler<GetWeakest, IReadOnlyList<WeakProblemDto>>
{
    public async Task<IReadOnlyList<WeakProblemDto>> HandleAsync(GetWeakest r, CancellationToken ct)
    {
        var latest = await db.DsaAttempts.AsNoTracking().Where(a => a.ProblemId != null)
            .Select(a => new { ProblemId = a.ProblemId!.Value, a.Date, a.Id, a.Confidence })
            .ToListAsync(ct);
        var byProblem = latest.GroupBy(a => a.ProblemId)
            .Select(g => new { g.Key, Last = g.OrderByDescending(a => a.Date).ThenByDescending(a => a.Id).First(), Count = g.Count() })
            .Where(x => x.Last.Confidence <= 3)
            .ToList();
        var ids = byProblem.Select(x => x.Key).ToList();
        var problems = await db.DsaProblems.AsNoTracking().Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Title, Topic = p.Topic!.Name }).ToDictionaryAsync(p => p.Id, ct);
        var next = await db.DsaReviewItems.AsNoTracking().Where(x => ids.Contains(x.ProblemId) && x.Status == ReviewStatus.Pending)
            .GroupBy(x => x.ProblemId).Select(g => new { g.Key, Due = g.Min(x => x.DueDate) }).ToDictionaryAsync(x => x.Key, x => x.Due, ct);

        return byProblem.Where(x => problems.ContainsKey(x.Key))
            .Select(x => new WeakProblemDto(x.Key, problems[x.Key].Title, problems[x.Key].Topic, x.Last.Confidence, x.Last.Date, x.Count,
                next.TryGetValue(x.Key, out var d) ? d : null))
            .OrderBy(x => x.NextReviewDue ?? DateOnly.MaxValue).ThenBy(x => x.LastAttemptOn).ThenBy(x => x.LastConfidence)
            .Take(Math.Clamp(r.Take, 1, 100)).ToList();
    }
}
