using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SelfTracker.Api.Tests;

[Collection("api")]
public sealed class DsaTests(SqlServerContainer sql) : IAsyncLifetime
{
    private readonly TrackerFactory _factory = new(sql);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static object Attempt(DateOnly date, int? problemId, int confidence, string type = "New", int pass1 = 28, string? missed = "forgot the edge case",
        int? reviewItemId = null, string? title = null, string[]? tags = null) => new
    {
        date,
        problemId,
        titleFreeText = title,
        type,
        pass1Min = pass1,
        hintLevel = "None",
        pass2Min = 5,
        pass3Min = 10,
        totalMin = (int?)null,
        solvedUnaided = confidence >= 4,
        pattern = "hashing",
        keyInsight = "complement lookup",
        whatIMissed = missed,
        timeComplexity = "O(n)",
        spaceComplexity = "O(n)",
        confidence,
        codeMd = (string?)null,
        company = (string?)null,
        mistakeTags = tags ?? ["off-by-one"],
        reviewItemId,
        rowVersion = (string?)null,
    };

    private static async Task<int> ProblemId(HttpClient api, string title)
    {
        var page = await api.GetAsync($"/api/v1/dsa/problems?q={Uri.EscapeDataString(title)}&pageSize=5").OkAsync();
        return page.GetProperty("items").EnumerateArray().First(p => p.GetProperty("title").GetString() == title).GetProperty("id").GetInt32();
    }

    private static Task<JsonElement> Log(HttpClient api, object body) => api.PostAsJsonAsync("/api/v1/dsa/attempts", body, Json.Options).OkAsync();

    private static async Task<List<JsonElement>> Pending(HttpClient api, int problemId)
    {
        var all = await api.GetAsync($"/api/v1/dsa/reviews?bucket=All&problemId={problemId}").OkAsync();
        return all.EnumerateArray().Where(r => r.GetProperty("status").GetString() == "Pending").ToList();
    }

