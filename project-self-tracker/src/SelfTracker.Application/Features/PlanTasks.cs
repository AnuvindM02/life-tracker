using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;
using SelfTracker.Domain.Services;

namespace SelfTracker.Application.Features;

/// <summary>
/// Adds read-time facts to plan tasks: edition-resolved DDIA chapters (v1.1) and BR-18 derived status for
/// weekly DSA/DDIA tasks (a manual Deferred or Skipped always wins).
/// </summary>
public sealed class PlanTaskEnricher(IAppDbContext db, DdiaTaskResolver ddia)
{
    public async Task<IReadOnlyList<PlanTaskDto>> EnrichAsync(IReadOnlyList<PlanTaskDto> tasks, CancellationToken ct)
    {
        tasks = await ddia.ResolveAsync(tasks, ct);
        var dsaWeeks = tasks.Where(t => t.Kind == PlanTaskKind.DerivedDsa && t.PlanWeek != null).Select(t => t.PlanWeek!.Value).Distinct().ToList();
        var ddiaWeeks = tasks.Where(t => t.Kind == PlanTaskKind.DerivedDdia && t.PlanWeek != null).Select(t => t.PlanWeek!.Value).Distinct().ToList();
        if (dsaWeeks.Count == 0 && ddiaWeeks.Count == 0)
        {
            return tasks;
        }

        var problems = dsaWeeks.Count == 0
            ? []
            : await db.DsaProblems.AsNoTracking().Where(p => p.PlanWeek != null && dsaWeeks.Contains(p.PlanWeek.Value))
                .Select(p => new
                {
                    Week = p.PlanWeek!.Value,
                    p.Status,
                    HasAttempt = db.DsaAttempts.Any(a => a.ProblemId == p.Id),
                    HasCounted = db.DsaAttempts.Any(a => a.ProblemId == p.Id && a.IsCounted),
                })
                .ToListAsync(ct);

        var chapterStatuses = new List<(int From, int To, ChapterStatus Status)>();
        if (ddiaWeeks.Count > 0)
        {
            var edition = DdiaRules.DisplayEdition((await db.AppSettings.AsNoTracking().OrderBy(s => s.Id).Select(s => (DdiaEdition?)s.DdiaEdition).FirstOrDefaultAsync(ct)) ?? DdiaEdition.Unconfirmed);
            chapterStatuses = (await db.DdiaChapters.AsNoTracking().Where(c => c.Edition == edition)
                .Select(c => new { c.PlannedWeekFrom, c.PlannedWeekTo, c.Status }).ToListAsync(ct))
                .Select(c => (c.PlannedWeekFrom, c.PlannedWeekTo, c.Status)).ToList();
        }

        return tasks.Select(t =>
        {
            if (t.PlanWeek is not { } week || t.Status is PlanTaskStatus.Deferred or PlanTaskStatus.Skipped)
            {
                return t;
            }

            PlanTaskStatus? derived = t.Kind switch
            {
                PlanTaskKind.DerivedDsa => PlanRules.DerivedDsaStatus(problems.Where(p => p.Week == week)
                    .Select(p => new ProblemProgress(p.Status, p.HasAttempt, p.HasCounted)).ToList()),
                PlanTaskKind.DerivedDdia => PlanRules.DerivedDdiaStatus(chapterStatuses.Where(c => DdiaRules.CoversWeek(c.From, c.To, week)).Select(c => c.Status).ToList()),
                _ => null,
            };
            return derived is { } d ? t with { Status = d } : t;
        }).ToList();
    }

    /// <summary>All tasks with their effective status (for nudges and phase pages).</summary>
    public async Task<IReadOnlyList<PlanTaskDto>> AllAsync(CancellationToken ct) =>
        await EnrichAsync(await db.PlanTasks.AsNoTracking().OrderBy(t => t.DayFrom).ThenBy(t => t.SortOrder).Select(ListPlanTasksHandler.Projection).ToListAsync(ct), ct);
}

public sealed record PlanTaskInput(
    [property: Range(1, 4)] int PhaseNumber,
    PlanTrack Track,
    [property: Required, StringLength(300)] string Title,
    string? Description,
    [property: StringLength(1000)] string? OutputRequired,
    [property: StringLength(1000)] string? Fallback,
    [property: Range(1, 400)] int DayFrom,
    [property: Range(1, 400)] int DayTo,
    PlanTaskStatus Status,
    string? NotesMd,
    [property: StringLength(500)] string? EvidenceUrl,
    int SortOrder,
    string? RowVersion) : IVersioned;

