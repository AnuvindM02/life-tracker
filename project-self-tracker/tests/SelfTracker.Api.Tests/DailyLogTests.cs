using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SelfTracker.Api.Tests;

[Collection("api")]
public sealed class DailyLogTests(SqlServerContainer sql) : IAsyncLifetime
{
    private readonly TrackerFactory _factory = new(sql);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static object Log(string? dayType, string? rowVersion = null, string? coreLine = null) => new
    {
        dayType,
        energy = 4,
        sleepHours = 7.5,
        dsaLine = "Two Sum, hashing, 28 min, conf 3, forgot complement lookup",
        coreLine,
        buildLine = "scaffolded repo",
        rowVersion,
    };

    private static Task<JsonElement> Create(HttpClient api, DateOnly date, string? dayType) =>
        api.PostAsJsonAsync("/api/v1/daily-logs", new { date, log = Log(dayType) }, Json.Options).OkAsync();

    [Fact]
    public async Task LogsAFullDayAnMvdAndARestDay()
    {
        var api = await _factory.SetUpAsync();
        var full = await Create(api, Json.Today.AddDays(-3), "Full");
        var mvd = await Create(api, Json.Today.AddDays(-2), "Mvd");
        var rest = await Create(api, Json.Today.AddDays(-1), "Rest");
        Assert.Equal("Full", full.GetProperty("dayType").GetString());
        Assert.Equal("Mvd", mvd.GetProperty("dayType").GetString());
        Assert.Equal("Rest", rest.GetProperty("dayType").GetString());
        Assert.True(full.GetProperty("loggedLate").GetBoolean());
        Assert.False(full.GetProperty("dsaTick").GetBoolean());

        var list = await api.GetAsync("/api/v1/daily-logs?pageSize=10").OkAsync();
        Assert.Equal(3, list.GetProperty("total").GetInt32());

        var heat = await api.GetAsync($"/api/v1/views/heatmap?from={Json.Today.AddDays(-4):yyyy-MM-dd}&to={Json.Today:yyyy-MM-dd}").OkAsync();
        var states = heat.GetProperty("cells").EnumerateArray().Select(c => c.GetProperty("state").GetString()).ToList();
        Assert.Equal(["Missed", "Full", "Mvd", "Rest", "Today"], states);
    }

