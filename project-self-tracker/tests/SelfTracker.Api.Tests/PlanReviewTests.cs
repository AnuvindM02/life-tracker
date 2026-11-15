using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SelfTracker.Api.Tests;

[Collection("api")]
public sealed partial class PlanReviewTests(SqlServerContainer sql) : IAsyncLifetime
{
    private readonly TrackerFactory _factory = new(sql);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    /// <summary>Set up so that today is the given plan day.</summary>
    private async Task<HttpClient> AtDay(int planDay, string edition = "Second")
    {
        var api = _factory.Api();
        await api.PostAsJsonAsync("/api/v1/settings/setup", new
        {
            planStartDate = Json.Today.AddDays(-(planDay - 1)),
            ddiaEdition = edition,
            travelEnabled = false,
            fileRoot = _factory.FileRoot,
            timeZone = "UTC",
        }, Json.Options).OkAsync();
        return api;
    }

    private static async Task<List<string>> NudgeKeys(HttpClient api, DateOnly? date = null) =>
        (await api.GetAsync($"/api/v1/views/nudges{(date is { } d ? $"?date={d:yyyy-MM-dd}" : "")}").OkAsync())
        .EnumerateArray().Select(n => n.GetProperty("key").GetString()!).ToList();

    private static async Task<JsonElement> Task(HttpClient api, int week, string kind)
    {
        var tasks = await api.GetAsync($"/api/v1/plan/tasks?week={week}&pageSize=50").OkAsync();
        return tasks.GetProperty("items").EnumerateArray().First(t => t.GetProperty("kind").GetString() == kind);
    }

    private static object Attempt(DateOnly date, int problemId, int confidence, string type = "New") => new
    {
        date, problemId, titleFreeText = (string?)null, type, pass1Min = 30, hintLevel = "None", pass2Min = 0, pass3Min = 0, totalMin = (int?)null,
        solvedUnaided = false, pattern = "x", keyInsight = "y", whatIMissed = "z", timeComplexity = (string?)null, spaceComplexity = (string?)null,
        confidence, codeMd = (string?)null, company = (string?)null, mistakeTags = new[] { "off-by-one" }, reviewItemId = (int?)null, rowVersion = (string?)null,
    };

    [Fact]
    public async Task BR18_DsaWeekTaskFollowsProblemProgress()
    {
        var api = await AtDay(5);
        Assert.Equal("NotStarted", (await Task(api, 1, "DerivedDsa")).GetProperty("status").GetString());

        var problems = (await api.GetAsync("/api/v1/dsa/problems?week=1&pageSize=50").OkAsync()).GetProperty("items").EnumerateArray().ToList();
        await api.PostAsJsonAsync("/api/v1/dsa/attempts", Attempt(Json.Today, problems[0].GetProperty("id").GetInt32(), 3), Json.Options).OkAsync();
        Assert.Equal("InProgress", (await Task(api, 1, "DerivedDsa")).GetProperty("status").GetString());

        foreach (var p in problems.Skip(1))
        {
            var body = JsonSerializer.SerializeToNode(new
            {
                topicId = p.GetProperty("topicId").GetInt32(), title = p.GetProperty("title").GetString(), url = (string?)null, difficulty = (string?)null,
                planWeek = 1, sortOrder = p.GetProperty("sortOrder").GetInt32(), status = "Deferred", rowVersion = p.GetProperty("rowVersion").GetString(),
            }, Json.Options);
            await api.PutAsJsonAsync($"/api/v1/dsa/problems/{p.GetProperty("id").GetInt32()}", body, Json.Options).OkAsync();
        }

        var done = await Task(api, 1, "DerivedDsa");
        Assert.Equal("Done", done.GetProperty("status").GetString());
        var filtered = await api.GetAsync("/api/v1/plan/tasks?status=Done&pageSize=50").OkAsync();
        Assert.Contains(filtered.GetProperty("items").EnumerateArray(), t => t.GetProperty("id").GetInt32() == done.GetProperty("id").GetInt32());

        // Manual Skipped overrides; any other value clears the override.
        var id = done.GetProperty("id").GetInt32();
        var skipped = await api.PatchAsJsonAsync($"/api/v1/plan/tasks/{id}/status", new { status = "Skipped", rowVersion = done.GetProperty("rowVersion").GetString() }, Json.Options).OkAsync();
        Assert.Equal("Skipped", skipped.GetProperty("status").GetString());
        var cleared = await api.PatchAsJsonAsync($"/api/v1/plan/tasks/{id}/status", new { status = "NotStarted", rowVersion = skipped.GetProperty("rowVersion").GetString() }, Json.Options).OkAsync();
        Assert.Equal("Done", cleared.GetProperty("status").GetString());
    }

