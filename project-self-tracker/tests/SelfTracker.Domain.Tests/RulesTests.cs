using SelfTracker.Domain.Common;
using SelfTracker.Domain.Services;

namespace SelfTracker.Domain.Tests;

public class TravelCalendarTests
{
    private static readonly DateOnly Anchor = new(2026, 11, 20); // a Friday

    [Fact]
    public void BR04_TripsRepeatEvery14DaysFridayToMonday()
    {
        var t = new TravelCalendar(true, Anchor);
        Assert.Equal(TravelRole.PreplannedMvd, t.RoleFor(Anchor));
        Assert.Equal(TravelRole.TimedAttempt, t.RoleFor(Anchor.AddDays(1)));
        Assert.Equal(TravelRole.Review, t.RoleFor(Anchor.AddDays(2)));
        Assert.Equal(TravelRole.ShortReview, t.RoleFor(Anchor.AddDays(3)));
        Assert.Equal(TravelRole.None, t.RoleFor(Anchor.AddDays(4)));
        Assert.Equal(TravelRole.None, t.RoleFor(Anchor.AddDays(7)));
        Assert.Equal(TravelRole.PreplannedMvd, t.RoleFor(Anchor.AddDays(14)));
    }

    [Fact]
    public void BR04_AnchorInThePastStillProjectsForward()
    {
        var t = new TravelCalendar(true, Anchor.AddDays(-14 * 10));
        Assert.True(t.IsTravelDay(Anchor));
        Assert.False(t.IsTravelDay(Anchor.AddDays(7)));
    }

    [Fact]
    public void BR04_DatesBeforeAnchorFollowThePattern()
    {
        var t = new TravelCalendar(true, Anchor);
        Assert.True(t.IsTravelDay(Anchor.AddDays(-14)));
        Assert.False(t.IsTravelDay(Anchor.AddDays(-7)));
    }

    [Fact]
    public void BR04_OverridesAddAndCancelTrips()
    {
        var overrides = new Dictionary<DateOnly, bool> { [Anchor] = false, [Anchor.AddDays(7)] = true };
        var t = new TravelCalendar(true, Anchor, overrides);
        Assert.False(t.IsTravelDay(Anchor));
        Assert.True(t.IsTravelDay(Anchor.AddDays(8)));
    }

    [Fact]
    public void BR04_DisabledHasNoTrips() =>
        Assert.False(new TravelCalendar(false, Anchor).IsTravelDay(Anchor));
}

public class DayStateTests
{
    private static readonly DateOnly Start = new(2026, 11, 1);
    private static readonly DateOnly Today = new(2026, 11, 20);