    [Fact]
    public async Task SeededLibraryHasAppendixCProblems()
    {
        var api = await _factory.SetUpAsync();
        var week1 = await api.GetAsync("/api/v1/dsa/problems?week=1&pageSize=50").OkAsync();
        Assert.Equal(8, week1.GetProperty("total").GetInt32());
        var topics = await api.GetAsync("/api/v1/dsa/topics?pageSize=50").OkAsync();
        Assert.Equal(17, topics.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task BR09_ConfidenceTwoCreatesFourReviewItemsAndDsaTick()
    {
        var api = await _factory.SetUpAsync();
        var day = Json.Today.AddDays(-2);
        var pid = await ProblemId(api, "Two Sum");
        var a = await Log(api, Attempt(day, pid, 2));
        Assert.True(a.GetProperty("isCounted").GetBoolean());
        Assert.Equal(43, a.GetProperty("totalMin").GetInt32());
        var dues = a.GetProperty("reviews").EnumerateArray().Select(r => r.GetProperty("dueDate").GetString()).ToList();
        Assert.Equal([day.AddDays(1), day.AddDays(3), day.AddDays(7), day.AddDays(21)], dues.Select(d => DateOnly.Parse(d!)));

        var log = await api.GetAsync($"/api/v1/daily-logs/by-date/{day:yyyy-MM-dd}").OkAsync();
        Assert.True(log.GetProperty("dsaTick").GetBoolean());
        var derived = Assert.Single(log.GetProperty("timeEntries").EnumerateArray());
        Assert.Equal("Derived", derived.GetProperty("source").GetString());
        Assert.Equal(43, derived.GetProperty("minutes").GetInt32());

        var problem = await api.GetAsync($"/api/v1/dsa/problems/{pid}").OkAsync();
        Assert.Equal("Attempted", problem.GetProperty("status").GetString());
        Assert.Equal(day.AddDays(1), DateOnly.Parse(problem.GetProperty("nextReviewDue").GetString()!));
    }

    [Fact]
    public async Task BR08_ShortAttemptIsSavedNotCountedButStillScheduled()
    {
        var api = await _factory.SetUpAsync();
        var pid = await ProblemId(api, "Valid Anagram");
        var a = await Log(api, Attempt(Json.Today, pid, 1, pass1: 20));
        Assert.False(a.GetProperty("isCounted").GetBoolean());
        Assert.Contains("25+", a.GetProperty("notCountedReason").GetString(), StringComparison.Ordinal);
        Assert.Equal(4, a.GetProperty("reviews").GetArrayLength()); // v1.1: counting affects markers, not memory
        var log = await api.GetAsync($"/api/v1/daily-logs/by-date/{Json.Today:yyyy-MM-dd}").OkAsync();
        Assert.False(log.GetProperty("dsaTick").GetBoolean());
        Assert.True(log.GetProperty("mvd").GetProperty("dsaDone").GetBoolean()); // 20+ minutes satisfies the MVD indicator
    }

    [Fact]
    public async Task BR09_FailedReviewRestartsLadderAndPassedReviewKeepsIt()
    {
        var api = await _factory.SetUpAsync();
        var start = Json.Today.AddDays(-10);
        var pid = await ProblemId(api, "Group Anagrams");
        await Log(api, Attempt(start, pid, 2));
        var first = (await Pending(api, pid)).OrderBy(r => r.GetProperty("dueDate").GetString()).First();

        // Pass the +1 review with confidence 4: remaining +3/+7/+21 stay.
        var pass = await Log(api, Attempt(start.AddDays(1), pid, 4, "ColdReview", pass1: 10, missed: null, reviewItemId: first.GetProperty("id").GetInt32()));
        Assert.True(pass.GetProperty("isCounted").GetBoolean());
        Assert.Equal(3, (await Pending(api, pid)).Count);

        // Fail the next review (unlinked cold review completes the earliest pending): ladder restarts at +1 from the review date.
        var reviewDay = start.AddDays(3);
        await Log(api, Attempt(reviewDay, pid, 2, "ColdReview", pass1: 10, missed: "forgot sorting the key"));
        var pending = await Pending(api, pid);
        Assert.Equal([reviewDay.AddDays(1), reviewDay.AddDays(3), reviewDay.AddDays(7), reviewDay.AddDays(21)],
            pending.Select(p => DateOnly.Parse(p.GetProperty("dueDate").GetString()!)).Order());
    }

    [Fact]
    public async Task BR09_NewAttemptSupersedesPendingLadder()
    {
        var api = await _factory.SetUpAsync();
        var pid = await ProblemId(api, "3Sum");
        await Log(api, Attempt(Json.Today.AddDays(-5), pid, 2));
        await Log(api, Attempt(Json.Today, pid, 5));
        var pending = await Pending(api, pid);
        Assert.Equal(Json.Today.AddDays(30), DateOnly.Parse(Assert.Single(pending).GetProperty("dueDate").GetString()!));
    }

    [Fact]
    public async Task EditingConfidenceRegeneratesReviewsAndUpdatesDerivedTime()
    {
        var api = await _factory.SetUpAsync();
        var pid = await ProblemId(api, "Min Stack");
        var a = await Log(api, Attempt(Json.Today.AddDays(-1), pid, 2));
        var id = a.GetProperty("id").GetInt32();
        var body = JsonSerializer.SerializeToNode(Attempt(Json.Today.AddDays(-1), pid, 5), Json.Options)!.AsObject();
        body["rowVersion"] = a.GetProperty("rowVersion").GetString();
        body["totalMin"] = 50;
        var updated = await api.PutAsJsonAsync($"/api/v1/dsa/attempts/{id}", body, Json.Options).OkAsync();
        var pending = updated.GetProperty("reviews").EnumerateArray().Where(r => r.GetProperty("status").GetString() == "Pending").ToList();
        Assert.Equal(30, Assert.Single(pending).GetProperty("intervalDays").GetInt32());

        var log = await api.GetAsync($"/api/v1/daily-logs/by-date/{Json.Today.AddDays(-1):yyyy-MM-dd}").OkAsync();
        Assert.Equal(50, Assert.Single(log.GetProperty("timeEntries").EnumerateArray()).GetProperty("minutes").GetInt32());

        // Derived entries cannot be edited directly (BR-19).
        var entryId = log.GetProperty("timeEntries")[0].GetProperty("id").GetInt32();
        var direct = await api.DeleteAsync($"/api/v1/time-entries/{entryId}");
        Assert.Equal(HttpStatusCode.Conflict, direct.StatusCode);
    }

    [Fact]
    public async Task DeletingAnAttemptRemovesItsTimeAndReviewsAndRestoreBringsTimeBack()
    {
        var api = await _factory.SetUpAsync();
        var pid = await ProblemId(api, "Car Fleet");
        var a = await Log(api, Attempt(Json.Today, pid, 3));
        var id = a.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await api.DeleteAsync($"/api/v1/dsa/attempts/{id}")).StatusCode);
        Assert.Empty(await Pending(api, pid));
        var log = await api.GetAsync($"/api/v1/daily-logs/by-date/{Json.Today:yyyy-MM-dd}").OkAsync();
        Assert.Empty(log.GetProperty("timeEntries").EnumerateArray());
        Assert.False(log.GetProperty("dsaTick").GetBoolean());

        await api.PostAsync($"/api/v1/trash/DsaAttempt/{id}/restore", null).OkAsync();
        log = await api.GetAsync($"/api/v1/daily-logs/by-date/{Json.Today:yyyy-MM-dd}").OkAsync();
        Assert.Single(log.GetProperty("timeEntries").EnumerateArray());
        Assert.True(log.GetProperty("dsaTick").GetBoolean());
    }