    [Fact]
    public async Task BR18_DdiaWeekTaskFollowsChapterNote()
    {
        var api = await AtDay(10);
        var chapters = await api.GetAsync("/api/v1/ddia/chapters?edition=Second&pageSize=50").OkAsync();
        var ch2 = chapters.GetProperty("items").EnumerateArray().First(c => c.GetProperty("number").GetInt32() == 2).GetProperty("id").GetInt32();
        await api.PostAsJsonAsync("/api/v1/ddia/reading-log", new { date = Json.Today, chapterId = ch2, pagesRead = 8, minutes = 20 }, Json.Options).OkAsync();
        Assert.Equal("InProgress", (await Task(api, 2, "DerivedDdia")).GetProperty("status").GetString());
        await api.PutAsJsonAsync($"/api/v1/ddia/notes/by-chapter/{ch2}", new
        {
            keyIdea1 = "a", keyIdea2 = "b", keyIdea3 = "c", keyIdea4 = "d", keyIdea5 = "e", tradeOffsMd = "t", failureModesMd = "f", askOpsDecision = "x",
            explanationRating = (int?)null, explanationNotes = (string?)null,
            questions = new[] { new { question = "q1", answer60sMd = "a1" }, new { question = "q2", answer60sMd = "a2" }, new { question = "q3", answer60sMd = "a3" } },
            rowVersion = (string?)null,
        }, Json.Options).OkAsync();
        Assert.Equal("Done", (await Task(api, 2, "DerivedDdia")).GetProperty("status").GetString());
    }

    [Fact]
    public async Task ManualTaskCrudAndCompletedDate()
    {
        var api = await AtDay(5);
        var created = await api.PostAsJsonAsync("/api/v1/plan/tasks", new
        {
            phaseNumber = 1, track = "Other", title = "Set up a DSA repo template", description = (string?)null, outputRequired = "repo", fallback = (string?)null,
            dayFrom = 5, dayTo = 9, status = "NotStarted", notesMd = (string?)null, evidenceUrl = (string?)null, sortOrder = 50, rowVersion = (string?)null,
        }, Json.Options).OkAsync();
        Assert.Equal("Manual", created.GetProperty("kind").GetString());
        var done = await api.PatchAsJsonAsync($"/api/v1/plan/tasks/{created.GetProperty("id").GetInt32()}/status",
            new { status = "Done", rowVersion = created.GetProperty("rowVersion").GetString() }, Json.Options).OkAsync();
        Assert.Equal(Json.Today, DateOnly.Parse(done.GetProperty("completedOn").GetString()!));
        Assert.Equal(HttpStatusCode.NoContent, (await api.DeleteAsync($"/api/v1/plan/tasks/{created.GetProperty("id").GetInt32()}")).StatusCode);
    }

    [Fact]
    public async Task BR11_CheckpointDueStateContextAndAnswer()
    {
        var api = await AtDay(28);
        var cps = await api.GetAsync("/api/v1/plan/checkpoints").OkAsync();
        var day30 = cps.EnumerateArray().First(c => c.GetProperty("planDay").GetInt32() == 30);
        Assert.Equal("Soon", day30.GetProperty("state").GetString());
        Assert.Equal(Json.Today.AddDays(2), DateOnly.Parse(day30.GetProperty("dueDate").GetString()!));
        Assert.Contains("aim 26", day30.GetProperty("context")[0].GetString(), StringComparison.Ordinal);
        Assert.Contains(NudgeService_Checkpoint, await NudgeKeys(api));

        var canary = cps.EnumerateArray().First(c => c.GetProperty("planDay").GetInt32() == 195);
        Assert.NotEqual(JsonValueKind.Null, canary.GetProperty("windowEnd").ValueKind);

        var answered = await api.PutAsJsonAsync($"/api/v1/plan/checkpoints/{day30.GetProperty("id").GetInt32()}",
            new { answerMd = "24 active days; the gym-first evening works.", outcome = "OnTrack", rowVersion = day30.GetProperty("rowVersion").GetString() }, Json.Options).OkAsync();
        Assert.Equal("Answered", answered.GetProperty("state").GetString());
        Assert.DoesNotContain(NudgeService_Checkpoint, await NudgeKeys(api));
    }

