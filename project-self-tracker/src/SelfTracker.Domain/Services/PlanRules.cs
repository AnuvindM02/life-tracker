using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;

namespace SelfTracker.Domain.Services;

public enum CheckpointState { Upcoming, Soon, DueToday, Overdue, Answered }

public sealed record ProblemProgress(ProblemStatus Status, bool HasAttempt, bool HasCountedAttempt);

public sealed record OpenTask(int Id, int PhaseNumber, int DayTo, PlanTaskStatus Status);

/// <summary>BR-11 checkpoints, BR-13 "behind", BR-18 derived task status, BR-12 hour stages. Pure.</summary>
public static class PlanRules
{
    public const int ReminderDays = 3;
    public const int BehindDays = 14;
    public const int Phase1BehindDays = 21;

    /// <summary>BR-11: reminder 3 days before; overdue when past due and unanswered.</summary>
    public static CheckpointState CheckpointStateFor(DateOnly due, DateOnly today, bool answered)
    {
        if (answered)
        {
            return CheckpointState.Answered;
        }

        var days = due.DayNumber - today.DayNumber;
        return days switch
        {
            < 0 => CheckpointState.Overdue,
            0 => CheckpointState.DueToday,
            <= ReminderDays => CheckpointState.Soon,
            _ => CheckpointState.Upcoming,
        };
    }

    public static bool IsOpen(PlanTaskStatus status) => status is PlanTaskStatus.NotStarted or PlanTaskStatus.InProgress;

    /// <summary>
    /// BR-18 (DSA): Done when every problem of the week is Solved, Deferred or has a counted attempt;
    /// In progress when any has an attempt. Null when the week has no problems (keep the stored status).
    /// </summary>
    public static PlanTaskStatus? DerivedDsaStatus(IReadOnlyCollection<ProblemProgress> problems)
    {
        if (problems.Count == 0)
        {
            return null;
        }

        if (problems.All(p => p.Status is ProblemStatus.Solved or ProblemStatus.Deferred || p.HasCountedAttempt))
        {
            return PlanTaskStatus.Done;
        }

        return problems.Any(p => p.HasAttempt || p.Status is ProblemStatus.Solved) ? PlanTaskStatus.InProgress : PlanTaskStatus.NotStarted;
    }

    /// <summary>BR-18 (DDIA): Done when every chapter planned for the week is Note done or later; In progress when one is Reading.</summary>
    public static PlanTaskStatus? DerivedDdiaStatus(IReadOnlyCollection<ChapterStatus> chapters)
    {
        if (chapters.Count == 0)
        {
            return null;
        }

        if (chapters.All(c => c >= ChapterStatus.NoteDone))
        {
            return PlanTaskStatus.Done;
        }

        return chapters.Any(c => c >= ChapterStatus.Reading) ? PlanTaskStatus.InProgress : PlanTaskStatus.NotStarted;
    }

    /// <summary>A manual Deferred or Skipped always wins over a derived status.</summary>
    public static PlanTaskStatus Effective(PlanTaskStatus stored, PlanTaskStatus? derived, PlanTaskStatus? manualOverride) =>
        manualOverride is PlanTaskStatus.Deferred or PlanTaskStatus.Skipped ? manualOverride.Value : derived ?? stored;

    /// <summary>
    /// BR-13 "behind": the oldest open task (not Done, Deferred or Skipped) whose end day is more than
    /// <paramref name="thresholdDays"/> plan days in the past. Returns that task, or null.
    /// </summary>
    public static OpenTask? Behind(IEnumerable<OpenTask> tasks, int planDay, int thresholdDays, int? phaseNumber = null) =>
        tasks.Where(t => IsOpen(t.Status) && (phaseNumber is null || t.PhaseNumber == phaseNumber) && planDay - t.DayTo > thresholdDays)
            .OrderBy(t => t.DayTo).ThenBy(t => t.Id).FirstOrDefault();

    /// <summary>BR-12 stage for a plan week: A (1-3), B (4-8), then the phase's per-category targets.</summary>
    public static string HourStage(int planWeek, int phaseNumber) => planWeek switch
    {
        <= 3 => "A",
        <= 8 => "B",
        _ => $"Phase{phaseNumber}",
    };

    /// <summary>
    /// BR-13 DSA plateau: weekly average confidence of new attempts did not increase over 3 consecutive weeks
    /// (needs 4 weekly averages, oldest first) and the latest average is below 3.5.
    /// </summary>
    public static bool DsaPlateau(IReadOnlyList<double> weeklyAverages)
    {
        if (weeklyAverages.Count < 4)
        {
            return false;
        }

        var last = weeklyAverages.TakeLast(4).ToList();
        return last[1] <= last[0] && last[2] <= last[1] && last[3] <= last[2] && last[3] < 3.5;
    }

    /// <summary>BR-13 recovery signal: sleep average below 7 h over the last 5 logged days, or energy 2 or lower on the last 3 logged days.</summary>
    public static bool RecoverySignal(IReadOnlyList<decimal> lastSleepHours, IReadOnlyList<int> lastEnergy) =>
        (lastSleepHours.Count >= 5 && lastSleepHours.TakeLast(5).Average() < 7m)
        || (lastEnergy.Count >= 3 && lastEnergy.TakeLast(3).All(e => e <= 2));

    public static DateOnly MondayOf(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    public static DateOnly FirstOfMonth(DateOnly d) => new(d.Year, d.Month, 1);

    public static int DaysLeftInMonth(DateOnly d) => DateTime.DaysInMonth(d.Year, d.Month) - d.Day;
}