    [Fact]
    public async Task FreeTextAttemptGetsNoReviewsAndColdReviewOfItIsNotCounted()
    {
        var api = await _factory.SetUpAsync();
        var a = await Log(api, Attempt(Json.Today, null, 2, title: "Some contest problem"));
        Assert.Empty(a.GetProperty("reviews").EnumerateArray());
        Assert.True(a.GetProperty("isCounted").GetBoolean());
        var cold = await Log(api, Attempt(Json.Today, null, 2, "ColdReview", title: "Some contest problem"));
        Assert.False(cold.GetProperty("isCounted").GetBoolean());
    }

    [Fact]
    public async Task ReviewQueueBucketsSkipAndReschedule()
    {
        var api = await _factory.SetUpAsync();
        var pid = await ProblemId(api, "Koko");
        await Log(api, Attempt(Json.Today.AddDays(-3), pid, 1)); // +1 overdue, +3 due today
        var overdue = await api.GetAsync("/api/v1/dsa/reviews?bucket=Overdue").OkAsync();
        var due = await api.GetAsync("/api/v1/dsa/reviews?bucket=Due").OkAsync();
        Assert.Equal(2, Assert.Single(overdue.EnumerateArray()).GetProperty("daysOverdue").GetInt32());
        var dueId = Assert.Single(due.EnumerateArray()).GetProperty("id").GetInt32();

        var noReason = await api.PostAsJsonAsync($"/api/v1/dsa/reviews/{dueId}/skip", new { reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await api.PostAsJsonAsync($"/api/v1/dsa/reviews/{dueId}/skip", new { reason = "travel day" })).StatusCode);

        var overdueId = overdue[0].GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent,
            (await api.PostAsJsonAsync($"/api/v1/dsa/reviews/{overdueId}/reschedule", new { dueDate = Json.Today.AddDays(2), reason = "busy week" }, Json.Options)).StatusCode);
        Assert.Empty((await api.GetAsync("/api/v1/dsa/reviews?bucket=Overdue").OkAsync()).EnumerateArray());