    private const string NudgeService_Checkpoint = "checkpoint";

    [Fact]
    public async Task BR11_OverdueCheckpointAndExitCriteria()
    {
        var api = await AtDay(32);
        var cps = await api.GetAsync("/api/v1/plan/checkpoints").OkAsync();
        Assert.Equal("Overdue", cps.EnumerateArray().First(c => c.GetProperty("planDay").GetInt32() == 30).GetProperty("state").GetString());

        var exits = await api.GetAsync("/api/v1/plan/exit-criteria?phase=1").OkAsync();
        Assert.Equal(5, exits.GetArrayLength());
        var first = exits[0];
        var done = await api.PutAsJsonAsync($"/api/v1/plan/exit-criteria/{first.GetProperty("id").GetInt32()}", new
        {
            phaseNumber = 1, group = (string?)null, text = first.GetProperty("text").GetString(), evidenceHint = (string?)null, done = true,
            evidenceText = "72 counted attempts", sortOrder = 0, rowVersion = first.GetProperty("rowVersion").GetString(),
        }, Json.Options).OkAsync();
        Assert.Equal(Json.Today, DateOnly.Parse(done.GetProperty("doneOn").GetString()!));
        var d270 = await api.GetAsync("/api/v1/plan/exit-criteria?phase=3").OkAsync();
        Assert.Equal(3, d270.EnumerateArray().Count(e => e.GetProperty("group").GetString() == "Day 270 readiness"));
    }

    [Fact]
    public async Task ReplanNeedsEvidence()
    {
        var api = await AtDay(40);
        var bad = await api.PostAsJsonAsync("/api/v1/plan/replans", new { date = Json.Today, trigger = "Phase1Behind", evidenceMd = "", decisionMd = "cut React" }, Json.Options);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
        await api.PostAsJsonAsync("/api/v1/plan/replans", new { date = Json.Today, trigger = "Phase1Behind", evidenceMd = "W3 tasks open on day 40", decisionMd = "Defer React polish" }, Json.Options).OkAsync();
    }

    [Fact]
    public async Task WeeklyReviewPrefillMatchesLoggedData()
    {
        var api = await AtDay(30);
        var monday = Monday(Json.Today);
        var dates = Enumerable.Range(0, 7).Select(i => monday.AddDays(i)).Where(d => d <= Json.Today).ToList();
        await api.PostAsJsonAsync("/api/v1/daily-logs", new { date = dates[0], log = new { dayType = "Full", sleepHours = 7.5, energy = 4, gymStatus = "Done" } }, Json.Options).OkAsync();
        if (dates.Count > 1)
        {
            await api.PostAsJsonAsync("/api/v1/daily-logs", new { date = dates[1], log = new { dayType = "Mvd", sleepHours = 6.5, energy = 3, gymStatus = "Skipped" } }, Json.Options).OkAsync();
        }

        var problems = (await api.GetAsync("/api/v1/dsa/problems?week=1&pageSize=50").OkAsync()).GetProperty("items").EnumerateArray().ToList();
        await api.PostAsJsonAsync("/api/v1/dsa/attempts", Attempt(dates[0], problems[0].GetProperty("id").GetInt32(), 2), Json.Options).OkAsync();
        await api.PostAsJsonAsync("/api/v1/dsa/attempts", Attempt(dates[0], problems[1].GetProperty("id").GetInt32(), 3), Json.Options).OkAsync();

        var pre = await api.GetAsync($"/api/v1/reviews/weekly/prefill?weekStart={monday:yyyy-MM-dd}").OkAsync();
        Assert.Equal(dates.Count > 1 ? 2 : 1, pre.GetProperty("activeDays").GetInt32());
        Assert.Equal(dates.Count > 1 ? 1 : 0, pre.GetProperty("mvdDays").GetInt32());
        Assert.Equal(2, pre.GetProperty("dsaAttempts").GetInt32());
        Assert.Equal(1, pre.GetProperty("gymSessions").GetInt32());
        Assert.Equal("off-by-one", pre.GetProperty("repeatedMistakes")[0].GetProperty("tag").GetString());
        Assert.Equal(60, pre.GetProperty("hours").GetProperty("totalMinutes").GetInt32());

        Assert.Equal(HttpStatusCode.NotFound, (await api.GetAsync($"/api/v1/reviews/weekly/{monday:yyyy-MM-dd}")).StatusCode);
        var saved = await api.PutAsJsonAsync($"/api/v1/reviews/weekly/{monday:yyyy-MM-dd}", new
        {
            energy = 4, sleep = 3, mood = 4, shippedMd = "Two Sum, ch. 2 note", blockedMd = (string?)null, nextSliceMd = "3Sum", recoveryNote = (string?)null,
            repeatedRequirement = (string?)null, start = "Morning attempt", stop = "Late scrolling", keep = "Gym at 18:15", rowVersion = (string?)null,
        }, Json.Options).OkAsync();
        Assert.Equal(2, saved.GetProperty("snapshot").GetProperty("dsaAttempts").GetInt32());
        Assert.Single((await api.GetAsync("/api/v1/reviews/weekly").OkAsync()).EnumerateArray());
    }

