using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;
using SelfTracker.Domain.Services;

namespace SelfTracker.Application.Features;

public sealed record PhaseDto(int Id, int Number, string Name, int DayFrom, int DayTo, int WeekFrom, int WeekTo, string Objective, string ExitEvidence);

public sealed record PlanDayDto(
    DateOnly Date,
    PlanDayStatus Status,
    int Day,
    bool IsPaused,
    int? PhaseNumber,
    string? PhaseName,
    int? Week,
    SchedulePhase SchedulePhase,
    TravelRole TravelRole,
    DateOnly? TripFriday,
    PauseReason? PauseReason)
{
    public static PlanDayDto From(PlanSnapshot snap, DateOnly date)
    {
        var cal = snap.RequireCalendar();
        var info = cal.Describe(date);
        var pause = snap.Pauses.FirstOrDefault(p => date >= p.StartDate && date <= p.EndDate);
        return new(date, info.Status, info.Day, info.IsPaused, info.Phase?.Number, info.Phase?.Name, info.Week, info.SchedulePhase,
            snap.Travel.RoleFor(date), snap.Travel.TripFridayFor(date), info.IsPaused ? pause?.Reason : null);
    }
}

public sealed record GetPhases;

public sealed class GetPhasesHandler(IAppDbContext db) : IHandler<GetPhases, IReadOnlyList<PhaseDto>>
{
    public async Task<IReadOnlyList<PhaseDto>> HandleAsync(GetPhases request, CancellationToken ct) =>
        await db.Phases.AsNoTracking().OrderBy(p => p.Number)
            .Select(p => new PhaseDto(p.Id, p.Number, p.Name, p.DayFrom, p.DayTo, p.WeekFrom, p.WeekTo, p.Objective, p.ExitEvidence))
            .ToListAsync(ct);
}

public sealed record GetPlanDay(DateOnly? Date);

public sealed class GetPlanDayHandler(PlanContext plan) : IHandler<GetPlanDay, PlanDayDto>
{
    public async Task<PlanDayDto> HandleAsync(GetPlanDay request, CancellationToken ct)
    {
        var snap = await plan.LoadAsync(ct);
        return PlanDayDto.From(snap, request.Date ?? snap.Today);
    }
}

// ---- Pause ranges (BR-01) ----

public sealed record PauseRangeDto(int Id, DateOnly StartDate, DateOnly EndDate, PauseReason Reason, string? Notes, string RowVersion);

public sealed record PauseRangeInput(
    [property: Required] DateOnly StartDate,
    [property: Required] DateOnly EndDate,
    PauseReason Reason,
    [property: StringLength(1000)] string? Notes,
    string? RowVersion) : IVersioned;

public sealed class PauseRangeSpec(IAppDbContext db) : ICrudSpec<PauseRange, PauseRangeDto, PauseRangeInput>
{
    public string Name => "Pause range";

    public string DefaultSort => "startDate";

    public Expression<Func<PauseRange, PauseRangeDto>> Projection =>
        p => new PauseRangeDto(p.Id, p.StartDate, p.EndDate, p.Reason, p.Notes, Convert.ToBase64String(p.RowVersion));

    public async Task ValidateAsync(PauseRangeInput input, PauseRange? existing, CancellationToken ct)
    {
        if (input.EndDate < input.StartDate)
        {
            throw RuleViolationException.For("endDate", "End date must be on or after the start date.");
        }

        var overlaps = await db.PauseRanges.AnyAsync(
            p => p.Id != (existing == null ? 0 : existing.Id) && p.StartDate <= input.EndDate && p.EndDate >= input.StartDate, ct);
        if (overlaps)
        {
            throw RuleViolationException.For("startDate", "This range overlaps another pause range; edit that one instead.");
        }
    }

    public void Apply(PauseRangeInput input, PauseRange entity)
    {
        entity.StartDate = input.StartDate;
        entity.EndDate = input.EndDate;
        entity.Reason = input.Reason;
        entity.Notes = input.Notes;
    }
}

// ---- Travel overrides (BR-04) ----

public sealed record TravelOverrideDto(int Id, DateOnly WeekFriday, bool IsTravel, string? Notes, string RowVersion);

public sealed record TravelOverrideInput([property: Required] DateOnly WeekFriday, bool IsTravel, [property: StringLength(500)] string? Notes, string? RowVersion) : IVersioned;