    [Fact]
    public async Task OneLogPerDateAndNoFutureLogs()
    {
        var api = await _factory.SetUpAsync();
        await Create(api, Json.Today, "Full");
        var dup = await api.PostAsJsonAsync("/api/v1/daily-logs", new { date = Json.Today, log = Log("Mvd") }, Json.Options);
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        var future = await api.PostAsJsonAsync("/api/v1/daily-logs", new { date = Json.Today.AddDays(1), log = Log("Full") }, Json.Options);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, future.StatusCode);
    }

    [Fact]
    public async Task BR06_TickWithoutEvidenceIsRejectedAndDsaTickIsDerived()
    {
        var api = await _factory.SetUpAsync();
        var log = await Create(api, Json.Today, "Full");
        var id = log.GetProperty("id").GetInt32();

        var noEvidence = await api.PutAsJsonAsync($"/api/v1/daily-logs/{id}/ticks/Build", new { evidenceNote = "worked on it" }, Json.Options);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noEvidence.StatusCode);

        var dsa = await api.PutAsJsonAsync($"/api/v1/daily-logs/{id}/ticks/Dsa", new { evidenceNote = "solved" }, Json.Options);
        Assert.Equal(HttpStatusCode.BadRequest, dsa.StatusCode);

        var build = await api.PutAsJsonAsync($"/api/v1/daily-logs/{id}/ticks/Build", new { evidenceNote = "added health checks", evidenceUrl = "3f2a9c1" }, Json.Options).OkAsync();
        Assert.Equal("3f2a9c1", build.GetProperty("buildTick").GetProperty("evidenceUrl").GetString());

        var core = await api.PutAsJsonAsync($"/api/v1/daily-logs/{id}/ticks/Core",
            new { evidenceNote = "DDIA ch 2 note:\np99 matters\nmeasure, do not guess" }, Json.Options).OkAsync();
        Assert.NotEqual(JsonValueKind.Null, core.GetProperty("coreTick").ValueKind);

        var cleared = await api.DeleteAsync($"/api/v1/daily-logs/{id}/ticks/Core").OkAsync();
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("coreTick").ValueKind);
    }

    [Fact]
    public async Task BR06_AttachmentCountsAsCoreEvidence()
    {
        var api = await _factory.SetUpAsync();
        var log = await Create(api, Json.Today, "Full");
        var upload = await api.PostAsync("/api/v1/files", Json.Files(("diagram.png", [0x89, 0x50, 0x4E, 0x47, 1, 2, 3]))).OkAsync();
        var fileId = upload[0].GetProperty("file").GetProperty("id").GetInt32();
        var tick = await api.PutAsJsonAsync($"/api/v1/daily-logs/{log.GetProperty("id").GetInt32()}/ticks/Core",
            new { evidenceNote = "drew the data model", attachmentFileIds = new[] { fileId } }, Json.Options).OkAsync();
        Assert.Equal(1, tick.GetProperty("coreTick").GetProperty("attachmentCount").GetInt32());
    }

    [Fact]
    public async Task TimeEntryCreatesInProgressLogAndFeedsMvd()
    {
        var api = await _factory.SetUpAsync();
        var log = await api.PostAsJsonAsync($"/api/v1/daily-logs/by-date/{Json.Today:yyyy-MM-dd}/time-entries",
            new { category = "Dsa", minutes = 25, source = "Timer" }, Json.Options).OkAsync();
        Assert.Equal(JsonValueKind.Null, log.GetProperty("dayType").ValueKind);
        Assert.True(log.GetProperty("mvd").GetProperty("dsaDone").GetBoolean());
        Assert.False(log.GetProperty("mvd").GetProperty("satisfied").GetBoolean());

        var entryId = log.GetProperty("timeEntries")[0].GetProperty("id").GetInt32();
        var derived = await api.PostAsJsonAsync($"/api/v1/daily-logs/by-date/{Json.Today:yyyy-MM-dd}/time-entries",
            new { category = "Dsa", minutes = 5, source = "Derived" }, Json.Options);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, derived.StatusCode);

        var afterDelete = await api.DeleteAsync($"/api/v1/time-entries/{entryId}").OkAsync();
        Assert.Empty(afterDelete.GetProperty("timeEntries").EnumerateArray());
    }

    [Fact]
    public async Task DeletedLogGoesToTrashAndRestoresWithItsTicks()
    {
        var api = await _factory.SetUpAsync();
        var log = await Create(api, Json.Today.AddDays(-1), "Full");
        var id = log.GetProperty("id").GetInt32();
        await api.PutAsJsonAsync($"/api/v1/daily-logs/{id}/ticks/Build", new { evidenceNote = "commit", evidenceUrl = "https://github.com/x/y/commit/abc1234" }, Json.Options).OkAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await api.DeleteAsync($"/api/v1/daily-logs/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetAsync($"/api/v1/daily-logs/{id}")).StatusCode);

        var trash = await api.GetAsync("/api/v1/trash").OkAsync();
        Assert.Contains(trash.EnumerateArray(), t => t.GetProperty("entityType").GetString() == "DailyLog" && t.GetProperty("id").GetInt32() == id);

        await api.PostAsync($"/api/v1/trash/DailyLog/{id}/restore", null).OkAsync();
        var restored = await api.GetAsync($"/api/v1/daily-logs/{id}").OkAsync();
        Assert.NotEqual(JsonValueKind.Null, restored.GetProperty("buildTick").ValueKind);
    }

    [Fact]
    public async Task TravelDayIsSuggestedFromAnchor()
    {
        var today = Json.Today;
        var friday = today.AddDays(-(((int)today.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7));
        var api = await _factory.SetUpAsync(today.AddDays(-30), friday);
        var log = await api.PostAsync($"/api/v1/daily-logs/by-date/{friday:yyyy-MM-dd}/ensure", null).OkAsync();
        Assert.True(log.GetProperty("isTravelDay").GetBoolean());
        Assert.Equal("PreplannedMvd", log.GetProperty("travelRole").GetString());
    }

    [Fact]
    public async Task MissedDaysProduceCalmNudges()
    {
        var api = await _factory.SetUpAsync(Json.Today.AddDays(-5));
        await Create(api, Json.Today.AddDays(-5), "Full");
        await Create(api, Json.Today.AddDays(-4), "Full");
        await Create(api, Json.Today.AddDays(-3), "Full");
        var today = await api.GetAsync("/api/v1/views/today").OkAsync();
        var keys = today.GetProperty("nudges").EnumerateArray().Select(n => n.GetProperty("key").GetString()).ToList();
        Assert.Contains("missed-two", keys);
        Assert.Equal(2, today.GetProperty("metrics").GetProperty("misses").GetProperty("consecutiveMissedEndingYesterday").GetInt32());
    }
}