    [Fact]
    public async Task MonthlyReviewAndSomedayTriage()
    {
        var api = await AtDay(30);
        var idea = await api.PostAsJsonAsync("/api/v1/someday", new { title = "Try .NET Aspire", notesMd = "after Phase 2", category = "Tool", status = "Parked" }, Json.Options).OkAsync();
        var drop = await api.PostAsJsonAsync("/api/v1/someday", new { title = "Another course", notesMd = (string?)null, category = "Course", status = "Parked" }, Json.Options).OkAsync();
        var month = new DateOnly(Json.Today.Year, Json.Today.Month, 1);
        var pre = await api.GetAsync($"/api/v1/reviews/monthly/prefill?month={month:yyyy-MM-dd}").OkAsync();
        Assert.Equal(2, pre.GetProperty("somedayParked").GetArrayLength());
        Assert.Equal(20, pre.GetProperty("postingsTarget").GetInt32());

        var promoted = await api.PostAsync($"/api/v1/someday/{idea.GetProperty("id").GetInt32()}/promote", null).OkAsync();
        Assert.Equal("Promoted", promoted.GetProperty("status").GetString());
        var task = await api.GetAsync($"/api/v1/plan/tasks/{promoted.GetProperty("promotedTaskId").GetInt32()}").OkAsync();
        Assert.Equal("Try .NET Aspire", task.GetProperty("title").GetString());
        var dropped = await api.PostAsJsonAsync($"/api/v1/someday/{drop.GetProperty("id").GetInt32()}/triage", new { action = "Drop" }, Json.Options).OkAsync();
        Assert.Equal("Dropped", dropped.GetProperty("status").GetString());

        await api.PutAsJsonAsync($"/api/v1/reviews/monthly/{month:yyyy-MM-dd}", new
        {
            phaseProgressMd = "On track", mistakeThemesMd = "off-by-one", postingsCaptured = 0, lighterWeekNeeded = false, somedayTriageMd = "Promoted Aspire", rowVersion = (string?)null,
        }, Json.Options).OkAsync();
        Assert.Equal("On track", (await api.GetAsync($"/api/v1/reviews/monthly/{month:yyyy-MM-dd}").OkAsync()).GetProperty("phaseProgressMd").GetString());
    }

    [Fact]
    public async Task BR12_WeeklyHoursUseGraduatedTargets()
    {
        var api = await AtDay(3);
        await api.PostAsJsonAsync($"/api/v1/daily-logs/by-date/{Json.Today:yyyy-MM-dd}/time-entries", new { category = "Dsa", minutes = 90, source = "Manual" }, Json.Options).OkAsync();
        var hours = await api.GetAsync("/api/v1/views/weekly-hours").OkAsync();
        Assert.Equal("A", hours.GetProperty("stage").GetString());
        var total = Assert.Single(hours.GetProperty("targets").EnumerateArray());
        Assert.True(total.GetProperty("isTotal").GetBoolean());
        Assert.Equal(4, total.GetProperty("minHours").GetDecimal());
        Assert.Equal(1.5m, total.GetProperty("actualHours").GetDecimal());
    }

    [Fact]
    public async Task BR12_PhaseTargetsFromWeek9()
    {
        var api = await AtDay(70);
        var hours = await api.GetAsync("/api/v1/views/weekly-hours").OkAsync();
        Assert.Equal("Phase1", hours.GetProperty("stage").GetString());
        var targets = hours.GetProperty("targets").EnumerateArray().ToList();
        Assert.Contains(targets, t => t.GetProperty("label").GetString() == "DSA" && t.GetProperty("minHours").GetDecimal() == 4);
        Assert.Contains(targets, t => t.GetProperty("isStretch").GetBoolean());
    }

