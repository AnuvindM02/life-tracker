using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;
using SelfTracker.Domain.Services;

namespace SelfTracker.Application.Features;

public sealed record ReviewDateDto(int Id, DateOnly DueDate, int IntervalDays, ReviewStatus Status);

public sealed record DsaAttemptDto(
    int Id,
    DateOnly Date,
    int? ProblemId,
    string Title,
    string? TopicName,
    string? TitleFreeText,
    AttemptType Type,
    int Pass1Min,
    HintLevel HintLevel,
    int Pass2Min,
    int Pass3Min,
    int TotalMin,
    bool SolvedUnaided,
    string? Pattern,
    string? KeyInsight,
    string? WhatIMissed,
    string? TimeComplexity,
    string? SpaceComplexity,
    int Confidence,
    string? CodeMd,
    string? Company,
    bool IsCounted,
    string? NotCountedReason,
    int? ReviewItemId,
    IReadOnlyList<string> MistakeTags,
    IReadOnlyList<ReviewDateDto> Reviews,
    string RowVersion);

public sealed record DsaAttemptSummaryDto(
    int Id,
    DateOnly Date,
    int? ProblemId,
    string Title,
    string? TopicName,
    AttemptType Type,
    int TotalMin,
    int Confidence,
    bool SolvedUnaided,
    bool IsCounted,
    string? Pattern,
    string? WhatIMissed,
    string? Company);

public sealed record DsaAttemptInput(
    [property: Required] DateOnly Date,
    int? ProblemId,
    [property: StringLength(200)] string? TitleFreeText,
    AttemptType Type,
    [property: Range(0, 600)] int Pass1Min,
    HintLevel HintLevel,
    [property: Range(0, 600)] int Pass2Min,
    [property: Range(0, 600)] int Pass3Min,
    [property: Range(0, 1440)] int? TotalMin,
    bool SolvedUnaided,
    [property: StringLength(200)] string? Pattern,
    [property: StringLength(2000)] string? KeyInsight,
    [property: StringLength(2000)] string? WhatIMissed,
    [property: StringLength(60)] string? TimeComplexity,
    [property: StringLength(60)] string? SpaceComplexity,
    [property: Range(1, 5)] int Confidence,
    string? CodeMd,
    [property: StringLength(120)] string? Company,
    string[]? MistakeTags,
    int? ReviewItemId,
    string? RowVersion) : IVersioned;

/// <summary>
/// Attempt logging: BR-08 counting, BR-09 ladder (spec v1.1: one active ladder per problem), problem status,
/// mistake tags and the derived DSA time entry (BR-19), written together in one transaction.
/// </summary>
public sealed class DsaAttemptService(IAppDbContext db, PlanContext plan, DailyLogService logs, IClock clock)
{
    public const string SourceType = "DsaAttempt";

    public async Task<DsaAttemptDto> GetAsync(int id, CancellationToken ct)
    {
        var a = await db.DsaAttempts.AsNoTracking()
            .Include(x => x.Problem!).ThenInclude(p => p.Topic)
            .Include(x => x.Tags).ThenInclude(t => t.Tag)
            .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException($"DSA attempt {id}");
        var reviews = await db.DsaReviewItems.AsNoTracking().Where(r => r.SourceAttemptId == id).OrderBy(r => r.DueDate)
            .Select(r => new ReviewDateDto(r.Id, r.DueDate, r.IntervalDays, r.Status)).ToListAsync(ct);
        return new DsaAttemptDto(
            a.Id, a.Date, a.ProblemId, a.Problem?.Title ?? a.TitleFreeText ?? "(untitled)", a.Problem?.Topic?.Name, a.TitleFreeText, a.Type,
            a.Pass1Min, a.HintLevel, a.Pass2Min, a.Pass3Min, a.TotalMin, a.SolvedUnaided, a.Pattern, a.KeyInsight, a.WhatIMissed,
            a.TimeComplexity, a.SpaceComplexity, a.Confidence, a.CodeMd, a.Company, a.IsCounted, a.NotCountedReason, a.ReviewItemId,
            a.Tags.Where(t => t.Tag != null).Select(t => t.Tag!.Name).Order().ToList(), reviews, a.Version());
    }

