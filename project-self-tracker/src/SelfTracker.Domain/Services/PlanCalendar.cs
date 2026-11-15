using SelfTracker.Domain.Common;

namespace SelfTracker.Domain.Services;

public sealed record PhaseRange(int Number, string Name, int DayFrom, int DayTo, int WeekFrom, int WeekTo);

public enum PlanDayStatus { NotStarted, Active, Paused, Year2 }

public sealed record PlanDayInfo(
    DateOnly Date,
    PlanDayStatus Status,
    int Day,
    bool IsPaused,
    PhaseRange? Phase,
    int? Week,
    SchedulePhase SchedulePhase);

/// <summary>
/// BR-01 (plan day), BR-02 (phase, phase-relative week, schedule phase) and BR-03 (date for plan day).
/// Pure: takes the start date and pause ranges as input.
/// </summary>
public sealed class PlanCalendar
{
    public const int PlanLength = 365;

    public static readonly IReadOnlyList<PhaseRange> DefaultPhases =
    [
        new(1, "Core Gate", 1, 90, 1, 13),
        new(2, "Flagship Build", 91, 220, 14, 31),
        new(3, "Scale & Polish", 221, 300, 32, 43),
        new(4, "Interview Sprint", 301, 365, 44, 52),
    ];

    private readonly List<(DateOnly Start, DateOnly End)> _pauses;
    private readonly IReadOnlyList<PhaseRange> _phases;

    public PlanCalendar(DateOnly start, IEnumerable<(DateOnly Start, DateOnly End)>? pauses = null, IReadOnlyList<PhaseRange>? phases = null)
    {
        Start = start;
        _phases = phases is { Count: > 0 } ? phases.OrderBy(p => p.DayFrom).ToList() : DefaultPhases;
        _pauses = Merge(pauses ?? []);
    }

    public DateOnly Start { get; }

    public IReadOnlyList<PhaseRange> Phases => _phases;

    public bool IsPaused(DateOnly date) => _pauses.Any(p => date >= p.Start && date <= p.End);

    /// <summary>BR-01: non-paused calendar days from the start date to <paramref name="date"/> inclusive; 0 before the start.</summary>
    public int PlanDay(DateOnly date)
    {
        if (date < Start)
        {
            return 0;
        }

        var calendarDays = date.DayNumber - Start.DayNumber + 1;
        return calendarDays - PausedDaysBetween(Start, date);
    }

    public PlanDayInfo Describe(DateOnly date)
    {
        var day = PlanDay(date);
        var paused = date >= Start && IsPaused(date);
        if (date < Start)
        {
            return new(date, PlanDayStatus.NotStarted, 0, false, null, null, SchedulePhase.A);
        }

        if (day > PlanLength)
        {
            return new(date, PlanDayStatus.Year2, day, paused, null, null, SchedulePhase.C);
        }

        // A paused date before any active day keeps day 0 and still shows phase 1 / week 1 context.
        var effective = Math.Max(day, 1);
        var phase = PhaseFor(effective, _phases);
        var week = WeekFor(effective, _phases);
        return new(date, paused ? PlanDayStatus.Paused : PlanDayStatus.Active, day, paused, phase, week, ScheduleFor(week ?? 1));
    }

    /// <summary>BR-03: the first non-paused date whose plan day equals <paramref name="day"/>.</summary>
    public DateOnly DateForDay(int day)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(day, 1);
        var candidate = Start.AddDays(day - 1);
        while (true)
        {
            var current = PlanDay(candidate);
            if (current == day && !IsPaused(candidate))
            {
                return candidate;
            }

            candidate = candidate.AddDays(Math.Max(1, day - current));
        }
    }

    public static PhaseRange? PhaseFor(int day, IReadOnlyList<PhaseRange> phases) =>
        phases.FirstOrDefault(p => day >= p.DayFrom && day <= p.DayTo);

    /// <summary>BR-02: phase-relative week, clamped to the phase's last week. Never ceil(day / 7).</summary>
    public static int? WeekFor(int day, IReadOnlyList<PhaseRange> phases)
    {
        var phase = PhaseFor(day, phases);
        if (phase is null)
        {
            return null;
        }

        var week = phase.WeekFrom + ((day - phase.DayFrom) / 7);
        return Math.Min(week, phase.WeekTo);
    }

    /// <summary>BR-02: weeks 1-3 = A, 4-8 = B, 9 onward = C.</summary>
    public static SchedulePhase ScheduleFor(int week) => week switch
    {
        <= 3 => SchedulePhase.A,
        <= 8 => SchedulePhase.B,
        _ => SchedulePhase.C,
    };

    /// <summary>Day range for a plan week (inverse of <see cref="WeekFor"/>).</summary>
    public static (int DayFrom, int DayTo)? DaysForWeek(int week, IReadOnlyList<PhaseRange> phases)
    {
        var phase = phases.FirstOrDefault(p => week >= p.WeekFrom && week <= p.WeekTo);
        if (phase is null)
        {
            return null;
        }

        var from = phase.DayFrom + ((week - phase.WeekFrom) * 7);
        var to = week == phase.WeekTo ? phase.DayTo : Math.Min(from + 6, phase.DayTo);
        return (from, to);
    }

    private int PausedDaysBetween(DateOnly from, DateOnly to)
    {
        var total = 0;
        foreach (var (s, e) in _pauses)
        {
            var start = s > from ? s : from;
            var end = e < to ? e : to;
            if (end >= start)
            {
                total += end.DayNumber - start.DayNumber + 1;
            }
        }

        return total;
    }

    private static List<(DateOnly Start, DateOnly End)> Merge(IEnumerable<(DateOnly Start, DateOnly End)> ranges)
    {
        var sorted = ranges.Where(r => r.End >= r.Start).OrderBy(r => r.Start).ToList();
        var merged = new List<(DateOnly Start, DateOnly End)>();
        foreach (var r in sorted)
        {
            if (merged.Count > 0 && r.Start.DayNumber <= merged[^1].End.DayNumber + 1)
            {
                var last = merged[^1];
                merged[^1] = (last.Start, r.End > last.End ? r.End : last.End);
            }
            else
            {
                merged.Add(r);
            }
        }

        return merged;
    }
}
