using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;
using SelfTracker.Domain.Services;

namespace SelfTracker.Domain.Tests;

public class PlanRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 20);

    [Theory]
    [InlineData(10, false, CheckpointState.Upcoming)]
    [InlineData(4, false, CheckpointState.Upcoming)]
    [InlineData(3, false, CheckpointState.Soon)]
    [InlineData(1, false, CheckpointState.Soon)]
    [InlineData(0, false, CheckpointState.DueToday)]
    [InlineData(-1, false, CheckpointState.Overdue)]
    [InlineData(-1, true, CheckpointState.Answered)]
    public void BR11_CheckpointReminderAndOverdue(int daysUntilDue, bool answered, CheckpointState expected) =>
        Assert.Equal(expected, PlanRules.CheckpointStateFor(Today.AddDays(daysUntilDue), Today, answered));

    [Fact]
    public void BR11_DueDateFollowsPauses()
    {
        var start = new DateOnly(2026, 11, 1);
        var cal = new PlanCalendar(start, [(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 14))]);
        Assert.Equal(start.AddDays(29 + 5), cal.DateForDay(30));
    }

    [Fact]
    public void BR13_BehindUsesOldestOpenTaskOfThePhase()
    {
        var tasks = new[]
        {
            new OpenTask(1, 1, 7, PlanTaskStatus.Done),
            new OpenTask(2, 1, 14, PlanTaskStatus.InProgress),
            new OpenTask(3, 1, 21, PlanTaskStatus.Deferred),
            new OpenTask(4, 2, 20, PlanTaskStatus.NotStarted),
        };
        Assert.Null(PlanRules.Behind(tasks, 28, 14, 1)); // 28 - 14 = 14, not more than 14
        Assert.Equal(2, PlanRules.Behind(tasks, 29, 14, 1)!.Id);
        Assert.Null(PlanRules.Behind(tasks, 35, 21, 1));
        Assert.Equal(2, PlanRules.Behind(tasks, 36, 21, 1)!.Id);
        Assert.Equal(4, PlanRules.Behind(tasks, 40, 14, 2)!.Id);
    }

    [Theory]
    [InlineData(new[] { 3.0, 3.0, 2.5, 2.5 }, true)]
    [InlineData(new[] { 3.0, 2.0, 3.0, 2.0 }, false)]
    [InlineData(new[] { 4.0, 4.0, 4.0, 4.0 }, false)]
    [InlineData(new[] { 3.0, 3.0, 3.0 }, false)]
    [InlineData(new[] { 5.0, 3.4, 3.4, 3.2, 3.2 }, true)]
    public void BR13_DsaPlateau(double[] weekly, bool plateau) => Assert.Equal(plateau, PlanRules.DsaPlateau(weekly));

    [Fact]
    public void BR13_RecoverySignal()
    {
        Assert.True(PlanRules.RecoverySignal([6.5m, 6m, 7m, 6.5m, 6m], []));
        Assert.False(PlanRules.RecoverySignal([7m, 7m, 7m, 7m, 7m], []));
        Assert.False(PlanRules.RecoverySignal([5m, 5m, 5m, 5m], []));
        Assert.True(PlanRules.RecoverySignal([], [4, 2, 2, 1]));
        Assert.False(PlanRules.RecoverySignal([], [2, 3, 2]));
    }

    [Fact]
    public void BR18_DsaWeekStatus()
    {
        Assert.Null(PlanRules.DerivedDsaStatus([]));
        Assert.Equal(PlanTaskStatus.NotStarted, PlanRules.DerivedDsaStatus([new(ProblemStatus.NotStarted, false, false)]));
        Assert.Equal(PlanTaskStatus.InProgress, PlanRules.DerivedDsaStatus([new(ProblemStatus.Attempted, true, false), new(ProblemStatus.NotStarted, false, false)]));
        Assert.Equal(PlanTaskStatus.Done, PlanRules.DerivedDsaStatus([new(ProblemStatus.Attempted, true, true), new(ProblemStatus.Deferred, false, false), new(ProblemStatus.Solved, true, false)]));
    }

    [Fact]
    public void BR18_DdiaWeekStatus()
    {
        Assert.Null(PlanRules.DerivedDdiaStatus([]));
        Assert.Equal(PlanTaskStatus.NotStarted, PlanRules.DerivedDdiaStatus([ChapterStatus.NotStarted]));
        Assert.Equal(PlanTaskStatus.InProgress, PlanRules.DerivedDdiaStatus([ChapterStatus.Reading, ChapterStatus.NotStarted]));
        Assert.Equal(PlanTaskStatus.Done, PlanRules.DerivedDdiaStatus([ChapterStatus.NoteDone, ChapterStatus.Explained]));
    }

    [Fact]
    public void BR18_ManualDeferOrSkipWins()
    {
        Assert.Equal(PlanTaskStatus.Deferred, PlanRules.Effective(PlanTaskStatus.NotStarted, PlanTaskStatus.Done, PlanTaskStatus.Deferred));
        Assert.Equal(PlanTaskStatus.Done, PlanRules.Effective(PlanTaskStatus.NotStarted, PlanTaskStatus.Done, null));
        Assert.Equal(PlanTaskStatus.InProgress, PlanRules.Effective(PlanTaskStatus.InProgress, null, null));
    }

    [Theory]
    [InlineData(1, 1, "A")]
    [InlineData(3, 1, "A")]
    [InlineData(4, 1, "B")]
    [InlineData(8, 1, "B")]
    [InlineData(9, 1, "Phase1")]
    [InlineData(20, 2, "Phase2")]
    [InlineData(50, 4, "Phase4")]
    public void BR12_HourStage(int week, int phase, string stage) => Assert.Equal(stage, PlanRules.HourStage(week, phase));

    [Fact]
    public void WeekAndMonthHelpers()
    {
        Assert.Equal(new DateOnly(2026, 11, 16), PlanRules.MondayOf(new DateOnly(2026, 11, 22)));
        Assert.Equal(new DateOnly(2026, 11, 16), PlanRules.MondayOf(new DateOnly(2026, 11, 16)));
        Assert.Equal(new DateOnly(2026, 11, 1), PlanRules.FirstOfMonth(new DateOnly(2026, 11, 20)));
        Assert.Equal(10, PlanRules.DaysLeftInMonth(new DateOnly(2026, 11, 20)));
    }
}