public sealed record PlanTaskStatusInput(PlanTaskStatus Status, string? RowVersion);

public sealed class PlanTaskService(IAppDbContext db, PlanContext plan, PlanTaskEnricher enricher, IClock clock)
{
    public async Task<PlanTaskDto> GetAsync(int id, CancellationToken ct)
    {
        var dto = await db.PlanTasks.AsNoTracking().Where(t => t.Id == id).Select(ListPlanTasksHandler.Projection).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Plan task {id}");
        return (await enricher.EnrichAsync([dto], ct))[0];
    }

    public async Task<PlanTaskDto> SaveAsync(int? id, PlanTaskInput input, CancellationToken ct)
    {
        if (input.DayTo < input.DayFrom)
        {
            throw RuleViolationException.For("dayTo", "The last day must be on or after the first.");
        }

        var phaseId = await db.Phases.Where(p => p.Number == input.PhaseNumber).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct)
            ?? throw RuleViolationException.For("phaseNumber", "Unknown phase.");
        PlanTask task;
        if (id is { } existing)
        {
            task = await db.PlanTasks.FirstOrDefaultAsync(t => t.Id == existing, ct) ?? throw new NotFoundException($"Plan task {existing}");
            db.ExpectVersion(task, input.RowVersion);
        }
        else
        {
            task = db.PlanTasks.Add(new PlanTask { Kind = PlanTaskKind.Manual }).Entity;
        }

        static string? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        task.PhaseId = phaseId;
        task.Track = input.Track;
        task.Title = input.Title.Trim();
        task.Description = T(input.Description);
        task.OutputRequired = T(input.OutputRequired);
        task.Fallback = T(input.Fallback);
        task.DayFrom = input.DayFrom;
        task.DayTo = input.DayTo;
        task.NotesMd = T(input.NotesMd);
        task.EvidenceUrl = T(input.EvidenceUrl);
        task.SortOrder = input.SortOrder;
        await ApplyStatusAsync(task, input.Status, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(task.Id, ct);
    }

