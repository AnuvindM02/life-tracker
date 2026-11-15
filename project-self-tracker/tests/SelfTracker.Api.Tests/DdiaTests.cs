using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SelfTracker.Api.Tests;

[Collection("api")]
public sealed class DdiaTests(SqlServerContainer sql) : IAsyncLifetime
{
    private readonly TrackerFactory _factory = new(sql);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<HttpClient> SetUp(string edition, int dayOfPlan = 10)
    {
        var api = _factory.Api();
        await api.PostAsJsonAsync("/api/v1/settings/setup", new
        {
            planStartDate = Json.Today.AddDays(-(dayOfPlan - 1)),
            ddiaEdition = edition,
            travelEnabled = false,
            fileRoot = _factory.FileRoot,
            timeZone = "UTC",
        }, Json.Options).OkAsync();
        return api;
    }

    private static async Task<JsonElement> Chapter(HttpClient api, string edition, int number)
    {
        var list = await api.GetAsync($"/api/v1/ddia/chapters?edition={edition}&pageSize=50").OkAsync();
        return list.GetProperty("items").EnumerateArray().First(c => c.GetProperty("number").GetInt32() == number);
    }

    [Fact]
    public async Task BothEditionsAreSeededAndUnconfirmedShowsWarningFromWeek2()
    {
        var api = await SetUp("Unconfirmed", dayOfPlan: 10);
        Assert.Equal(14, (await api.GetAsync("/api/v1/ddia/chapters?edition=Second").OkAsync()).GetProperty("total").GetInt32());
        Assert.Equal(12, (await api.GetAsync("/api/v1/ddia/chapters?edition=First").OkAsync()).GetProperty("total").GetInt32());

        var summary = await api.GetAsync("/api/v1/ddia/summary").OkAsync();
        Assert.True(summary.GetProperty("showEditionWarning").GetBoolean());
        Assert.Equal("Second", summary.GetProperty("displayEdition").GetString());
        Assert.Equal("Defining Nonfunctional Requirements", Assert.Single(summary.GetProperty("weekChapters").EnumerateArray()).GetProperty("title").GetString());

        var tasks = await api.GetAsync("/api/v1/plan/tasks?week=2").OkAsync();
        var ddia = tasks.GetProperty("items").EnumerateArray().First(t => t.GetProperty("kind").GetString() == "DerivedDdia");
        Assert.Equal("Ch. 2: Defining Nonfunctional Requirements (2nd edition, unconfirmed)", ddia.GetProperty("ddiaChapters")[0].GetString());
    }

    [Fact]
    public async Task NoWarningInWeek1()
    {
        var api = await SetUp("Unconfirmed", dayOfPlan: 3);
        Assert.False((await api.GetAsync("/api/v1/ddia/summary").OkAsync()).GetProperty("showEditionWarning").GetBoolean());
    }

    [Fact]
    public async Task ConfirmingFirstEditionSwitchesChaptersAndPlanTasks()
    {
        var api = await SetUp("Unconfirmed", dayOfPlan: 10);
        await api.PostAsJsonAsync("/api/v1/settings/confirm-ddia-edition", new { edition = "First" }, Json.Options).OkAsync();
        var summary = await api.GetAsync("/api/v1/ddia/summary").OkAsync();
        Assert.False(summary.GetProperty("showEditionWarning").GetBoolean());
        Assert.Equal("First", summary.GetProperty("displayEdition").GetString());
        // 1st edition chapter 1 spans weeks 1-2: never a 2nd-edition number on a 1st-edition book.
        Assert.Equal("Reliable, Scalable, and Maintainable Applications", Assert.Single(summary.GetProperty("weekChapters").EnumerateArray()).GetProperty("title").GetString());

        var today = await api.GetAsync("/api/v1/views/today").OkAsync();
        var ddia = today.GetProperty("weekTasks").EnumerateArray().First(t => t.GetProperty("kind").GetString() == "DerivedDdia");
        Assert.Equal("Ch. 1: Reliable, Scalable, and Maintainable Applications", ddia.GetProperty("ddiaChapters")[0].GetString());
    }