public sealed class TravelOverrideSpec(IAppDbContext db) : ICrudSpec<TravelOverride, TravelOverrideDto, TravelOverrideInput>
{
    public string Name => "Travel override";

    public string DefaultSort => "weekFriday";

    public Expression<Func<TravelOverride, TravelOverrideDto>> Projection =>
        t => new TravelOverrideDto(t.Id, t.WeekFriday, t.IsTravel, t.Notes, Convert.ToBase64String(t.RowVersion));

    public async Task ValidateAsync(TravelOverrideInput input, TravelOverride? existing, CancellationToken ct)
    {
        if (input.WeekFriday.DayOfWeek != DayOfWeek.Friday)
        {
            throw RuleViolationException.For("weekFriday", "Travel overrides are keyed by the trip Friday.");
        }

        if (await db.TravelOverrides.AnyAsync(t => t.WeekFriday == input.WeekFriday && t.Id != (existing == null ? 0 : existing.Id), ct))
        {
            throw RuleViolationException.For("weekFriday", "That week already has an override.");
        }
    }

    public void Apply(TravelOverrideInput input, TravelOverride entity)
    {
        entity.WeekFriday = input.WeekFriday;
        entity.IsTravel = input.IsTravel;
        entity.Notes = input.Notes;
    }
}

// ---- Plan tasks (read side; full CRUD and BR-18 arrive with M5) ----

public sealed record PlanTaskDto(
    int Id,
    int PhaseNumber,
    PlanTrack Track,
    PlanTaskKind Kind,
    string Title,
    string? Description,
    string? OutputRequired,
    string? Fallback,
    int DayFrom,
    int DayTo,
    int? PlanWeek,
    PlanTaskStatus Status,
    DateOnly? CompletedOn,
    string? SeedKey,
    string RowVersion,
    IReadOnlyList<string>? DdiaChapters);

public sealed record ListPlanTasks(int? Week, int? Day, PlanTrack? Track, PlanTaskStatus? Status, int? Phase, ListQuery List);

public sealed class ListPlanTasksHandler(IAppDbContext db, PlanContext plan, PlanTaskEnricher enricher) : IHandler<ListPlanTasks, PagedResult<PlanTaskDto>>
{
    public static readonly Expression<Func<PlanTask, PlanTaskDto>> Projection = t => new PlanTaskDto(
        t.Id, t.Phase!.Number, t.Track, t.Kind, t.Title, t.Description, t.OutputRequired, t.Fallback, t.DayFrom, t.DayTo, t.PlanWeek,
        t.StatusOverride ?? t.Status, t.CompletedOn, t.SeedKey, Convert.ToBase64String(t.RowVersion), null);

    public async Task<PagedResult<PlanTaskDto>> HandleAsync(ListPlanTasks r, CancellationToken ct)
    {
        var q = db.PlanTasks.AsNoTracking();
        if (r.Week is { } week)
        {
            var snap = await plan.LoadAsync(ct);
            var days = PlanCalendar.DaysForWeek(week, snap.Phases);
            if (days is null)
            {
                return new PagedResult<PlanTaskDto>([], 0, 1, r.List.SafePageSize);
            }

            var (from, to) = days.Value;
            q = q.Where(t => t.DayFrom <= to && t.DayTo >= from);
        }

        if (r.Day is { } day)
        {
            q = q.Where(t => t.DayFrom <= day && t.DayTo >= day);
        }

        if (r.Track is { } track)
        {
            q = q.Where(t => t.Track == track);
        }

        if (r.Phase is { } phase)
        {
            q = q.Where(t => t.Phase!.Number == phase);
        }

        // Effective status (BR-18) is computed after loading, so filter and page in memory (a few hundred tasks at most).
        var all = await enricher.EnrichAsync(await q.ApplySort(r.List.Sort, "dayFrom").Take(2000).Select(Projection).ToListAsync(ct), ct);
        var filtered = r.Status is { } status ? all.Where(t => t.Status == status).ToList() : all.ToList();
        var items = filtered.Skip((r.List.SafePage - 1) * r.List.SafePageSize).Take(r.List.SafePageSize).ToList();
        return new PagedResult<PlanTaskDto>(items, filtered.Count, r.List.SafePage, r.List.SafePageSize);
    }
}
