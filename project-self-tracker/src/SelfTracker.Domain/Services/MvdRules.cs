namespace SelfTracker.Domain.Services;

public sealed record MvdInput(
    bool IsTravelDay,
    int DsaMinutes,
    bool HasDsaReview,
    int CoreMinutes,
    string? CoreNote,
    int BuildMinutes,
    bool HasBuildEvidence,
    string? AnyWrittenNote);

public sealed record MvdResult(bool DsaDone, bool CoreDone, bool BuildDone, bool TravelRuleApplied, bool Satisfied);

/// <summary>
/// BR-07: MVD indicator (never a gate). DSA attempt or cold review 20-30 min, 15-20 min DDIA/core with a
/// three-line note, 10-15 min build or one tiny commit/test. Travel day: DSA review plus a short written note.
/// A 20-24 minute attempt satisfies this indicator but not the DSA tick (BR-08, 25+ minutes).
/// </summary>
public static class MvdRules
{
    public const int DsaMinutes = 20;
    public const int CoreMinutes = 15;
    public const int BuildMinutes = 10;
    public const int CoreNoteLines = 3;

    public static MvdResult Evaluate(MvdInput input)
    {
        var dsa = input.HasDsaReview || input.DsaMinutes >= DsaMinutes;
        var core = input.CoreMinutes >= CoreMinutes && TickRules.NonEmptyLines(input.CoreNote) >= CoreNoteLines;
        var build = input.BuildMinutes >= BuildMinutes || input.HasBuildEvidence;

        if (input.IsTravelDay)
        {
            var hasNote = !string.IsNullOrWhiteSpace(input.AnyWrittenNote);
            var travelOk = dsa && hasNote;
            return new(dsa, core || hasNote, build, true, travelOk || (dsa && core && build));
        }

        return new(dsa, core, build, false, dsa && core && build);
    }
}
