using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;
using SelfTracker.Domain.Services;

namespace SelfTracker.Domain.Tests;

public class DdiaRulesTests
{
    [Theory]
    [InlineData(DdiaEdition.Unconfirmed, DdiaEdition.Second)]
    [InlineData(DdiaEdition.Second, DdiaEdition.Second)]
    [InlineData(DdiaEdition.First, DdiaEdition.First)]
    public void DisplayEditionDefaultsToSecondUntilConfirmed(DdiaEdition settings, DdiaEdition expected) =>
        Assert.Equal(expected, DdiaRules.DisplayEdition(settings));

    [Theory]
    [InlineData(DdiaEdition.Unconfirmed, 1, false)]
    [InlineData(DdiaEdition.Unconfirmed, 2, true)]
    [InlineData(DdiaEdition.Unconfirmed, 30, true)]
    [InlineData(DdiaEdition.Unconfirmed, null, false)]
    [InlineData(DdiaEdition.First, 5, false)]
    public void EditionWarningFromWeekTwoWhenUnconfirmed(DdiaEdition edition, int? week, bool show) =>
        Assert.Equal(show, DdiaRules.ShowEditionWarning(edition, week));

    [Fact]
    public void ReadingStartsAChapterButNeverDowngrades()
    {
        Assert.Equal(ChapterStatus.Reading, DdiaRules.AfterReading(ChapterStatus.NotStarted));
        Assert.Equal(ChapterStatus.NoteDone, DdiaRules.AfterReading(ChapterStatus.NoteDone));
    }

    private static NoteContent Full(int ideas = 5, int answered = 3, string? tradeOffs = "A vs B") => new(
        Enumerable.Range(1, 5).Select(i => i <= ideas ? $"idea {i}" : null).ToList(),
        tradeOffs,
        "replica lag shows stale reads",
        "Read-your-writes for order status",
        Enumerable.Range(1, 3).Select(i => ($"Q{i}", i <= answered ? (string?)"answer" : null)).ToList());

    [Fact]
    public void CompleteNoteFollowsTemplateC()
    {
        Assert.Empty(DdiaRules.MissingFromNote(Full()));
        var missing = DdiaRules.MissingFromNote(Full(ideas: 3, answered: 1, tradeOffs: null));
        Assert.Contains(missing, m => m.StartsWith("2 more key idea", StringComparison.Ordinal));
        Assert.Contains(missing, m => m.StartsWith("trade-offs", StringComparison.Ordinal));
        Assert.Contains(missing, m => m.StartsWith("2 more interview", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ChapterStatus.Reading, false, null, ChapterStatus.Reading)]
    [InlineData(ChapterStatus.Reading, true, null, ChapterStatus.NoteDone)]
    [InlineData(ChapterStatus.NotStarted, true, 4, ChapterStatus.Explained)]
    [InlineData(ChapterStatus.Explained, true, null, ChapterStatus.Explained)]
    public void ChapterStatusFromNote(ChapterStatus current, bool complete, int? rating, ChapterStatus expected) =>
        Assert.Equal(expected, DdiaRules.AfterNote(current, complete, rating));

    [Fact]
    public void FrameworkIs45MinutesInSixParts()
    {
        Assert.Equal(6, DdiaRules.DrillSegments.Count);
        Assert.Equal(45, DdiaRules.DrillSegments.Sum(s => s.Minutes));
        Assert.Equal([5, 5, 8, 10, 10, 7], DdiaRules.DrillSegments.Select(s => s.Minutes));
    }

    [Fact]
    public void WrittenDrillNeedsEverySegment()
    {
        Assert.Empty(DdiaRules.MissingForWritten(["a", "b", "c", "d", "e", "f"]));
        Assert.Equal(["High-level design", "Bottlenecks, failure modes and trade-offs"], DdiaRules.MissingForWritten(["a", "b", "c", " ", "e", null]));
    }

    [Theory]
    [InlineData(1, 12)]
    [InlineData(8, 19)]
    [InlineData(24, 35)]
    public void DrillWeeks(int number, int week) => Assert.Equal(week, DdiaRules.PlannedWeekForDrill(number));
}
