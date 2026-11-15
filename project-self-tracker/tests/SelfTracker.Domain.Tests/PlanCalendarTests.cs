using SelfTracker.Domain.Common;
using SelfTracker.Domain.Services;

namespace SelfTracker.Domain.Tests;

public class PlanCalendarTests
{
    private static readonly DateOnly Start = new(2026, 11, 16);

    [Fact]
    public void BR01_PlanDayCountsFromStartInclusive()
    {
        var cal = new PlanCalendar(Start);
        Assert.Equal(1, cal.PlanDay(Start));
        Assert.Equal(2, cal.PlanDay(Start.AddDays(1)));
        Assert.Equal(365, cal.PlanDay(Start.AddDays(364)));
    }

    [Fact]
    public void BR01_BeforeStartIsNotStarted()
    {
        var cal = new PlanCalendar(Start);
        Assert.Equal(0, cal.PlanDay(Start.AddDays(-1)));
        Assert.Equal(PlanDayStatus.NotStarted, cal.Describe(Start.AddDays(-1)).Status);
    }

    [Fact]
    public void BR01_PausedDateKeepsPreviousValueAndIsFlagged()
    {
        var cal = new PlanCalendar(Start, [(Start.AddDays(5), Start.AddDays(7))]);
        Assert.Equal(5, cal.PlanDay(Start.AddDays(4)));
        var paused = cal.Describe(Start.AddDays(6));
        Assert.True(paused.IsPaused);
        Assert.Equal(PlanDayStatus.Paused, paused.Status);
        Assert.Equal(5, paused.Day);
        Assert.Equal(6, cal.PlanDay(Start.AddDays(8)));
    }

    [Fact]
    public void BR01_OverlappingPausesAreNotDoubleCounted()
    {
        var cal = new PlanCalendar(Start, [(Start.AddDays(2), Start.AddDays(6)), (Start.AddDays(4), Start.AddDays(8))]);
        Assert.Equal(3, cal.PlanDay(Start.AddDays(9)));
    }

    [Fact]
    public void BR01_PauseOnStartDateGivesDayZero()
    {
        var cal = new PlanCalendar(Start, [(Start, Start)]);
        Assert.Equal(0, cal.PlanDay(Start));
        Assert.Equal(1, cal.PlanDay(Start.AddDays(1)));
    }

    [Fact]
    public void BR01_Above365IsYear2()
    {
        var cal = new PlanCalendar(Start);
        var info = cal.Describe(Start.AddDays(365));
        Assert.Equal(366, info.Day);
        Assert.Equal(PlanDayStatus.Year2, info.Status);
        Assert.Null(info.Phase);
    }

    [Fact]
    public void BR01_LeapDayCountsAsOneDay()
    {
        var cal = new PlanCalendar(new DateOnly(2028, 2, 27));
        Assert.Equal(3, cal.PlanDay(new DateOnly(2028, 2, 29)));
        Assert.Equal(4, cal.PlanDay(new DateOnly(2028, 3, 1)));
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(7, 1, 1)]
    [InlineData(8, 1, 2)]
    [InlineData(84, 1, 12)]
    [InlineData(85, 1, 13)]
    [InlineData(90, 1, 13)]
    [InlineData(91, 2, 14)]
    [InlineData(97, 2, 14)]
    [InlineData(98, 2, 15)]
    [InlineData(216, 2, 31)]
    [InlineData(217, 2, 31)]
    [InlineData(220, 2, 31)]
    [InlineData(221, 3, 32)]
    [InlineData(241, 3, 34)]
    [InlineData(242, 3, 35)]
    [InlineData(300, 3, 43)]
    [InlineData(301, 4, 44)]
    [InlineData(356, 4, 51)]
    [InlineData(357, 4, 52)]
    [InlineData(365, 4, 52)]
    public void BR02_PhaseRelativeWeeksMatchRoadmapLabels(int day, int phase, int week)
    {
        Assert.Equal(phase, PlanCalendar.PhaseFor(day, PlanCalendar.DefaultPhases)!.Number);
        Assert.Equal(week, PlanCalendar.WeekFor(day, PlanCalendar.DefaultPhases));
    }

    [Theory]
    [InlineData(1, SchedulePhase.A)]
    [InlineData(3, SchedulePhase.A)]
    [InlineData(4, SchedulePhase.B)]
    [InlineData(8, SchedulePhase.B)]
    [InlineData(9, SchedulePhase.C)]
    [InlineData(40, SchedulePhase.C)]
    public void BR02_SchedulePhaseByWeek(int week, SchedulePhase expected) =>
        Assert.Equal(expected, PlanCalendar.ScheduleFor(week));

    [Theory]
    [InlineData(13, 85, 90)]
    [InlineData(14, 91, 97)]
    [InlineData(31, 210, 220)]
    [InlineData(43, 298, 300)]
    [InlineData(52, 357, 365)]
    public void BR02_DaysForWeekFoldPartialWeeksIntoLastWeek(int week, int from, int to) =>
        Assert.Equal((from, to), PlanCalendar.DaysForWeek(week, PlanCalendar.DefaultPhases));

    [Fact]
    public void BR03_DateForDaySkipsPausedDates()
    {
        var cal = new PlanCalendar(Start, [(Start.AddDays(5), Start.AddDays(7))]);
        Assert.Equal(Start.AddDays(4), cal.DateForDay(5));
        Assert.Equal(Start.AddDays(8), cal.DateForDay(6));
        Assert.Equal(Start.AddDays(92), cal.DateForDay(90));
    }

    [Fact]
    public void BR03_DateForDayIsInverseOfPlanDay()
    {
        var cal = new PlanCalendar(Start, [(Start.AddDays(10), Start.AddDays(20)), (Start.AddDays(100), Start.AddDays(101))]);
        foreach (var n in new[] { 1, 10, 11, 30, 90, 220, 365 })
        {
            var date = cal.DateForDay(n);
            Assert.Equal(n, cal.PlanDay(date));
            Assert.False(cal.IsPaused(date));
        }
    }
}
