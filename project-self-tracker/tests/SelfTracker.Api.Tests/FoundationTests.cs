using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SelfTracker.Infrastructure.Persistence;
using SelfTracker.Infrastructure.Seeding;

namespace SelfTracker.Api.Tests;

[Collection("api")]
public sealed class FoundationTests(SqlServerContainer sql) : IAsyncLifetime
{
    private readonly TrackerFactory _factory = new(sql);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task HealthReadyIsGreen()
    {
        await _factory.SetUpAsync();
        var res = await _factory.Api().GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Healthy", (await res.ReadAsync()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Guard_RejectsUnsafeRequestWithoutHeader()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/plan/pause-ranges", new { startDate = "2026-01-01", endDate = "2026-01-02", reason = "Other" });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Guard_RejectsCrossSiteUploadWithoutHeader()
    {
        var client = _factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files") { Content = Json.Files(("x.txt", "hello"u8.ToArray())) };
        req.Headers.Add("Origin", "https://evil.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(req)).StatusCode);
    }

    [Fact]
    public async Task Guard_RejectsForeignOriginEvenWithHeader()
    {
        var api = _factory.Api();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/plan/pause-ranges")
        {
            Content = JsonContent.Create(new { startDate = "2026-01-01", endDate = "2026-01-02", reason = "Other" }),
        };
        req.Headers.Add("Origin", "https://evil.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await api.SendAsync(req)).StatusCode);
    }

    [Fact]
    public async Task Guard_RejectsNonLoopbackHost()
    {
        var api = _factory.Api();
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/settings");
        req.Headers.Host = "attacker.example";
        Assert.Equal(HttpStatusCode.BadRequest, (await api.SendAsync(req)).StatusCode);
    }

    [Fact]
    public async Task BR15_SeedingTwiceChangesNothingAndKeepsUserEdits()
    {
        var api = await _factory.SetUpAsync();
        var blocks = await api.GetAsync("/api/v1/schedule/blocks?phase=A&pageSize=200").OkAsync();
        var first = blocks.GetProperty("items")[0];
        var id = first.GetProperty("id").GetInt32();
        await api.PutAsJsonAsync($"/api/v1/schedule/blocks/{id}", new
        {
            schedulePhase = "A",
            startTime = "07:45:00",
            endTime = (string?)null,
            timeLabel = (string?)null,
            title = "Wake (my version)",
            rule = "edited",
            isStretch = false,
            sortOrder = 10,
            rowVersion = first.GetProperty("rowVersion").GetString(),
        }, Json.Options).OkAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = db.ScheduleBlocks.Count() + db.PlanTasks.Count() + db.Phases.Count() + db.Checkpoints.Count() + db.GymPlanDays.Count();
        var results = await scope.ServiceProvider.GetRequiredService<Seeder>().SeedAsync(CancellationToken.None);
        Assert.All(results, r => Assert.Equal(0, r.Inserted));
        var after = db.ScheduleBlocks.Count() + db.PlanTasks.Count() + db.Phases.Count() + db.Checkpoints.Count() + db.GymPlanDays.Count();
        Assert.Equal(before, after);

        var edited = await api.GetAsync($"/api/v1/schedule/blocks/{id}").OkAsync();
        Assert.Equal("Wake (my version)", edited.GetProperty("title").GetString());
        Assert.True(edited.GetProperty("isUserModified").GetBoolean());

        // Restore defaults leaves user-modified rows alone unless chosen explicitly.
        await api.PostAsJsonAsync("/api/v1/settings/restore-defaults/schedule-blocks", new { keys = Array.Empty<string>() }).OkAsync();
        Assert.Equal("Wake (my version)", (await api.GetAsync($"/api/v1/schedule/blocks/{id}").OkAsync()).GetProperty("title").GetString());
        await api.PostAsJsonAsync("/api/v1/settings/restore-defaults/schedule-blocks", new { keys = new[] { "block-a-01" } }).OkAsync();
        Assert.Equal("Wake + morning prep", (await api.GetAsync($"/api/v1/schedule/blocks/{id}").OkAsync()).GetProperty("title").GetString());
    }

    [Fact]
    public async Task BR16_StaleRowVersionReturns409()
    {
        var api = await _factory.SetUpAsync();
        var created = await api.PostAsJsonAsync("/api/v1/plan/pause-ranges",
            new { startDate = "2027-03-01", endDate = "2027-03-03", reason = "Illness" }, Json.Options).OkAsync();
        var id = created.GetProperty("id").GetInt32();
        var v1 = created.GetProperty("rowVersion").GetString();
        await api.PutAsJsonAsync($"/api/v1/plan/pause-ranges/{id}", new { startDate = "2027-03-01", endDate = "2027-03-04", reason = "Illness", rowVersion = v1 }, Json.Options).OkAsync();
        var stale = await api.PutAsJsonAsync($"/api/v1/plan/pause-ranges/{id}", new { startDate = "2027-03-01", endDate = "2027-03-05", reason = "Illness", rowVersion = v1 }, Json.Options);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("changed elsewhere", (await stale.ReadAsync()).GetProperty("title").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BR01_PauseShiftsPlanDayAndTodayShowsPhase()
    {
        var start = Json.Today.AddDays(-9);
        var api = await _factory.SetUpAsync(start);
        Assert.Equal(10, (await api.GetAsync("/api/v1/plan/day").OkAsync()).GetProperty("day").GetInt32());
        await api.PostAsJsonAsync("/api/v1/plan/pause-ranges", new { startDate = start.AddDays(2), endDate = start.AddDays(3), reason = "Travel" }, Json.Options).OkAsync();
        var day = await api.GetAsync("/api/v1/plan/day").OkAsync();
        Assert.Equal(8, day.GetProperty("day").GetInt32());
        Assert.Equal(2, day.GetProperty("week").GetInt32());

        var today = await api.GetAsync("/api/v1/views/today").OkAsync();
        Assert.True(today.GetProperty("setupCompleted").GetBoolean());
        Assert.Equal("Core Gate", today.GetProperty("planDay").GetProperty("phaseName").GetString());
        Assert.Contains(today.GetProperty("weekTasks").EnumerateArray(), t => t.GetProperty("title").GetString()!.StartsWith("DSA W2", StringComparison.Ordinal));
        Assert.NotEmpty(today.GetProperty("scheduleBlocks").EnumerateArray());
    }

    [Fact]
    public async Task OverlappingPauseIsRejected()
    {
        var api = await _factory.SetUpAsync();
        await api.PostAsJsonAsync("/api/v1/plan/pause-ranges", new { startDate = "2027-05-01", endDate = "2027-05-05", reason = "Other" }, Json.Options).OkAsync();
        var res = await api.PostAsJsonAsync("/api/v1/plan/pause-ranges", new { startDate = "2027-05-04", endDate = "2027-05-06", reason = "Other" }, Json.Options);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }
}