    [Fact]
    public void BR05_StatesForLoggedMissedPausedFuture()
    {
        var cal = new PlanCalendar(Start, [(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 11))]);
        Assert.Equal(DayState.Full, DayStates.For(new(2026, 11, 2), Today, cal, true, DayType.Full));
        Assert.Equal(DayState.Mvd, DayStates.For(new(2026, 11, 3), Today, cal, true, DayType.Mvd));
        Assert.Equal(DayState.Rest, DayStates.For(new(2026, 11, 4), Today, cal, true, DayType.Rest));
        Assert.Equal(DayState.Missed, DayStates.For(new(2026, 11, 5), Today, cal, false, null));
        Assert.Equal(DayState.Paused, DayStates.For(new(2026, 11, 10), Today, cal, false, null));
        Assert.Equal(DayState.Future, DayStates.For(Today.AddDays(1), Today, cal, false, null));
        Assert.Equal(DayState.Today, DayStates.For(Today, Today, cal, false, null));
        Assert.Equal(DayState.Unclassified, DayStates.For(new(2026, 11, 6), Today, cal, true, null));
        Assert.Equal(DayState.NotStarted, DayStates.For(new(2026, 10, 30), Today, cal, false, null));
    }

    [Fact]
    public void BR05_RestIsNotAMissAndNotActive()
    {
        Assert.False(DayStates.IsActive(DayState.Rest));
        Assert.True(DayStates.IsActive(DayState.Mvd));
    }

    [Fact]
    public void BR05_ActiveDaysInLast30ExcludesPausedDates()
    {
        var cal = new PlanCalendar(new DateOnly(2026, 10, 1), [(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 3))]);
        var logs = new Dictionary<DateOnly, DayType?>();
        for (var d = Today.AddDays(-29); d <= Today; d = d.AddDays(1))
        {
            logs[d] = d.Day % 5 == 0 ? DayType.Mvd : DayType.Full;
        }

        logs[new DateOnly(2026, 11, 18)] = DayType.Rest;
        logs[new DateOnly(2026, 11, 19)] = null; // in progress
        var w = DayStates.ActiveInLast30(Today, cal, logs);
        Assert.Equal(3, w.PausedDays);
        Assert.Equal(25, w.ActiveDays);
        Assert.Equal(26, w.Target);
        Assert.True(w.MvdDays > 0);
    }

    [Fact]
    public void BR05_NeverMissedTwiceTracksConsecutiveMisses()
    {
        var cal = new PlanCalendar(Start);
        var logs = new Dictionary<DateOnly, DayType?>();
        for (var d = Start; d < Today; d = d.AddDays(1))
        {
            logs[d] = DayType.Full;
        }

        logs.Remove(new DateOnly(2026, 11, 5));
        var once = DayStates.MissStatus(Today, cal, logs, null);
        Assert.True(once.NeverMissedTwice);

        logs.Remove(new DateOnly(2026, 11, 18));
        logs.Remove(new DateOnly(2026, 11, 19));
        var twice = DayStates.MissStatus(Today, cal, logs, null);
        Assert.False(twice.NeverMissedTwice);
        Assert.Equal(2, twice.ConsecutiveMissedEndingYesterday);
    }

    [Fact]
    public void BR05_PauseResetsNeverMissedTwice()
    {
        var cal = new PlanCalendar(Start, [(new DateOnly(2026, 11, 8), new DateOnly(2026, 11, 9))]);
        var logs = new Dictionary<DateOnly, DayType?>();
        for (var d = new DateOnly(2026, 11, 10); d < Today; d = d.AddDays(1))
        {
            logs[d] = DayType.Mvd;
        }

        // Nov 1-7 missed, but they are before the last pause ended.
        Assert.True(DayStates.MissStatus(Today, cal, logs, new DateOnly(2026, 11, 9)).NeverMissedTwice);
    }
}

public class TickRulesTests
{
    [Fact]
    public void BR06_DsaTickCannotBeSetByHand() =>
        Assert.Contains(TickRules.DsaDerivedMessage, TickRules.Validate(new(TickType.Dsa, "x", null, null, null, 0)));

    [Fact]
    public void BR06_DsaTickDerivesFromCountedAttemptsOrReviews()
    {
        Assert.False(TickRules.DsaTick(0, 0));
        Assert.True(TickRules.DsaTick(1, 0));
        Assert.True(TickRules.DsaTick(0, 1));
    }

    [Fact]
    public void BR06_CoreNeedsNoteAndArtifact()
    {
        Assert.NotEmpty(TickRules.Validate(new(TickType.Core, null, "https://x.dev", null, null, 0)));
        Assert.NotEmpty(TickRules.Validate(new(TickType.Core, "read ch 2", null, null, null, 0)));
        Assert.Empty(TickRules.Validate(new(TickType.Core, "one\ntwo\nthree", null, null, null, 0)));
        Assert.Empty(TickRules.Validate(new(TickType.Core, "note", null, null, null, 1)));
        Assert.Empty(TickRules.Validate(new(TickType.Core, "note", "https://notes.example/x", null, null, 0)));
        Assert.Empty(TickRules.Validate(new(TickType.Core, "note", null, "DdiaNote", 4, 0)));
    }