        var today = await api.GetAsync("/api/v1/views/today").OkAsync();
        Assert.Empty(today.GetProperty("reviewsDue").EnumerateArray());
    }

    [Fact]
    public async Task BR10_ProgressCountsAndWeakestList()
    {
        var api = await _factory.SetUpAsync();
        var p1 = await ProblemId(api, "Two Sum");
        var p2 = await ProblemId(api, "Valid Sudoku");
        await Log(api, Attempt(Json.Today.AddDays(-1), p1, 2, tags: ["off-by-one", "edge-case"]));
        await Log(api, Attempt(Json.Today, p2, 5, tags: ["off-by-one"]));
        await Log(api, Attempt(Json.Today, null, 3, pass1: 10, title: "warm-up"));

        var progress = await api.GetAsync("/api/v1/dsa/progress").OkAsync();
        Assert.Equal(2, progress.GetProperty("countedTotal").GetInt32());
        Assert.Equal(3, progress.GetProperty("attemptsTotal").GetInt32());
        Assert.Equal(90, progress.GetProperty("nextMarker").GetProperty("day").GetInt32());
        Assert.Contains(progress.GetProperty("repeatedMistakes").EnumerateArray(), t => t.GetProperty("tag").GetString() == "off-by-one");
        var arrays = progress.GetProperty("topics").EnumerateArray().First(t => t.GetProperty("topic").GetString() == "Arrays & Hashing");
        Assert.Equal(2, arrays.GetProperty("attempted").GetInt32());
        Assert.Equal(1, arrays.GetProperty("solved").GetInt32());

        var weakest = await api.GetAsync("/api/v1/dsa/weakest").OkAsync();
        Assert.Equal("Two Sum", Assert.Single(weakest.EnumerateArray()).GetProperty("title").GetString());

        var tags = await api.GetAsync("/api/v1/dsa/tags").OkAsync();
        Assert.Equal("off-by-one", tags[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task CsvImportCreatesProblemsAndTopicsAndSkipsDuplicates()
    {
        var api = await _factory.SetUpAsync();
        const string csv = "title,topic,url,difficulty,week\nNumber of Islands,Graphs,https://leetcode.com/problems/number-of-islands/,Medium,14\n\"Clone Graph, again\",Graphs,,,\nTwo Sum,Arrays & Hashing,,,\nMeeting Rooms,Intervals 2,,Easy,\n";
        var dry = await api.PostAsJsonAsync("/api/v1/dsa/problems/import", new { csv, dryRun = true }).OkAsync();
        Assert.True(dry.GetProperty("dryRun").GetBoolean());
        var res = await api.PostAsJsonAsync("/api/v1/dsa/problems/import", new { csv, dryRun = false }).OkAsync();
        Assert.Equal(3, res.GetProperty("created").GetInt32());
        Assert.Equal(1, res.GetProperty("skipped").GetInt32());
        Assert.Equal(["Intervals 2"], res.GetProperty("topicsCreated").EnumerateArray().Select(t => t.GetString()));
        var clone = await api.GetAsync("/api/v1/dsa/problems?q=Clone%20Graph").OkAsync();
        Assert.Equal("Clone Graph, again", clone.GetProperty("items")[0].GetProperty("title").GetString());

        var bad = await api.PostAsJsonAsync("/api/v1/dsa/problems/import", new { csv = "title,topic,difficulty\nX,Graphs,Impossible\n", dryRun = false }).OkAsync();
        Assert.Equal(0, bad.GetProperty("created").GetInt32());
        Assert.NotEmpty(bad.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public async Task TopicWithProblemsCannotBeDeleted()
    {
        var api = await _factory.SetUpAsync();
        var topics = await api.GetAsync("/api/v1/dsa/topics?pageSize=50").OkAsync();
        var stack = topics.GetProperty("items").EnumerateArray().First(t => t.GetProperty("name").GetString() == "Stack").GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Conflict, (await api.DeleteAsync($"/api/v1/dsa/topics/{stack}")).StatusCode);
    }
}