    public async Task<DsaAttemptDto> CreateAsync(DsaAttemptInput input, CancellationToken ct)
    {
        var snap = await plan.LoadAsync(ct);
        var problem = await ValidateAsync(input, snap, null, ct);
        var attempt = new DsaAttempt();
        Apply(input, attempt, snap.Settings, problem);
        await SetTagsAsync(attempt, input.MistakeTags, ct);

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.DsaAttempts.Add(attempt);
            await db.SaveChangesAsync(ct);
            if (problem is not null)
            {
                await ApplyLadderAsync(attempt, input.ReviewItemId, snap.Settings, ct);
                problem.Status = DsaRules.StatusAfterAttempt(problem.Status, attempt.SolvedUnaided);
            }

            await SyncTimeEntryAsync(attempt, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return await GetAsync(attempt.Id, ct);
    }

    public async Task<DsaAttemptDto> UpdateAsync(int id, DsaAttemptInput input, CancellationToken ct)
    {
        var snap = await plan.LoadAsync(ct);
        var attempt = await db.DsaAttempts.Include(a => a.Tags).FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException($"DSA attempt {id}");
        db.ExpectVersion(attempt, input.RowVersion);
        if (input.ProblemId != attempt.ProblemId || input.Type != attempt.Type)
        {
            throw RuleViolationException.For("problemId", "The problem and attempt type cannot change after logging (they own review dates). Delete and log again instead.");
        }

        var problem = await ValidateAsync(input, snap, attempt, ct);
        var regenerate = input.Confidence != attempt.Confidence || input.Date != attempt.Date;
        Apply(input, attempt, snap.Settings, problem);
        await SetTagsAsync(attempt, input.MistakeTags, ct);

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (regenerate && problem is not null)
            {
                // BR-09: editing confidence regenerates this attempt's pending items (if its ladder is still active).
                var pending = await db.DsaReviewItems.Where(r => r.SourceAttemptId == attempt.Id && r.Status == ReviewStatus.Pending).ToListAsync(ct);
                if (pending.Count > 0)
                {
                    db.DsaReviewItems.RemoveRange(pending);
                    AddLadder(attempt, problem.Id, attempt.Date, snap.Settings);
                }
            }

            if (problem is not null)
            {
                problem.Status = DsaRules.StatusAfterAttempt(problem.Status, attempt.SolvedUnaided);
            }

            await SyncTimeEntryAsync(attempt, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return await GetAsync(attempt.Id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var attempt = await db.DsaAttempts.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException($"DSA attempt {id}");
        var now = clock.UtcNow;
        attempt.SoftDelete(now);
        foreach (var r in await db.DsaReviewItems.Where(r => r.SourceAttemptId == id && r.Status == ReviewStatus.Pending).ToListAsync(ct))
        {
            r.Status = ReviewStatus.Superseded;
        }

        foreach (var r in await db.DsaReviewItems.Where(r => r.CompletedAttemptId == id).ToListAsync(ct))
        {
            r.Status = ReviewStatus.Pending;
            r.CompletedAttemptId = null;
        }

        foreach (var te in await db.TimeEntries.Where(t => t.SourceEntityType == SourceType && t.SourceEntityId == id).ToListAsync(ct))
        {
            te.SoftDelete(now);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<DsaProblem?> ValidateAsync(DsaAttemptInput input, PlanSnapshot snap, DsaAttempt? existing, CancellationToken ct)
    {
        if (input.Date > snap.Today)
        {
            throw RuleViolationException.For("date", "Attempts cannot be logged for a future date.");
        }

        DsaProblem? problem = null;
        if (input.ProblemId is { } pid)
        {
            problem = await db.DsaProblems.FirstOrDefaultAsync(p => p.Id == pid, ct)
                ?? throw RuleViolationException.For("problemId", "That problem is not in the library.");
        }
        else if (string.IsNullOrWhiteSpace(input.TitleFreeText))
        {
            throw RuleViolationException.For("titleFreeText", "Choose a problem from the library or type a title.");
        }

        if (input.ReviewItemId is { } rid && existing is null)
        {
            var ok = problem is not null && input.Type == AttemptType.ColdReview
                && await db.DsaReviewItems.AnyAsync(r => r.Id == rid && r.ProblemId == problem.Id && r.Status == ReviewStatus.Pending, ct);
            if (!ok)
            {
                throw RuleViolationException.For("reviewItemId", "That review is not pending for this problem.");
            }
        }

        return problem;
    }

    private static void Apply(DsaAttemptInput input, DsaAttempt a, AppSettings settings, DsaProblem? problem)
    {
        static string? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        a.Date = input.Date;
        a.ProblemId = problem?.Id;
        a.TitleFreeText = problem is null ? T(input.TitleFreeText) : null;
        a.Type = input.Type;
        a.Pass1Min = input.Pass1Min;
        a.HintLevel = input.HintLevel;
        a.Pass2Min = input.Pass2Min;
        a.Pass3Min = input.Pass3Min;
        a.TotalMin = input.TotalMin is > 0 ? input.TotalMin.Value : input.Pass1Min + input.Pass2Min + input.Pass3Min;
        a.SolvedUnaided = input.SolvedUnaided;
        a.Pattern = T(input.Pattern);
        a.KeyInsight = T(input.KeyInsight);
        a.WhatIMissed = T(input.WhatIMissed);
        a.TimeComplexity = T(input.TimeComplexity);
        a.SpaceComplexity = T(input.SpaceComplexity);
        a.Confidence = input.Confidence;
        a.CodeMd = T(input.CodeMd);
        a.Company = T(input.Company);
        var count = DsaRules.Evaluate(a.Type, a.Pass1Min, settings.DsaUnaidedMinMinutes, a.WhatIMissed, problem is not null);
        a.IsCounted = count.IsCounted;
        a.NotCountedReason = count.Reason;
    }

    private async Task SetTagsAsync(DsaAttempt attempt, string[]? names, CancellationToken ct)
    {
        var wanted = (names ?? []).Select(n => n.Trim().ToLowerInvariant()).Where(n => n.Length is > 0 and <= 60).Distinct().ToList();
        var tags = await db.Tags.Where(t => t.Kind == TagKind.Mistake && wanted.Contains(t.Name)).ToListAsync(ct);
        foreach (var name in wanted.Where(n => tags.All(t => t.Name != n)))
        {
            var tag = new Tag { Name = name, Kind = TagKind.Mistake };
            db.Tags.Add(tag);
            tags.Add(tag);
        }

        attempt.Tags.RemoveAll(at => !tags.Any(t => t.Id != 0 && t.Id == at.TagId));
        foreach (var tag in tags.Where(t => t.Id == 0 || attempt.Tags.All(at => at.TagId != t.Id)))
        {
            attempt.Tags.Add(new DsaAttemptTag { Tag = tag });
        }
    }

    /// <summary>BR-09 (v1.1): one active ladder per problem.</summary>
    private async Task ApplyLadderAsync(DsaAttempt attempt, int? reviewItemId, AppSettings settings, CancellationToken ct)
    {
        var problemId = attempt.ProblemId!.Value;
        var pending = await db.DsaReviewItems.Where(r => r.ProblemId == problemId && r.Status == ReviewStatus.Pending)
            .OrderBy(r => r.DueDate).ThenBy(r => r.Id).ToListAsync(ct);

        if (attempt.Type == AttemptType.ColdReview)
        {
            var item = reviewItemId is { } rid ? pending.FirstOrDefault(r => r.Id == rid) : pending.FirstOrDefault();
            if (item is not null)
            {
                item.Status = ReviewStatus.Done;
                item.CompletedAttemptId = attempt.Id;
                attempt.ReviewItemId = item.Id;
                if (DsaRules.OnReview(attempt.Confidence) == ReviewOutcome.KeepRemaining)
                {
                    return;
                }

                pending.Where(r => r != item).ToList().ForEach(r => r.Status = ReviewStatus.Superseded);
                AddLadder(attempt, problemId, attempt.Date, settings);
                return;
            }
        }

        // New or timed attempt (or an unlinked cold review with nothing pending): this attempt starts the ladder.
        pending.ForEach(r => r.Status = ReviewStatus.Superseded);
        AddLadder(attempt, problemId, attempt.Date, settings);
    }

    private void AddLadder(DsaAttempt attempt, int problemId, DateOnly from, AppSettings settings)
    {
        foreach (var step in DsaRules.PlanLadder(from, attempt.Confidence, DsaRules.ParseIntervals(settings.ReviewIntervalsCsv), settings.HighConfidenceReviewDays))
        {
            db.DsaReviewItems.Add(new DsaReviewItem
            {
                ProblemId = problemId,
                SourceAttemptId = attempt.Id,
                DueDate = step.DueDate,
                IntervalDays = step.IntervalDays,
                Status = ReviewStatus.Pending,
            });
        }
    }

    /// <summary>BR-19: the attempt owns one derived DSA time entry on its date.</summary>
    private async Task SyncTimeEntryAsync(DsaAttempt attempt, CancellationToken ct)
    {
        var entry = await db.TimeEntries.FirstOrDefaultAsync(t => t.SourceEntityType == SourceType && t.SourceEntityId == attempt.Id, ct);
        if (attempt.TotalMin <= 0)
        {
            if (entry is not null)
            {
                db.TimeEntries.Remove(entry);
            }

            return;
        }

        var log = await logs.EnsureAsync(attempt.Date, ct);
        entry ??= db.TimeEntries.Add(new TimeEntry { Source = TimeSource.Derived, SourceEntityType = SourceType, SourceEntityId = attempt.Id }).Entity;
        entry.Date = attempt.Date;
        entry.DailyLogId = log.Id;
        entry.Category = TimeCategory.Dsa;
        entry.Minutes = attempt.TotalMin;
        entry.Note = $"{(attempt.Type == AttemptType.ColdReview ? "Cold review" : attempt.Type == AttemptType.Timed ? "Timed" : "Attempt")}: {attempt.TitleFreeText ?? (await db.DsaProblems.Where(p => p.Id == attempt.ProblemId).Select(p => p.Title).FirstOrDefaultAsync(ct))}";
    }
}

public sealed record CreateDsaAttempt(DsaAttemptInput Body);

public sealed class CreateDsaAttemptHandler(DsaAttemptService svc) : IHandler<CreateDsaAttempt, DsaAttemptDto>
{
    public Task<DsaAttemptDto> HandleAsync(CreateDsaAttempt r, CancellationToken ct) => svc.CreateAsync(r.Body, ct);
}

public sealed record UpdateDsaAttempt(int Id, DsaAttemptInput Body);

public sealed class UpdateDsaAttemptHandler(DsaAttemptService svc) : IHandler<UpdateDsaAttempt, DsaAttemptDto>
{
    public Task<DsaAttemptDto> HandleAsync(UpdateDsaAttempt r, CancellationToken ct) => svc.UpdateAsync(r.Id, r.Body, ct);
}

public sealed record GetDsaAttempt(int Id);

public sealed class GetDsaAttemptHandler(DsaAttemptService svc) : IHandler<GetDsaAttempt, DsaAttemptDto>
{
    public Task<DsaAttemptDto> HandleAsync(GetDsaAttempt r, CancellationToken ct) => svc.GetAsync(r.Id, ct);
}

public sealed record DeleteDsaAttempt(int Id);

public sealed class DeleteDsaAttemptHandler(DsaAttemptService svc) : IHandler<DeleteDsaAttempt, bool>
{
    public async Task<bool> HandleAsync(DeleteDsaAttempt r, CancellationToken ct)
    {
        await svc.DeleteAsync(r.Id, ct);
        return true;
    }
}

public sealed record ListDsaAttempts(
    DateOnly? From,
    DateOnly? To,
    int? ProblemId,
    int? Confidence,
    AttemptType? Type,
    string? Tag,
    string? Company,
    bool? Counted,
    ListQuery List);

public sealed class ListDsaAttemptsHandler(IAppDbContext db) : IHandler<ListDsaAttempts, PagedResult<DsaAttemptSummaryDto>>
{
    public Task<PagedResult<DsaAttemptSummaryDto>> HandleAsync(ListDsaAttempts r, CancellationToken ct)
    {
        var q = db.DsaAttempts.AsNoTracking();
        if (r.From is { } from) q = q.Where(a => a.Date >= from);
        if (r.To is { } to) q = q.Where(a => a.Date <= to);
        if (r.ProblemId is { } pid) q = q.Where(a => a.ProblemId == pid);
        if (r.Confidence is { } c) q = q.Where(a => a.Confidence == c);
        if (r.Type is { } type) q = q.Where(a => a.Type == type);
        if (r.Counted is { } counted) q = q.Where(a => a.IsCounted == counted);
        if (!string.IsNullOrWhiteSpace(r.Company)) q = q.Where(a => a.Company != null && a.Company.Contains(r.Company));
        if (!string.IsNullOrWhiteSpace(r.Tag))
        {
            var tag = r.Tag.Trim().ToLowerInvariant();
            q = q.Where(a => a.Tags.Any(t => t.Tag!.Name == tag));
        }

        return q.ApplySort(r.List.Sort, "-date")
            .Select(a => new DsaAttemptSummaryDto(a.Id, a.Date, a.ProblemId, a.Problem != null ? a.Problem.Title : a.TitleFreeText ?? "(untitled)",
                a.Problem != null ? a.Problem.Topic!.Name : null, a.Type, a.TotalMin, a.Confidence, a.SolvedUnaided, a.IsCounted, a.Pattern,
                a.WhatIMissed, a.Company))
            .ToPagedAsync(r.List, ct);
    }
}

public sealed record TagDto(string Name, int Uses);

public sealed record ListTags(TagKind Kind);

public sealed class ListTagsHandler(IAppDbContext db) : IHandler<ListTags, IReadOnlyList<TagDto>>
{
    public async Task<IReadOnlyList<TagDto>> HandleAsync(ListTags r, CancellationToken ct)
    {
        var tags = await db.Tags.AsNoTracking().Where(t => t.Kind == r.Kind).Select(t => new { t.Id, t.Name }).ToListAsync(ct);
        var uses = await db.DsaAttemptTags.AsNoTracking().GroupBy(x => x.TagId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return tags.Select(t => new TagDto(t.Name, uses.GetValueOrDefault(t.Id)))
            .OrderByDescending(t => t.Uses).ThenBy(t => t.Name, StringComparer.Ordinal).Take(200).ToList();
    }
}