    [Fact]
    public void BR06_BuildNeedsCommitPrLinkOrAttachment()
    {
        Assert.NotEmpty(TickRules.Validate(new(TickType.Build, "one\ntwo\nthree", null, null, null, 0)));
        Assert.Empty(TickRules.Validate(new(TickType.Build, "fixed test", "a1b2c3d", null, null, 0)));
        Assert.Empty(TickRules.Validate(new(TickType.Build, "PR", "https://github.com/me/repo/pull/1", null, null, 0)));
        Assert.Empty(TickRules.Validate(new(TickType.Build, "session", null, "FlagshipSession", 3, 0)));
        Assert.NotEmpty(TickRules.Validate(new(TickType.Build, "note", null, "DdiaNote", 3, 0)));
    }

    [Theory]
    [InlineData("abc", false)]
    [InlineData("a1b2c3d", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("ftp://x", false)]
    [InlineData("https://example.com", true)]
    public void BR06_EvidenceUrlMustBeHttpOrHash(string value, bool ok) => Assert.Equal(ok, TickRules.IsUrlOrCommitHash(value));
}

public class MvdRulesTests
{
    [Fact]
    public void BR07_FullMvdNeedsAllThreeParts()
    {
        var r = MvdRules.Evaluate(new(false, 20, false, 15, "a\nb\nc", 10, false, null));
        Assert.True(r.Satisfied);
        Assert.False(MvdRules.Evaluate(new(false, 19, false, 15, "a\nb\nc", 10, false, null)).Satisfied);
        Assert.False(MvdRules.Evaluate(new(false, 20, false, 15, "a\nb", 10, false, null)).Satisfied);
        Assert.True(MvdRules.Evaluate(new(false, 20, false, 15, "a\nb\nc", 0, true, null)).Satisfied);
    }

    [Fact]
    public void BR07_TravelDayNeedsOnlyDsaReviewAndShortNote()
    {
        Assert.True(MvdRules.Evaluate(new(true, 0, true, 0, null, 0, false, "on the bus: reread notes")).Satisfied);
        Assert.False(MvdRules.Evaluate(new(true, 0, true, 0, null, 0, false, null)).Satisfied);
        Assert.False(MvdRules.Evaluate(new(false, 0, true, 0, null, 0, false, "note")).Satisfied);
    }
}

public class FileRulesTests
{
    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("C:\\Users\\me\\secret.pdf", "secret.pdf")]
    [InlineData("na\u0001me.txt", "name.txt")]
    [InlineData("", "file")]
    [InlineData("...", "file")]
    public void SanitizeStripsPathsAndControlCharacters(string input, string expected) =>
        Assert.Equal(expected, FileRules.SanitizeName(input));

    [Fact]
    public void SanitizeCapsLengthKeepingExtension()
    {
        var name = FileRules.SanitizeName(new string('a', 400) + ".mp4");
        Assert.Equal(FileRules.MaxNameLength, name.Length);
        Assert.EndsWith(".mp4", name, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("x.EXE", true)]
    [InlineData("x.ps1", true)]
    [InlineData("x.pdf", false)]
    public void DangerousExtensionsAreBlocked(string name, bool blocked) =>
        Assert.Equal(blocked, FileRules.IsBlocked(FileRules.SafeExtension(name)));

    [Fact]
    public void SniffIgnoresTheExtensionForBinaryContent()
    {
        Assert.Equal("application/pdf", FileRules.Sniff("%PDF-1.7 rest"u8, ".png"));
        Assert.Equal("image/png", FileRules.Sniff(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A }, ".txt"));
        Assert.Equal("video/mp4", FileRules.Sniff(new byte[] { 0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m' }, ".bin"));
        Assert.Equal("application/octet-stream", FileRules.Sniff(new byte[] { 0, 1, 2, 3, 0 }, ".pdf"));
    }

    [Fact]
    public void SvgAndHtmlAreNeverInline()
    {
        Assert.Equal("image/svg+xml", FileRules.Sniff("<svg></svg>"u8, ".svg"));
        Assert.False(FileRules.InlineAllowed("image/svg+xml"));
        Assert.False(FileRules.InlineAllowed("text/html"));
        Assert.True(FileRules.InlineAllowed("video/mp4"));
        Assert.True(FileRules.InlineAllowed("text/markdown"));
    }
}
