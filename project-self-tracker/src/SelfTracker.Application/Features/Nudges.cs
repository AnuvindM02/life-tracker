using Microsoft.EntityFrameworkCore;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;
using SelfTracker.Domain.Services;

namespace SelfTracker.Application.Features;

/// <summary>
/// BR-13 nudges, computed on the fly (never stored). Calm and practical: no alarms, streaks or debt (BR-14).
/// The posting-count and written-offer nudges arrive with the Market and Interviews module (M7).
/// </summary>
public sealed class NudgeService(IAppDbContext db, PlanContext plan, PlanTaskEnricher tasks, CheckpointService checkpoints)
{
    public static class Keys
    {
        public const string MissedOne = "missed-one";
        public const string MissedTwo = "missed-two";
        public const string Behind = "behind-2w";
        public const string Phase1Behind = "phase1-behind-3w";
        public const string DsaPlateau = "dsa-plateau";
        public const string Recovery = "recovery";
        public const string WeeklyReview = "weekly-review";
        public const string MonthlyReview = "monthly-review";
        public const string DdiaEdition = "ddia-edition";
        public const string Checkpoint = "checkpoint";
        public const string SchedulePhase = "schedule-phase";
    }

    public async Task<IReadOnlyList<NudgeDto>> ComputeAsync(DateOnly? date, CancellationToken ct)
    {
        var snap = await plan.LoadAsync(ct);
        if (snap.Calendar is not { } cal || !snap.Settings.SetupCompleted)
        {
            return [];
        }

        var today = date ?? snap.Today;
        var day = PlanDayDto.From(snap, today);
        var list = new List<NudgeDto>();

        // Missed days (never miss twice).
        var logMap = await GetTodayHandler.LogMapAsync(db, today.AddDays(-60), today, ct);
        var lastPauseEnd = snap.Pauses.Where(p => p.EndDate < today).Select(p => (DateOnly?)p.EndDate).Max();
        var misses = DayStates.MissStatus(today, cal, logMap, lastPauseEnd);
        var activeToday = logMap.TryGetValue(today, out var todayType) && todayType is DayType.Full or DayType.Mvd;
        if (!day.IsPaused && !activeToday)
        {
            if (misses.ConsecutiveMissedEndingYesterday >= 2)
            {
                list.Add(new(Keys.MissedTwo, "Never miss twice", "Do an MVD now and ramp back over 2-3 days. Nothing needs to be repaid.", "Log an MVD", "/logs/today?type=Mvd"));
            }
            else if (misses.ConsecutiveMissedEndingYesterday == 1)
            {
                list.Add(new(Keys.MissedOne, "MVD today", "Yesterday has no log. An MVD today is enough; log it and move on.", "Log an MVD", "/logs/today?type=Mvd"));
            }
        }

        // Behind the plan (BR-13): oldest open task of the current phase ended 14+ plan days ago; Phase 1 ended 21+.
        if (day.Day >= 1 && day.PhaseNumber is { } phase)
        {
            var open = (await tasks.AllAsync(ct)).Select(t => new OpenTask(t.Id, t.PhaseNumber, t.DayTo, t.Status)).ToList();
            if (PlanRules.Behind(open, day.Day, PlanRules.BehindDays, phase) is not null)
            {
                list.Add(new(Keys.Behind, "Cut scope, not consistency",
                    "Plan tasks are more than two weeks behind. Cut optional infrastructure, UI polish and extra features; keep DSA, DDIA and review.",
                    "Open plan tasks", "/plan/tasks?view=board"));
            }

            if (PlanRules.Behind(open, day.Day, PlanRules.Phase1BehindDays, 1) is not null)
            {
                list.Add(new(Keys.Phase1Behind, "Re-plan trigger 2 is met",
                    "Phase 1 is more than three weeks behind. Re-plan deliberately: record the evidence and the decision.", "Open re-plan form", "/plan/replans"));
            }
        }

        // DSA plateau: weekly average confidence of new attempts.
        var since = PlanRules.MondayOf(today).AddDays(-7 * 7);
        var fresh = await db.DsaAttempts.AsNoTracking().Where(a => a.Date >= since && a.Date <= today && a.Type != AttemptType.ColdReview)
            .Select(a => new { a.Date, a.Confidence }).ToListAsync(ct);
        var weekly = fresh.GroupBy(a => PlanRules.MondayOf(a.Date)).OrderBy(g => g.Key).Select(g => g.Average(a => a.Confidence)).ToList();
        if (PlanRules.DsaPlateau(weekly))
        {
            list.Add(new(Keys.DsaPlateau, "DSA plateau",
                "Confidence has not risen for three weeks. Pick one weak pattern and practise it with spaced reviews for two weeks. Do not switch languages.",
                "Open weakest problems", "/dsa/progress"));
        }

        // Recovery signal.
        var recent = await db.DailyLogs.AsNoTracking().Where(l => l.Date <= today).OrderByDescending(l => l.Date).Take(10)
            .Select(l => new { l.Date, l.SleepHours, l.Energy }).ToListAsync(ct);
        var sleep = recent.Where(r => r.SleepHours != null).Take(5).OrderBy(r => r.Date).Select(r => r.SleepHours!.Value).ToList();
        var energy = recent.Where(r => r.Energy != null).Take(3).OrderBy(r => r.Date).Select(r => r.Energy!.Value).ToList();
        if (PlanRules.RecoverySignal(sleep, energy))
        {
            list.Add(new(Keys.Recovery, "Take a lighter week", "Sleep or energy has been low. Take a lighter week and shrink the third study block first.",
                "Open weekly review", "/reviews/weekly"));
        }

        // Weekly review: due on Sunday; the previous week's review is still open from Monday.
        var thisMonday = PlanRules.MondayOf(today);
        var lastMonday = thisMonday.AddDays(-7);
        var reviewed = await db.WeeklyReviews.AsNoTracking().Where(r => r.WeekStart == thisMonday || r.WeekStart == lastMonday).Select(r => r.WeekStart).ToListAsync(ct);
        if (today.DayOfWeek == DayOfWeek.Sunday && !reviewed.Contains(thisMonday))
        {
            list.Add(new(Keys.WeeklyReview, "Weekly review today", "The weekly review is 30 minutes: learning, outputs, sleep, travel and job evidence.", "Open weekly review", "/reviews/weekly"));
        }
        else if (today.DayOfWeek != DayOfWeek.Sunday && cal.Start <= lastMonday.AddDays(6) && !reviewed.Contains(lastMonday))
        {
            list.Add(new(Keys.WeeklyReview, "Last week's review", "The weekly review is 30 minutes. Adjust to trends, not one bad week.", "Open weekly review", $"/reviews/weekly?week={lastMonday:yyyy-MM-dd}"));
        }

        // Monthly review: last 3 days of the month, or early next month if last month's is missing.
        var month = PlanRules.FirstOfMonth(today);
        var prevMonth = month.AddMonths(-1);
        var monthly = await db.MonthlyReviews.AsNoTracking().Where(m => m.Month == month || m.Month == prevMonth).Select(m => m.Month).ToListAsync(ct);
        if (PlanRules.DaysLeftInMonth(today) < 3 && !monthly.Contains(month))
        {
            list.Add(new(Keys.MonthlyReview, "Monthly review", "60 minutes: phase progress, mistake log themes, 20 live postings and whether a lighter week is needed.", "Open monthly review", "/reviews/monthly"));
        }
        else if (today.Day <= 5 && cal.Start < month && !monthly.Contains(prevMonth))
        {
            list.Add(new(Keys.MonthlyReview, "Last month's review", "60 minutes: phase progress, mistake log themes and someday-list triage.", "Open monthly review", $"/reviews/monthly?month={prevMonth:yyyy-MM-dd}"));
        }

        // DDIA edition.
        if (DdiaRules.ShowEditionWarning(snap.Settings.DdiaEdition, day.Week))
        {
            list.Add(new(Keys.DdiaEdition, "Confirm your DDIA edition", DdiaRules.EditionWarning, "Open DDIA", "/ddia"));
        }

        // Checkpoint within 3 days or overdue (BR-11).
        var cp = (await checkpoints.ListAsync(false, ct)).FirstOrDefault(c => c.State is CheckpointState.Soon or CheckpointState.DueToday or CheckpointState.Overdue);
        if (cp is not null)
        {
            var when = cp.State switch
            {
                CheckpointState.Overdue => $"was due {cp.DueDate:d MMM}",
                CheckpointState.DueToday => "is due today",
                _ => $"is due {cp.DueDate:d MMM}",
            };
            list.Add(new(Keys.Checkpoint, $"Day {cp.PlanDay} checkpoint: {cp.Title}", $"Answer the checkpoint question in writing; it {when}.", "Open checkpoint", $"/plan/checkpoints#cp-{cp.Id}"));
        }

        // Schedule phase change (weeks 4 and 9).
        if (day.Week is 4 or 9 && day.Day > 0 && day.Day - (PlanCalendar.DaysForWeek(day.Week.Value, snap.Phases)?.DayFrom ?? day.Day) < 3)
        {
            list.Add(new(Keys.SchedulePhase, $"Schedule Phase {day.SchedulePhase} starts", "Run the same phase for at least two weeks before changing it.", "Open schedule", "/plan/schedule"));
        }

        return list;
    }
}
