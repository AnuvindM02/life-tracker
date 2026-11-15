using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;

namespace SelfTracker.Application.Features;

public sealed record ScheduleBlockDto(
    int Id,
    SchedulePhase SchedulePhase,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string? TimeLabel,
    string Title,
    string Rule,
    bool IsStretch,
    int SortOrder,
    bool IsUserModified,
    string RowVersion);

public sealed record ScheduleBlockInput(
    SchedulePhase SchedulePhase,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    [property: StringLength(60)] string? TimeLabel,
    [property: Required, StringLength(120)] string Title,
    [property: StringLength(1000)] string? Rule,
    bool IsStretch,
    int SortOrder,
    string? RowVersion) : IVersioned;

public sealed class ScheduleBlockSpec : ICrudSpec<ScheduleBlock, ScheduleBlockDto, ScheduleBlockInput>
{
    public string Name => "Schedule block";

    public string DefaultSort => "sortOrder";

    public Expression<Func<ScheduleBlock, ScheduleBlockDto>> Projection => b => new ScheduleBlockDto(
        b.Id, b.SchedulePhase, b.StartTime, b.EndTime, b.TimeLabel, b.Title, b.Rule, b.IsStretch, b.SortOrder, b.IsUserModified,
        Convert.ToBase64String(b.RowVersion));

    public IQueryable<ScheduleBlock> Filter(IQueryable<ScheduleBlock> query, IReadOnlyDictionary<string, string?> filters) =>
        filters.TryGetValue("phase", out var p) && Enum.TryParse<SchedulePhase>(p, true, out var phase)
            ? query.Where(b => b.SchedulePhase == phase)
            : query;

    public void Apply(ScheduleBlockInput input, ScheduleBlock entity)
    {
        entity.SchedulePhase = input.SchedulePhase;
        entity.StartTime = input.StartTime;
        entity.EndTime = input.EndTime;
        entity.TimeLabel = input.TimeLabel;
        entity.Title = input.Title;
        entity.Rule = input.Rule ?? "";
        entity.IsStretch = input.IsStretch;
        entity.SortOrder = input.SortOrder;
    }
}

public sealed record GymPlanDayDto(int Id, SchedulePhase SchedulePhase, DayOfWeek DayOfWeek, string SessionName, bool IsOptional, string? Notes, string RowVersion);

public sealed record GymPlanDayInput(
    SchedulePhase SchedulePhase,
    DayOfWeek DayOfWeek,
    [property: Required, StringLength(120)] string SessionName,
    bool IsOptional,
    [property: StringLength(1000)] string? Notes,
    string? RowVersion) : IVersioned;

public sealed class GymPlanDaySpec : ICrudSpec<GymPlanDay, GymPlanDayDto, GymPlanDayInput>
{
    public string Name => "Gym plan day";

    public string DefaultSort => "dayOfWeek";

    public Expression<Func<GymPlanDay, GymPlanDayDto>> Projection => g => new GymPlanDayDto(
        g.Id, g.SchedulePhase, g.DayOfWeek, g.SessionName, g.IsOptional, g.Notes, Convert.ToBase64String(g.RowVersion));

    public IQueryable<GymPlanDay> Filter(IQueryable<GymPlanDay> query, IReadOnlyDictionary<string, string?> filters) =>
        filters.TryGetValue("phase", out var p) && Enum.TryParse<SchedulePhase>(p, true, out var phase)
            ? query.Where(b => b.SchedulePhase == phase)
            : query;

    public void Apply(GymPlanDayInput input, GymPlanDay entity)
    {
        entity.SchedulePhase = input.SchedulePhase;
        entity.DayOfWeek = input.DayOfWeek;
        entity.SessionName = input.SessionName;
        entity.IsOptional = input.IsOptional;
        entity.Notes = input.Notes;
    }
}
