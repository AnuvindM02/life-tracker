using SelfTracker.Domain.Entities;

namespace SelfTracker.Domain.Services;

public sealed record CountResult(bool IsCounted, string? Reason);

public sealed record PlannedReview(DateOnly DueDate, int IntervalDays);

public sealed record MarkerBand(int Day, int Min, int Max, string Label);

/// <summary>What happens to the problem's ladder when a cold review is logged (BR-09, spec v1.1).</summary>
public enum ReviewOutcome
{
    /// <summary>Confidence 1-3: supersede the remaining items and restart at +1 day from the review.</summary>
    RestartLadder,

    /// <summary>Confidence 4-5: the remaining items stay.</summary>
    KeepRemaining,
}

/// <summary>BR-08 (meaningful attempt), BR-09 (spaced review), BR-10 (markers). Pure.</summary>
public static class DsaRules
{
    public static readonly IReadOnlyList<int> DefaultIntervals = [1, 3, 7, 21];

    public static readonly IReadOnlyList<MarkerBand> Markers =
    [
        new(90, 65, 80, "65-80 meaningful attempts/reviews by Day 90"),
        new(220, 120, 135, "about 120-135 selected attempts/reviews by Day 220"),
    ];

    public const string MarkerCaveat = "Cold re-solves and pattern coverage matter more than raw count.";

    public const int WeeklyNewMin = 5;
    public const int WeeklyNewMax = 6;

    /// <summary>
    /// BR-08 (v1.1): counted if (New or Timed, Pass 1 at least the unaided minimum, and the mistake-log line filled)
    /// or (a cold review of a library problem, linked to a review item or not).
    /// </summary>
    public static CountResult Evaluate(AttemptType type, int pass1Minutes, int unaidedMinMinutes, string? whatIMissed, bool hasProblem)
    {
        if (type == AttemptType.ColdReview)
        {
            return hasProblem
                ? new(true, null)
                : new(false, "Cold reviews count only for problems in the library. Add the problem to count it.");
        }

        var reasons = new List<string>();
        if (pass1Minutes < unaidedMinMinutes)
        {
            reasons.Add($"Pass 1 (unaided) was {pass1Minutes} min; a counted attempt needs {unaidedMinMinutes}+ minutes.");
        }

        if (string.IsNullOrWhiteSpace(whatIMissed))
        {
            reasons.Add("The mistake-log line (what I missed) is empty.");
        }

        return reasons.Count == 0 ? new(true, null) : new(false, string.Join(' ', reasons));
    }

    /// <summary>BR-09: confidence 1-3 -> +1, +3, +7, +21 days (configurable); 4-5 -> one review after HighConfidenceReviewDays.</summary>
    public static IReadOnlyList<PlannedReview> PlanLadder(DateOnly from, int confidence, IReadOnlyList<int> intervals, int highConfidenceDays)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(confidence, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(confidence, 5);
        return confidence <= 3
            ? intervals.Select(i => new PlannedReview(from.AddDays(i), i)).ToList()
            : [new PlannedReview(from.AddDays(highConfidenceDays), highConfidenceDays)];
    }

    public static ReviewOutcome OnReview(int confidence) => confidence <= 3 ? ReviewOutcome.RestartLadder : ReviewOutcome.KeepRemaining;

    public static IReadOnlyList<int> ParseIntervals(string? csv)
    {
        var values = (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var v) ? v : 0).Where(v => v > 0).Distinct().Order().ToList();
        return values.Count > 0 ? values : DefaultIntervals;
    }

    /// <summary>Problem status after an attempt: solved unaided wins; otherwise at least Attempted. Never downgrades Solved.</summary>
    public static ProblemStatus StatusAfterAttempt(ProblemStatus current, bool solvedUnaided) =>
        solvedUnaided || current == ProblemStatus.Solved ? ProblemStatus.Solved : ProblemStatus.Attempted;

    /// <summary>BR-10: the marker band relevant to a plan day (the next one ahead, or the last).</summary>
    public static MarkerBand MarkerFor(int planDay) => Markers.FirstOrDefault(m => planDay <= m.Day) ?? Markers[^1];

    /// <summary>Median of a list of minutes; null when empty.</summary>
    public static double? Median(IEnumerable<int> values)
    {
        var sorted = values.Order().ToList();
        if (sorted.Count == 0)
        {
            return null;
        }

        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}
