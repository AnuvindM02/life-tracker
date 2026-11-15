using SelfTracker.Domain.Entities;
using SelfTracker.Domain.Services;

namespace SelfTracker.Domain.Tests;

public class DsaRulesTests
{
    private static readonly DateOnly Day = new(2026, 11, 16);

    [Theory]
    [InlineData(AttemptType.New, 25, "forgot the complement map", true)]
    [InlineData(AttemptType.Timed, 30, "off by one", true)]
    [InlineData(AttemptType.New, 24, "forgot the complement map", false)]
    [InlineData(AttemptType.New, 30, "", false)]
    [InlineData(AttemptType.New, 30, null, false)]
    public void BR08_NewAndTimedNeedUnaidedMinutesAndMistakeLine(AttemptType type, int pass1, string? missed, bool counted)
    {
        var r = DsaRules.Evaluate(type, pass1, 25, missed, hasProblem: true);
        Assert.Equal(counted, r.IsCounted);
        Assert.Equal(counted, r.Reason is null);
    }

    [Fact]
    public void BR08_ReasonExplainsBothGaps()
    {
        var r = DsaRules.Evaluate(AttemptType.New, 20, 25, " ", true);
        Assert.Contains("20 min", r.Reason, StringComparison.Ordinal);
        Assert.Contains("mistake-log line", r.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void BR08_ColdReviewOfLibraryProblemCountsLinkedOrNot()
    {
        Assert.True(DsaRules.Evaluate(AttemptType.ColdReview, 5, 25, null, hasProblem: true).IsCounted);
        Assert.False(DsaRules.Evaluate(AttemptType.ColdReview, 30, 25, "x", hasProblem: false).IsCounted);
    }

    [Fact]
    public void BR08_UnaidedMinimumIsConfigurable() =>
        Assert.True(DsaRules.Evaluate(AttemptType.New, 20, 20, "missed it", true).IsCounted);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void BR09_LowConfidenceCreatesFourStepLadder(int confidence)
    {
        var ladder = DsaRules.PlanLadder(Day, confidence, DsaRules.DefaultIntervals, 30);
        Assert.Equal([Day.AddDays(1), Day.AddDays(3), Day.AddDays(7), Day.AddDays(21)], ladder.Select(l => l.DueDate));
        Assert.Equal([1, 3, 7, 21], ladder.Select(l => l.IntervalDays));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void BR09_HighConfidenceCreatesOneLaterReview(int confidence)
    {
        var ladder = DsaRules.PlanLadder(Day, confidence, DsaRules.DefaultIntervals, 30);
        Assert.Equal(Day.AddDays(30), Assert.Single(ladder).DueDate);
    }

    [Fact]
    public void BR09_FailedReviewRestartsLadder()
    {
        Assert.Equal(ReviewOutcome.RestartLadder, DsaRules.OnReview(2));
        Assert.Equal(ReviewOutcome.RestartLadder, DsaRules.OnReview(3));
        Assert.Equal(ReviewOutcome.KeepRemaining, DsaRules.OnReview(4));
    }

    [Theory]
    [InlineData("1,3,7,21", new[] { 1, 3, 7, 21 })]
    [InlineData(" 2, 5 ,14", new[] { 2, 5, 14 })]
    [InlineData("7,1,1", new[] { 1, 7 })]
    [InlineData("", new[] { 1, 3, 7, 21 })]
    [InlineData("x,0,-3", new[] { 1, 3, 7, 21 })]
    public void BR09_IntervalsParseSafely(string csv, int[] expected) => Assert.Equal(expected, DsaRules.ParseIntervals(csv));

    [Fact]
    public void BR09_LadderCrossesMonthAndLeapDay()
    {
        var ladder = DsaRules.PlanLadder(new DateOnly(2028, 2, 28), 1, DsaRules.DefaultIntervals, 30);
        Assert.Equal(new DateOnly(2028, 2, 29), ladder[0].DueDate);
        Assert.Equal(new DateOnly(2028, 3, 20), ladder[3].DueDate);
    }

    [Theory]
    [InlineData(1, 90)]
    [InlineData(90, 90)]
    [InlineData(91, 220)]
    [InlineData(220, 220)]
    [InlineData(300, 220)]
    public void BR10_MarkerForPlanDay(int day, int markerDay) => Assert.Equal(markerDay, DsaRules.MarkerFor(day).Day);

    [Fact]
    public void BR10_MarkersAreRangesNotQuotas()
    {
        Assert.Equal((65, 80), (DsaRules.Markers[0].Min, DsaRules.Markers[0].Max));
        Assert.Equal((120, 135), (DsaRules.Markers[1].Min, DsaRules.Markers[1].Max));
    }

    [Theory]
    [InlineData(ProblemStatus.NotStarted, false, ProblemStatus.Attempted)]
    [InlineData(ProblemStatus.NotStarted, true, ProblemStatus.Solved)]
    [InlineData(ProblemStatus.Solved, false, ProblemStatus.Solved)]
    [InlineData(ProblemStatus.Deferred, false, ProblemStatus.Attempted)]
    public void ProblemStatusNeverDowngradesSolved(ProblemStatus current, bool solved, ProblemStatus expected) =>
        Assert.Equal(expected, DsaRules.StatusAfterAttempt(current, solved));

    [Fact]
    public void MedianHandlesEvenOddAndEmpty()
    {
        Assert.Null(DsaRules.Median([]));
        Assert.Equal(30, DsaRules.Median([40, 30, 10]));
        Assert.Equal(25, DsaRules.Median([20, 30]));
    }
}
