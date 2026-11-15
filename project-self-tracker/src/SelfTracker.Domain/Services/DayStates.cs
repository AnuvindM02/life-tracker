using SelfTracker.Domain.Common;

namespace SelfTracker.Domain.Services;

public sealed record ActiveWindow(int ActiveDays, int MvdDays, int PausedDays, int WindowDays, int Target);

public sealed record MissStatus(bool NeverMissedTwice, int ConsecutiveMissedEndingYesterday, DateOnly? LastDoubleMissStart);

/// <summary>BR-05: day states, active days in the last 30 and the "never missed twice" status.</summary>
public static class DayStates
{
    public const int ActiveTarget = 26;
    public const int WindowDays = 30;

    /// <param name="logType">null when no log exists; otherwise the log's day type, which is itself null while In progress.</param>
    public static DayState For(DateOnly date, DateOnly today, PlanCalendar calendar, bool hasLog, DayType? logType)
    {
        if (hasLog && logType is { } type)
        {
            return type switch
            {
                DayType.Full => DayState.Full,
                DayType.Mvd => DayState.Mvd,
                _ => DayState.Rest,
            };
        }

        if (date > today)
        {
            return DayState.Future;
        }

        if (date < calendar.Start)
        {
            return DayState.NotStarted;
        }

        if (calendar.IsPaused(date))
        {
            return DayState.Paused;
        }

        if (date == today)
        {
            return DayState.Today;
        }

        return hasLog ? DayState.Unclassified : DayState.Missed;
    }

    public static bool IsActive(DayState state) => state is DayState.Full or DayState.Mvd;

    /// <summary>Active (Full or MVD) logs in the 30 calendar days ending today; paused dates are excluded and reported.</summary>
    public static ActiveWindow ActiveInLast30(DateOnly today, PlanCalendar calendar, IReadOnlyDictionary<DateOnly, DayType?> logs)
    {
        int active = 0, mvd = 0, paused = 0;
        for (var d = today.AddDays(-(WindowDays - 1)); d <= today; d = d.AddDays(1))
        {
            if (d >= calendar.Start && calendar.IsPaused(d))
            {
                paused++;
                continue;
            }

            if (logs.TryGetValue(d, out var type) && type is DayType.Full or DayType.Mvd)
            {
                active++;
                if (type == DayType.Mvd)
                {
                    mvd++;
                }
            }
        }

        return new(active, mvd, paused, WindowDays, ActiveTarget);
    }

    /// <summary>
    /// "Never missed twice" since the last pause ended: no two consecutive Missed dates up to yesterday.
    /// Also returns the current run of consecutive missed days ending yesterday (used by nudges).
    /// </summary>
    public static MissStatus MissStatus(DateOnly today, PlanCalendar calendar, IReadOnlyDictionary<DateOnly, DayType?> logs, DateOnly? lastPauseEnd)
    {
        var from = calendar.Start;
        if (lastPauseEnd is { } end && end.AddDays(1) > from)
        {
            from = end.AddDays(1);
        }

        var run = 0;
        DateOnly? doubleStart = null;
        for (var d = from; d < today; d = d.AddDays(1))
        {
            var hasLog = logs.TryGetValue(d, out var type);
            var state = For(d, today, calendar, hasLog, type);
            if (state == DayState.Missed)
            {
                run++;
                if (run >= 2)
                {
                    doubleStart = d.AddDays(-1);
                }
            }
            else
            {
                run = 0;
            }
        }

        return new(doubleStart is null, run, doubleStart);
    }
}