    // ---- BR-13: every nudge in spec 4.12 that M5 owns can be triggered ----

    [Fact]
    public async Task BR13_BehindAndPhase1BehindNudges()
    {
        var api = await AtDay(40);
        var keys = await NudgeKeys(api);
        Assert.Contains("behind-2w", keys);
        Assert.Contains("phase1-behind-3w", keys);
    }

    [Fact]
    public async Task BR13_DsaPlateauNudge()
    {
        var api = await AtDay(60);
        var problems = (await api.GetAsync("/api/v1/dsa/problems?pageSize=50").OkAsync()).GetProperty("items").EnumerateArray().ToList();
        var thisMonday = Monday(Json.Today);
        int[] conf = [3, 3, 2, 2];
        for (var w = 0; w < 4; w++)
        {
            var d = thisMonday.AddDays(-7 * (3 - w));
            await api.PostAsJsonAsync("/api/v1/dsa/attempts", Attempt(d, problems[w].GetProperty("id").GetInt32(), conf[w]), Json.Options).OkAsync();
        }

        Assert.Contains("dsa-plateau", await NudgeKeys(api));
    }

    [Fact]
    public async Task BR13_RecoveryNudge()
    {
        var api = await AtDay(20);
        for (var i = 3; i >= 1; i--)
        {
            await api.PostAsJsonAsync("/api/v1/daily-logs", new { date = Json.Today.AddDays(-i), log = new { dayType = "Mvd", energy = 2 } }, Json.Options).OkAsync();
        }

        Assert.Contains("recovery", await NudgeKeys(api));
    }

    [Fact]
    public async Task BR13_WeeklyReviewNudgeClearsWhenReviewed()
    {
        var api = await AtDay(20);
        Assert.Contains("weekly-review", await NudgeKeys(api));
        var monday = Monday(Json.Today);
        var target = Json.Today.DayOfWeek == DayOfWeek.Sunday ? monday : monday.AddDays(-7);
        await api.PutAsJsonAsync($"/api/v1/reviews/weekly/{target:yyyy-MM-dd}", new { energy = 3, sleep = 3, mood = 3 }, Json.Options).OkAsync();
        Assert.DoesNotContain("weekly-review", await NudgeKeys(api));
    }

    [Fact]
    public async Task BR13_MonthlyReviewNudgeNearMonthEnd()
    {
        var api = await AtDay(40);
        var lastDay = new DateOnly(Json.Today.Year, Json.Today.Month, DateTime.DaysInMonth(Json.Today.Year, Json.Today.Month));
        Assert.Contains("monthly-review", await NudgeKeys(api, lastDay));
    }

    [Fact]
    public async Task BR13_EditionAndSchedulePhaseNudges()
    {
        var api = await AtDay(22, "Unconfirmed");
        var keys = await NudgeKeys(api);
        Assert.Contains("ddia-edition", keys);
        Assert.Contains("schedule-phase", keys);
    }

    [Fact]
    public async Task BR13_MissedDayNudges()
    {
        var api = await AtDay(5);
        await api.PostAsJsonAsync("/api/v1/daily-logs", new { date = Json.Today.AddDays(-4), log = new { dayType = "Full" } }, Json.Options).OkAsync();
        await api.PostAsJsonAsync("/api/v1/daily-logs", new { date = Json.Today.AddDays(-3), log = new { dayType = "Full" } }, Json.Options).OkAsync();
        await api.PostAsJsonAsync("/api/v1/daily-logs", new { date = Json.Today.AddDays(-2), log = new { dayType = "Full" } }, Json.Options).OkAsync();
        Assert.Contains("missed-one", await NudgeKeys(api));
        await api.PostAsJsonAsync("/api/v1/daily-logs", new { date = Json.Today, log = new { dayType = "Mvd" } }, Json.Options).OkAsync();
        Assert.DoesNotContain("missed-one", await NudgeKeys(api));
    }

    [Fact]
    public async Task BR14_OpenApiContractHasNoDebtWording()
    {
        var api = await AtDay(1);
        var openApi = await api.GetStringAsync("/openapi/v1.json");
        Assert.DoesNotMatch(Forbidden(), openApi);
    }

    [GeneratedRegex(@"\bowed\b|catch[- ]?up|make[- ]?up ?hours|\bdebt\b|backlog ?hours|behind ?by ?\d+ ?hours", RegexOptions.IgnoreCase)]
    private static partial Regex Forbidden();

    private static DateOnly Monday(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
}