    [Fact]
    public async Task ReadingLogUpdatesChapterAndCreatesDerivedTime()
    {
        var api = await SetUp("Second");
        var ch = await Chapter(api, "Second", 1);
        var id = ch.GetProperty("id").GetInt32();
        var chBody = JsonSerializer.SerializeToNode(new
        {
            edition = "Second", number = 1, title = ch.GetProperty("title").GetString(), plannedWeekFrom = 1, plannedWeekTo = 1,
            topics = (string?)null, askOpsConnection = (string?)null, totalPages = 15, status = "NotStarted", rowVersion = ch.GetProperty("rowVersion").GetString(),
        }, Json.Options);
        await api.PutAsJsonAsync($"/api/v1/ddia/chapters/{id}", chBody, Json.Options).OkAsync();

        var day1 = Json.Today.AddDays(-1);
        var r1 = await api.PostAsJsonAsync("/api/v1/ddia/reading-log", new { date = day1, chapterId = id, pagesRead = 8, minutes = 25, note = "p1-8" }, Json.Options).OkAsync();
        await api.PostAsJsonAsync("/api/v1/ddia/reading-log", new { date = Json.Today, chapterId = id, pagesRead = 7, minutes = 20 }, Json.Options).OkAsync();
        var after = await api.GetAsync($"/api/v1/ddia/chapters/{id}").OkAsync();
        Assert.Equal(15, after.GetProperty("pagesRead").GetInt32());
        Assert.Equal("Reading", after.GetProperty("status").GetString());
        Assert.Equal(day1, DateOnly.Parse(after.GetProperty("startedOn").GetString()!));
        Assert.Equal(Json.Today, DateOnly.Parse(after.GetProperty("finishedOn").GetString()!));
        Assert.Equal(45, after.GetProperty("readingMinutes").GetInt32());

        var log = await api.GetAsync($"/api/v1/daily-logs/by-date/{day1:yyyy-MM-dd}").OkAsync();
        var entry = Assert.Single(log.GetProperty("timeEntries").EnumerateArray());
        Assert.Equal("DdiaDesign", entry.GetProperty("category").GetString());
        Assert.Equal("Derived", entry.GetProperty("source").GetString());

        // Delete moves pages and time away; restore brings both back.
        var rid = r1.GetProperty("id").GetInt32();
        await api.DeleteAsync($"/api/v1/ddia/reading-log/{rid}");
        Assert.Equal(7, (await api.GetAsync($"/api/v1/ddia/chapters/{id}").OkAsync()).GetProperty("pagesRead").GetInt32());
        Assert.Empty((await api.GetAsync($"/api/v1/daily-logs/by-date/{day1:yyyy-MM-dd}").OkAsync()).GetProperty("timeEntries").EnumerateArray());
        await api.PostAsync($"/api/v1/trash/ReadingLog/{rid}/restore", null).OkAsync();
        Assert.Equal(15, (await api.GetAsync($"/api/v1/ddia/chapters/{id}").OkAsync()).GetProperty("pagesRead").GetInt32());
        Assert.Single((await api.GetAsync($"/api/v1/daily-logs/by-date/{day1:yyyy-MM-dd}").OkAsync()).GetProperty("timeEntries").EnumerateArray());
    }

    private static object Note(int ideas, int answered, int? rating = null, string? rowVersion = null) => new
    {
        keyIdea1 = ideas >= 1 ? "Operational vs analytical" : null,
        keyIdea2 = ideas >= 2 ? "Single node first" : null,
        keyIdea3 = ideas >= 3 ? "Measure before scaling" : null,
        keyIdea4 = ideas >= 4 ? "Cloud vs self-hosted trade-offs" : null,
        keyIdea5 = ideas >= 5 ? "Systems of record vs derived data" : null,
        tradeOffsMd = "Monolith vs services: simpler ops vs independent scaling",
        failureModesMd = "Analytical queries starve OLTP",
        askOpsDecision = "Keep reporting reads off the order tables",
        explanationRating = rating,
        explanationNotes = (string?)null,
        questions = Enumerable.Range(1, 3).Select(i => new { question = $"Question {i}?", answer60sMd = i <= answered ? "Answer first, then trade-offs." : null }).ToArray(),
        rowVersion,
    };

