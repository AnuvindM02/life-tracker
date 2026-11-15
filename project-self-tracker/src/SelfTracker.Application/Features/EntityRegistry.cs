using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;

namespace SelfTracker.Application.Features;

/// <summary>Entity types that can carry attachments (FileLink targets) or appear in the Trash.</summary>
public sealed record EntityTypeInfo(string Name, Type ClrType, bool Attachable, bool Trashable, Func<Entity, string> Title);

public static class EntityRegistry
{
    public static readonly IReadOnlyList<EntityTypeInfo> All =
    [
        new("DailyLog", typeof(DailyLog), true, true, e => $"Daily log {((DailyLog)e).Date:yyyy-MM-dd}"),
        new("DailyTick", typeof(DailyTick), true, false, e => $"{((DailyTick)e).Type} tick"),
        new("TimeEntry", typeof(TimeEntry), false, true, e => $"{((TimeEntry)e).Minutes} min {((TimeEntry)e).Category} on {((TimeEntry)e).Date:yyyy-MM-dd}"),
        new("PauseRange", typeof(PauseRange), false, true, e => $"Pause {((PauseRange)e).StartDate:yyyy-MM-dd} to {((PauseRange)e).EndDate:yyyy-MM-dd}"),
        new("TravelOverride", typeof(TravelOverride), false, true, e => $"Travel override {((TravelOverride)e).WeekFriday:yyyy-MM-dd}"),
        new("ScheduleBlock", typeof(ScheduleBlock), false, true, e => $"Schedule block {((ScheduleBlock)e).Title}"),
        new("GymPlanDay", typeof(GymPlanDay), false, true, e => $"Gym plan {((GymPlanDay)e).DayOfWeek} {((GymPlanDay)e).SessionName}"),
        new("PlanTask", typeof(PlanTask), true, true, e => $"Plan task {((PlanTask)e).Title}"),
        new("Checkpoint", typeof(Checkpoint), true, true, e => $"Checkpoint day {((Checkpoint)e).PlanDay}: {((Checkpoint)e).Title}"),
        new("FileAsset", typeof(FileAsset), false, true, e => $"File {((FileAsset)e).DisplayTitle}"),
        new("DsaTopic", typeof(DsaTopic), false, true, e => $"DSA topic {((DsaTopic)e).Name}"),
        new("DdiaChapter", typeof(DdiaChapter), true, true, e => $"DDIA {((DdiaChapter)e).Edition} edition ch. {((DdiaChapter)e).Number}: {((DdiaChapter)e).Title}"),
        new("ReadingLog", typeof(ReadingLog), false, true, e => $"Reading log {((ReadingLog)e).Date:yyyy-MM-dd}: {((ReadingLog)e).PagesRead} pages"),
        new("DdiaNote", typeof(DdiaNote), true, true, e => $"DDIA chapter note #{e.Id}"),
        new("DesignDrill", typeof(DesignDrill), true, true, e => $"Design drill {((DesignDrill)e).Number}: {((DesignDrill)e).Prompt}"),
        new("ArticulationLog", typeof(ArticulationLog), true, true, e => $"Articulation {((ArticulationLog)e).Date:yyyy-MM-dd}: {((ArticulationLog)e).Prompt}"),
        new("ArticulationPrompt", typeof(ArticulationPrompt), false, true, e => $"Prompt: {((ArticulationPrompt)e).Text}"),
        new("ExitCriterion", typeof(ExitCriterion), true, true, e => $"Exit criterion: {((ExitCriterion)e).Text}"),
        new("ReplanEvent", typeof(ReplanEvent), true, true, e => $"Re-plan {((ReplanEvent)e).Date:yyyy-MM-dd}: {((ReplanEvent)e).Trigger}"),
        new("WeeklyReview", typeof(WeeklyReview), true, true, e => $"Weekly review {((WeeklyReview)e).WeekStart:yyyy-MM-dd}"),
        new("MonthlyReview", typeof(MonthlyReview), true, true, e => $"Monthly review {((MonthlyReview)e).Month:yyyy-MM}"),
        new("SomedayItem", typeof(SomedayItem), false, true, e => $"Someday: {((SomedayItem)e).Title}"),
        new("DsaProblem", typeof(DsaProblem), true, true, e => $"DSA problem {((DsaProblem)e).Title}"),
        new("DsaAttempt", typeof(DsaAttempt), true, true, e => $"DSA attempt on {((DsaAttempt)e).Date:yyyy-MM-dd}{(((DsaAttempt)e).TitleFreeText is { } t ? $": {t}" : "")}"),
    ];

    public static EntityTypeInfo? Find(string? name) =>
        All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    public static EntityTypeInfo Require(string? name) =>
        Find(name) ?? throw new BadRequestException($"Unknown entity type '{name}'.");

    public static EntityTypeInfo? ForClr(Type type) => All.FirstOrDefault(t => t.ClrType == type);
}

/// <summary>Reflection-free-at-call-site helpers over the registry's DbSets.</summary>
public sealed class EntityLookup(IAppDbContext db)
{
    private static readonly MethodInfo ExistsMethod = typeof(EntityLookup).GetMethod(nameof(ExistsCore), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly MethodInfo FindMethod = typeof(EntityLookup).GetMethod(nameof(FindCore), BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly MethodInfo DeletedMethod = typeof(EntityLookup).GetMethod(nameof(DeletedCore), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public Task<bool> ExistsAsync(EntityTypeInfo type, int id, CancellationToken ct) =>
        (Task<bool>)ExistsMethod.MakeGenericMethod(type.ClrType).Invoke(this, [id, ct])!;

    /// <summary>Find including soft-deleted rows (tracked).</summary>
    public Task<Entity?> FindAnyAsync(EntityTypeInfo type, int id, CancellationToken ct) =>
        (Task<Entity?>)FindMethod.MakeGenericMethod(type.ClrType).Invoke(this, [id, ct])!;

    public Task<List<Entity>> DeletedAsync(EntityTypeInfo type, DateTimeOffset? olderThan, CancellationToken ct) =>
        (Task<List<Entity>>)DeletedMethod.MakeGenericMethod(type.ClrType).Invoke(this, [olderThan, ct])!;

    private Task<bool> ExistsCore<T>(int id, CancellationToken ct) where T : Entity =>
        db.Set<T>().AnyAsync(e => e.Id == id, ct);

    private async Task<Entity?> FindCore<T>(int id, CancellationToken ct) where T : Entity =>
        await db.Set<T>().IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == id, ct);

    private async Task<List<Entity>> DeletedCore<T>(DateTimeOffset? olderThan, CancellationToken ct) where T : Entity
    {
        var q = db.Set<T>().IgnoreQueryFilters().Where(e => e.IsDeleted);
        if (olderThan is { } cutoff)
        {
            q = q.Where(e => e.DeletedAt != null && e.DeletedAt < cutoff);
        }

        return (await q.OrderByDescending(e => e.DeletedAt).Take(1000).ToListAsync(ct)).Cast<Entity>().ToList();
    }
}
