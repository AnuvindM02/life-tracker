using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;

namespace SelfTracker.Domain.Services;

public sealed record DrillSegment(string Key, string Label, int Minutes);

public sealed record NoteContent(
    IReadOnlyList<string?> KeyIdeas,
    string? TradeOffs,
    string? FailureModes,
    string? AskOpsDecision,
    IReadOnlyList<(string Question, string? Answer)> Questions);

/// <summary>DDIA edition handling, chapter progress and the design-drill framework (spec 4.5, v1.1).</summary>
public static class DdiaRules
{
    public const int PagesPerDayMin = 7;
    public const int PagesPerDayMax = 10;
    public const int KeyIdeas = 5;
    public const int InterviewQuestions = 3;

    public const string EditionWarning = "STOP BEFORE WEEK 2: check the title/copyright page and ISBN of your copy.";

    /// <summary>45-minute framework (Appendix H).</summary>
    public static readonly IReadOnlyList<DrillSegment> DrillSegments =
    [
        new("clarify", "Clarify requirements / scale", 5),
        new("estimate", "Estimate load / storage", 5),
        new("apiData", "API and data model", 8),
        new("highLevel", "High-level design", 10),
        new("deepDive", "Two hardest deep dives", 10),
        new("tradeOffs", "Bottlenecks, failure modes and trade-offs", 7),
    ];

    /// <summary>The chapter list to show: the confirmed edition, or the 2nd edition (labelled unconfirmed) until confirmed.</summary>
    public static DdiaEdition DisplayEdition(DdiaEdition settingsEdition) =>
        settingsEdition == DdiaEdition.Unconfirmed ? DdiaEdition.Second : settingsEdition;

    /// <summary>Banner rule: unconfirmed edition from plan Week 2 onward.</summary>
    public static bool ShowEditionWarning(DdiaEdition settingsEdition, int? planWeek) =>
        settingsEdition == DdiaEdition.Unconfirmed && planWeek is >= 2;

    public static bool CoversWeek(int weekFrom, int weekTo, int week) => week >= weekFrom && week <= weekTo;

    /// <summary>A chapter moves to Reading on its first reading-log entry; later states are never downgraded.</summary>
    public static ChapterStatus AfterReading(ChapterStatus current) => current == ChapterStatus.NotStarted ? ChapterStatus.Reading : current;

    /// <summary>What a note still needs before it is complete (Template C). Empty means complete.</summary>
    public static IReadOnlyList<string> MissingFromNote(NoteContent note)
    {
        var missing = new List<string>();
        var ideas = note.KeyIdeas.Count(i => !string.IsNullOrWhiteSpace(i));
        if (ideas < KeyIdeas)
        {
            missing.Add($"{KeyIdeas - ideas} more key idea(s) in your own words");
        }

        if (string.IsNullOrWhiteSpace(note.TradeOffs))
        {
            missing.Add("trade-offs (option A vs B, what you gain and pay, when each wins)");
        }

        if (string.IsNullOrWhiteSpace(note.FailureModes))
        {
            missing.Add("failure modes");
        }

        if (string.IsNullOrWhiteSpace(note.AskOpsDecision))
        {
            missing.Add("one concrete \"In Ask-Ops I will...\" decision");
        }

        var answered = note.Questions.Count(q => !string.IsNullOrWhiteSpace(q.Question) && !string.IsNullOrWhiteSpace(q.Answer));
        if (answered < InterviewQuestions)
        {
            missing.Add($"{InterviewQuestions - answered} more interview question(s) with a 60-second answer");
        }

        return missing;
    }

    /// <summary>Chapter status from its note: complete note -> NoteDone; plus a 90-second explanation rating -> Explained.</summary>
    public static ChapterStatus AfterNote(ChapterStatus current, bool noteComplete, int? explanationRating)
    {
        if (!noteComplete)
        {
            return current;
        }

        var target = explanationRating is >= 1 ? ChapterStatus.Explained : ChapterStatus.NoteDone;
        return target > current ? target : current;
    }

    /// <summary>"Written" means the full 45-minute framework was written out (drills 1-8 in full).</summary>
    public static IReadOnlyList<string> MissingForWritten(IReadOnlyList<string?> segmentNotes) =>
        DrillSegments.Where((s, i) => i >= segmentNotes.Count || string.IsNullOrWhiteSpace(segmentNotes[i])).Select(s => s.Label).ToList();

    /// <summary>Appendix H: drill n is planned for week 11 + n (drill 1 in W12, near Day 80; drill 24 in W35).</summary>
    public static int PlannedWeekForDrill(int number) => 11 + number;
}