    [Fact]
    public async Task NoteLinksToChapterHoldsAttachmentsAndDrivesStatus()
    {
        var api = await SetUp("Second");
        var id = (await Chapter(api, "Second", 3)).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetAsync($"/api/v1/ddia/notes/by-chapter/{id}")).StatusCode);

        var draft = await api.PutAsJsonAsync($"/api/v1/ddia/notes/by-chapter/{id}", Note(3, 1), Json.Options).OkAsync();
        Assert.Equal("Draft", draft.GetProperty("status").GetString());
        Assert.Equal("Second", draft.GetProperty("edition").GetString());
        Assert.Equal("Data Models and Query Languages", draft.GetProperty("chapterTitle").GetString());
        Assert.NotEmpty(draft.GetProperty("missing").EnumerateArray());

        var done = await api.PutAsJsonAsync($"/api/v1/ddia/notes/by-chapter/{id}", Note(5, 3, rowVersion: draft.GetProperty("rowVersion").GetString()), Json.Options).OkAsync();
        Assert.Equal("Complete", done.GetProperty("status").GetString());
        Assert.Equal("NoteDone", done.GetProperty("chapterStatus").GetString());
        Assert.Equal(3, done.GetProperty("questions").GetArrayLength());

        var explained = await api.PutAsJsonAsync($"/api/v1/ddia/notes/by-chapter/{id}", Note(5, 3, 4, done.GetProperty("rowVersion").GetString()), Json.Options).OkAsync();
        Assert.Equal("Explained", explained.GetProperty("chapterStatus").GetString());

        var noteId = explained.GetProperty("id").GetInt32();
        var up = await api.PostAsync($"/api/v1/files?entityType=DdiaNote&entityId={noteId}", Json.Files(("model.png", [0x89, 0x50, 0x4E, 0x47, 9, 9]))).OkAsync();
        Assert.Equal("Stored", up[0].GetProperty("status").GetString());
        var files = await api.GetAsync($"/api/v1/files?entityType=DdiaNote&entityId={noteId}").OkAsync();
        Assert.Equal(1, files.GetProperty("total").GetInt32());

        // A Core tick can link the note (BR-06).
        var log = await api.PostAsync($"/api/v1/daily-logs/by-date/{Json.Today:yyyy-MM-dd}/ensure", null).OkAsync();
        var artifacts = await api.GetAsync($"/api/v1/ddia/artifacts?date={Json.Today:yyyy-MM-dd}").OkAsync();
        Assert.Contains(artifacts.EnumerateArray(), a => a.GetProperty("entityType").GetString() == "DdiaNote" && a.GetProperty("id").GetInt32() == noteId);
        var tick = await api.PutAsJsonAsync($"/api/v1/daily-logs/{log.GetProperty("id").GetInt32()}/ticks/Core",
            new { evidenceNote = "Chapter 3 note", linkedEntityType = "DdiaNote", linkedEntityId = noteId }, Json.Options).OkAsync();
        Assert.Equal("DdiaNote", tick.GetProperty("coreTick").GetProperty("linkedEntityType").GetString());
    }

    [Fact]
    public async Task DrillWrittenNeedsAllSegmentsAndPracticeCreatesTime()
    {
        var api = await SetUp("Second");
        var drills = await api.GetAsync("/api/v1/ddia/drills").OkAsync();
        Assert.Equal(24, drills.GetArrayLength());
        var d1 = drills[0];
        Assert.Equal(12, d1.GetProperty("plannedWeek").GetInt32());
        var id = d1.GetProperty("id").GetInt32();

        object Body(string status, bool full, string rv) => new
        {
            number = 1, prompt = "URL shortener", group = "1-4", plannedWeek = 12, status,
            clarifyNotes = "100M URLs/month, read heavy", estimateNotes = "40 writes/s", apiDataNotes = "POST /urls",
            highLevelNotes = full ? "API + KV store + cache" : null, deepDiveNotes = full ? "ID generation; hot keys" : null,
            tradeOffNotes = full ? "Cache staleness vs cost" : null, spokenMock = false, practicedOn = Json.Today, minutes = 45, rowVersion = rv,
        };

        var partial = await api.PutAsJsonAsync($"/api/v1/ddia/drills/{id}", Body("Written", false, d1.GetProperty("rowVersion").GetString()!), Json.Options);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, partial.StatusCode);
        var written = await api.PutAsJsonAsync($"/api/v1/ddia/drills/{id}", Body("Written", true, d1.GetProperty("rowVersion").GetString()!), Json.Options).OkAsync();
        Assert.Equal("Written", written.GetProperty("status").GetString());
        var reviewed = await api.PutAsJsonAsync($"/api/v1/ddia/drills/{id}", Body("Reviewed", true, written.GetProperty("rowVersion").GetString()!), Json.Options).OkAsync();
        Assert.Equal(Json.Today, DateOnly.Parse(reviewed.GetProperty("reviewedOn").GetString()!));

        var log = await api.GetAsync($"/api/v1/daily-logs/by-date/{Json.Today:yyyy-MM-dd}").OkAsync();
        Assert.Equal(45, Assert.Single(log.GetProperty("timeEntries").EnumerateArray()).GetProperty("minutes").GetInt32());
    }

    [Fact]
    public async Task ArticulationLogWithSeededPrompts()
    {
        var api = await SetUp("Second");
        var prompts = await api.GetAsync("/api/v1/ddia/articulation-prompts").OkAsync();
        Assert.Contains(prompts.GetProperty("items").EnumerateArray(), p => p.GetProperty("text").GetString() == "Dapper vs EF Core");

        var a = await api.PostAsJsonAsync("/api/v1/ddia/articulation", new
        {
            date = Json.Today, kind = "WhyXOverY", prompt = "Dapper vs EF Core", answerMd = "EF Core for writes; Dapper for hot reads.",
            tradeOffsMd = "Productivity vs control", evidenceMd = "p95 of the order list query", selfRating = 3, notes = (string?)null, minutes = 15,
        }, Json.Options).OkAsync();
        Assert.Equal("WhyXOverY", a.GetProperty("kind").GetString());
        var list = await api.GetAsync("/api/v1/ddia/articulation?kind=WhyXOverY").OkAsync();
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        var log = await api.GetAsync($"/api/v1/daily-logs/by-date/{Json.Today:yyyy-MM-dd}").OkAsync();
        Assert.Equal(15, Assert.Single(log.GetProperty("timeEntries").EnumerateArray()).GetProperty("minutes").GetInt32());
    }

    [Fact]
    public async Task ChapterWithReadingCannotBeDeleted()
    {
        var api = await SetUp("Second");
        var id = (await Chapter(api, "Second", 4)).GetProperty("id").GetInt32();
        await api.PostAsJsonAsync("/api/v1/ddia/reading-log", new { date = Json.Today, chapterId = id, pagesRead = 5, minutes = 0 }, Json.Options).OkAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await api.DeleteAsync($"/api/v1/ddia/chapters/{id}")).StatusCode);
    }
}
