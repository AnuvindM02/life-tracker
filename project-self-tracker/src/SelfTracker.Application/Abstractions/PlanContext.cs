using Microsoft.EntityFrameworkCore;
using SelfTracker.Domain.Entities;
using SelfTracker.Domain.Services;

namespace SelfTracker.Application.Abstractions;

/// <summary>Everything the plan rules need, loaded once per request.</summary>
public sealed record PlanSnapshot(
    AppSettings Settings,
    PlanCalendar? Calendar,
    TravelCalendar Travel,
    IReadOnlyList<PhaseRange> Phases,
    IReadOnlyList<PauseRange> Pauses,
    DateOnly Today,
    TimeZoneInfo Zone)
{
    public PlanCalendar RequireCalendar() =>
        Calendar ?? throw new ConflictException("Finish first-run setup (plan start date) before using plan views.");
}

public sealed class PlanContext(IAppDbContext db, IClock clock)
{
    private PlanSnapshot? _cached;

    public static TimeZoneInfo ResolveZone(string? id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(id) ? "Asia/Kolkata" : id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public static DateOnly TodayIn(DateTimeOffset utcNow, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, zone).DateTime);

    public async Task<AppSettings> GetSettingsAsync(CancellationToken ct) =>
        await db.AppSettings.OrderBy(s => s.Id).FirstOrDefaultAsync(ct) ?? new AppSettings();

    public async Task<PlanSnapshot> LoadAsync(CancellationToken ct)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var settings = await GetSettingsAsync(ct);
        var pauses = await db.PauseRanges.AsNoTracking().OrderBy(p => p.StartDate).ToListAsync(ct);
        var phases = await db.Phases.AsNoTracking().OrderBy(p => p.DayFrom)
            .Select(p => new PhaseRange(p.Number, p.Name, p.DayFrom, p.DayTo, p.WeekFrom, p.WeekTo))
            .ToListAsync(ct);
        if (phases.Count == 0)
        {
            phases = [.. PlanCalendar.DefaultPhases];
        }

        var overrides = await db.TravelOverrides.AsNoTracking().ToDictionaryAsync(o => o.WeekFriday, o => o.IsTravel, ct);
        var zone = ResolveZone(settings.TimeZone);
        var calendar = settings.PlanStartDate is { } start
            ? new PlanCalendar(start, pauses.Select(p => (p.StartDate, p.EndDate)), phases)
            : null;
        var travel = new TravelCalendar(settings.TravelEnabled, settings.TravelAnchorFriday, overrides);
        _cached = new PlanSnapshot(settings, calendar, travel, phases, pauses, TodayIn(clock.UtcNow, zone), zone);
        return _cached;
    }

    public void Invalidate() => _cached = null;
}