    public async Task<PlanTaskDto> SetStatusAsync(int id, PlanTaskStatusInput input, CancellationToken ct)
    {
        var task = await db.PlanTasks.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException($"Plan task {id}");
        db.ExpectVersion(task, input.RowVersion);
        await ApplyStatusAsync(task, input.Status, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>
    /// Manual tasks take any status. Derived tasks (BR-18) only take a manual Deferred or Skipped override;
    /// any other value clears the override so progress decides again.
    /// </summary>
    private async Task ApplyStatusAsync(PlanTask task, PlanTaskStatus status, CancellationToken ct)
    {
        if (task.Kind != PlanTaskKind.Manual)
        {
            task.StatusOverride = status is PlanTaskStatus.Deferred or PlanTaskStatus.Skipped ? status : null;
            return;
        }

        if (status == PlanTaskStatus.Done && task.Status != PlanTaskStatus.Done)
        {
            task.CompletedOn = (await plan.LoadAsync(ct)).Today;
        }
        else if (status != PlanTaskStatus.Done)
        {
            task.CompletedOn = null;
        }

        task.Status = status;
        task.StatusOverride = null;
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var task = await db.PlanTasks.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException($"Plan task {id}");
        task.SoftDelete(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

// ---- Exit criteria ----

public sealed record ExitCriterionDto(int Id, int PhaseNumber, string? Group, string Text, string? EvidenceHint, bool Done, DateOnly? DoneOn, string? EvidenceText, int SortOrder, int AttachmentCount, string RowVersion);

public sealed record ExitCriterionInput(
    [property: Range(1, 4)] int PhaseNumber,
    [property: StringLength(80)] string? Group,
    [property: Required, StringLength(1000)] string Text,
    [property: StringLength(1000)] string? EvidenceHint,
    bool Done,
    [property: StringLength(2000)] string? EvidenceText,
    int SortOrder,
    string? RowVersion) : IVersioned;

public sealed class ExitCriterionService(IAppDbContext db, PlanContext plan, IClock clock)
{
    public async Task<IReadOnlyList<ExitCriterionDto>> ListAsync(int? phase, CancellationToken ct)
    {
        var q = db.ExitCriteria.AsNoTracking();
        if (phase is { } p) q = q.Where(c => c.Phase!.Number == p);
        return await q.OrderBy(c => c.Phase!.Number).ThenBy(c => c.SortOrder)
            .Select(c => new ExitCriterionDto(c.Id, c.Phase!.Number, c.Group, c.Text, c.EvidenceHint, c.Done, c.DoneOn, c.EvidenceText, c.SortOrder,
                db.FileLinks.Count(l => l.EntityType == "ExitCriterion" && l.EntityId == c.Id), Convert.ToBase64String(c.RowVersion)))
            .ToListAsync(ct);
    }

    public async Task<ExitCriterionDto> SaveAsync(int? id, ExitCriterionInput input, CancellationToken ct)
    {
        var phaseId = await db.Phases.Where(p => p.Number == input.PhaseNumber).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct)
            ?? throw RuleViolationException.For("phaseNumber", "Unknown phase.");
        ExitCriterion c;
        if (id is { } existing)
        {
            c = await db.ExitCriteria.FirstOrDefaultAsync(x => x.Id == existing, ct) ?? throw new NotFoundException($"Exit criterion {existing}");
            db.ExpectVersion(c, input.RowVersion);
        }
        else
        {
            c = db.ExitCriteria.Add(new ExitCriterion()).Entity;
        }

        c.PhaseId = phaseId;
        c.Group = string.IsNullOrWhiteSpace(input.Group) ? null : input.Group.Trim();
        c.Text = input.Text.Trim();
        c.EvidenceHint = input.EvidenceHint;
        c.EvidenceText = string.IsNullOrWhiteSpace(input.EvidenceText) ? null : input.EvidenceText.Trim();
        c.SortOrder = input.SortOrder;
        if (input.Done && !c.Done)
        {
            c.DoneOn = (await plan.LoadAsync(ct)).Today;
        }
        else if (!input.Done)
        {
            c.DoneOn = null;
        }

        c.Done = input.Done;
        await db.SaveChangesAsync(ct);
        return (await ListAsync(input.PhaseNumber, ct)).First(x => x.Id == c.Id);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var c = await db.ExitCriteria.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException($"Exit criterion {id}");
        c.SoftDelete(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}

// ---- Checkpoints (BR-11) ----

public sealed record CheckpointDto(
    int Id,
    int PlanDay,
    int? DayTo,
    string Title,
    string Question,
    string? AnswerMd,
    CheckpointOutcome? Outcome,
    DateOnly? AnsweredOn,
    DateOnly? DueDate,
    DateOnly? WindowEnd,
    CheckpointState State,
    IReadOnlyList<string> Context,
    int AttachmentCount,
    string RowVersion);

public sealed record CheckpointAnswerInput(string? AnswerMd, CheckpointOutcome? Outcome, string? RowVersion);

public sealed class CheckpointService(IAppDbContext db, PlanContext plan, PlanTaskEnricher tasks)
{
    public async Task<IReadOnlyList<CheckpointDto>> ListAsync(bool withContext, CancellationToken ct)
    {
        var snap = await plan.LoadAsync(ct);
        var rows = await db.Checkpoints.AsNoTracking().OrderBy(c => c.PlanDay)
            .Select(c => new { c, Files = db.FileLinks.Count(l => l.EntityType == "Checkpoint" && l.EntityId == c.Id) }).ToListAsync(ct);
        var result = new List<CheckpointDto>();
        foreach (var r in rows)
        {
            var c = r.c;
            DateOnly? due = snap.Calendar?.DateForDay(c.PlanDay);
            DateOnly? end = c.DayTo is { } to ? snap.Calendar?.DateForDay(to) : null;
            var answered = !string.IsNullOrWhiteSpace(c.AnswerMd);
            var state = due is { } d ? PlanRules.CheckpointStateFor(d, snap.Today, answered) : CheckpointState.Upcoming;
            var context = withContext ? await ContextAsync(c, snap, due, ct) : [];
            result.Add(new CheckpointDto(c.Id, c.PlanDay, c.DayTo, c.Title, c.Question, c.AnswerMd, c.Outcome, c.AnsweredOn, due, end, state, context, r.Files, Convert.ToBase64String(c.RowVersion)));
        }

        return result;
    }

    public async Task<CheckpointDto> AnswerAsync(int id, CheckpointAnswerInput input, CancellationToken ct)
    {
        var c = await db.Checkpoints.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException($"Checkpoint {id}");
        db.ExpectVersion(c, input.RowVersion);
        var answer = string.IsNullOrWhiteSpace(input.AnswerMd) ? null : input.AnswerMd.Trim();
        if (answer is not null && c.AnswerMd is null)
        {
            c.AnsweredOn = (await plan.LoadAsync(ct)).Today;
        }
        else if (answer is null)
        {
            c.AnsweredOn = null;
        }

        c.AnswerMd = answer;
        c.Outcome = input.Outcome;
        await db.SaveChangesAsync(ct);
        return (await ListAsync(true, ct)).First(x => x.Id == id);
    }

    /// <summary>Spec 4.6: Day 30 active days, Day 60 edition, Day 90 incomplete Phase 1 artifacts; market and funnel arrive with M7.</summary>
    private async Task<IReadOnlyList<string>> ContextAsync(Checkpoint c, PlanSnapshot snap, DateOnly? due, CancellationToken ct)
    {
        var lines = new List<string>();
        switch (c.PlanDay)
        {
            case 30 when snap.Calendar is { } cal:
            {
                var at = due is { } d && d < snap.Today ? d : snap.Today;
                var logs = await GetTodayHandler.LogMapAsync(db, at.AddDays(-29), at, ct);
                var w = DayStates.ActiveInLast30(at, cal, logs);
                lines.Add($"Active days in the 30 days to {at:yyyy-MM-dd}: {w.ActiveDays} (aim 26, MVDs included: {w.MvdDays}{(w.PausedDays > 0 ? $", {w.PausedDays} paused days excluded" : "")}).");
                break;
            }

            case 60:
                lines.Add(snap.Settings.DdiaEdition == DdiaEdition.Unconfirmed
                    ? "DDIA edition: not confirmed yet. Check the title/copyright page."
                    : $"DDIA edition confirmed: {snap.Settings.DdiaEdition} (on {snap.Settings.DdiaEditionConfirmedOn:yyyy-MM-dd}).");
                break;
            case 90:
            {
                var open = (await tasks.AllAsync(ct)).Where(t => t.PhaseNumber == 1 && PlanRules.IsOpen(t.Status)).ToList();
                var exits = await db.ExitCriteria.AsNoTracking().Where(e => e.Phase!.Number == 1 && !e.Done).Select(e => e.Text).ToListAsync(ct);
                lines.Add($"Phase 1 exit criteria not done: {exits.Count}.");
                lines.AddRange(exits.Select(e => $"Not done: {e}"));
                lines.Add($"Open Phase 1 plan tasks: {open.Count}.");
                break;
            }

            case 180:
                lines.Add("Review 20 current postings: the posting log and skill analytics arrive with the Market module (M7).");
                break;
            case 340:
                lines.Add("Funnel metrics (application-to-screen, screen-to-next-stage) arrive with the Interviews module (M7).");
                break;
        }

        return lines;
    }
}

// ---- Re-plan log ----

public sealed record ReplanDto(int Id, DateOnly Date, ReplanTrigger Trigger, string? EvidenceMd, string? DecisionMd, int? CheckpointId, string RowVersion);

public sealed record ReplanInput([property: Required] DateOnly Date, ReplanTrigger Trigger, string? EvidenceMd, string? DecisionMd, int? CheckpointId, string? RowVersion) : IVersioned;

public sealed class ReplanSpec(IAppDbContext db) : ICrudSpec<ReplanEvent, ReplanDto, ReplanInput>
{
    public string Name => "Re-plan";

    public string DefaultSort => "-date";

    public System.Linq.Expressions.Expression<Func<ReplanEvent, ReplanDto>> Projection =>
        r => new ReplanDto(r.Id, r.Date, r.Trigger, r.EvidenceMd, r.DecisionMd, r.CheckpointId, Convert.ToBase64String(r.RowVersion));

    public async Task ValidateAsync(ReplanInput input, ReplanEvent? existing, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.EvidenceMd))
        {
            throw RuleViolationException.For("evidenceMd", "Write the evidence: re-plan only when checkpoint evidence or a listed trigger says so.");
        }

        if (input.CheckpointId is { } cid && !await db.Checkpoints.AnyAsync(c => c.Id == cid, ct))
        {
            throw RuleViolationException.For("checkpointId", "Unknown checkpoint.");
        }
    }

    public void Apply(ReplanInput input, ReplanEvent entity)
    {
        entity.Date = input.Date;
        entity.Trigger = input.Trigger;
        entity.EvidenceMd = input.EvidenceMd?.Trim();
        entity.DecisionMd = string.IsNullOrWhiteSpace(input.DecisionMd) ? null : input.DecisionMd.Trim();
        entity.CheckpointId = input.CheckpointId;
    }
}
