using SelfTracker.Domain.Common;

namespace SelfTracker.Domain.Services;

/// <summary>
/// BR-04: Edappal trips start on TravelAnchorFriday + 14k days and run Friday to Monday inclusive.
/// Per-week overrides (keyed by the trip Friday) can add or cancel a trip.
/// </summary>
public sealed class TravelCalendar(bool enabled, DateOnly? anchorFriday, IReadOnlyDictionary<DateOnly, bool>? overrides = null)
{
    private readonly IReadOnlyDictionary<DateOnly, bool> _overrides = overrides ?? new Dictionary<DateOnly, bool>();

    public static TravelCalendar Disabled { get; } = new(false, null);

    public bool IsTripFriday(DateOnly friday)
    {
        if (friday.DayOfWeek != DayOfWeek.Friday)
        {
            return false;
        }

        if (_overrides.TryGetValue(friday, out var forced))
        {
            return forced;
        }

        if (!enabled || anchorFriday is null)
        {
            return false;
        }

        return (friday.DayNumber - anchorFriday.Value.DayNumber) % 14 == 0;
    }

    /// <summary>The Friday of the trip that covers <paramref name="date"/>, if any.</summary>
    public DateOnly? TripFridayFor(DateOnly date)
    {
        var offset = OffsetFromFriday(date);
        if (offset > 3)
        {
            return null;
        }

        var friday = date.AddDays(-offset);
        return IsTripFriday(friday) ? friday : null;
    }

    public TravelRole RoleFor(DateOnly date)
    {
        if (TripFridayFor(date) is null)
        {
            return TravelRole.None;
        }

        return OffsetFromFriday(date) switch
        {
            0 => TravelRole.PreplannedMvd,
            1 => TravelRole.TimedAttempt,
            2 => TravelRole.Review,
            _ => TravelRole.ShortReview,
        };
    }

    public bool IsTravelDay(DateOnly date) => RoleFor(date) != TravelRole.None;

    /// <summary>Trip Fridays whose Friday-Monday window overlaps [from, to].</summary>
    public IEnumerable<DateOnly> TripsBetween(DateOnly from, DateOnly to)
    {
        var friday = from.AddDays(-OffsetFromFriday(from));
        for (var f = friday; f <= to; f = f.AddDays(7))
        {
            if (IsTripFriday(f))
            {
                yield return f;
            }
        }
    }

    private static int OffsetFromFriday(DateOnly date) => ((int)date.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7;
}
