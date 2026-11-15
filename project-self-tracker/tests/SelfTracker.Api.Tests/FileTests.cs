using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using SelfTracker.Infrastructure.Hosting;

namespace SelfTracker.Api.Tests;

[Collection("api")]
public sealed class FileTests(SqlServerContainer sql) : IAsyncLifetime
{
    private readonly TrackerFactory _factory = new(sql);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static byte[] FakeMp4(int size)
    {
        var bytes = new byte[size];
        RandomNumberGenerator.Fill(bytes);
        byte[] head = [0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m'];
        head.CopyTo(bytes, 0);
        return bytes;
    }

    [Fact]
    public async Task UploadDownloadAndRangePlayback()
    {
        var api = await _factory.SetUpAsync();
        var video = FakeMp4(3 * 1024 * 1024);
        var res = await api.PostAsync("/api/v1/files", Json.Files(("mock interview.mp4", video))).OkAsync();
        var file = res[0].GetProperty("file");
        Assert.Equal("Stored", res[0].GetProperty("status").GetString());
        Assert.Equal("video/mp4", file.GetProperty("contentType").GetString());
        Assert.Equal("Video", file.GetProperty("kind").GetString());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(video)), file.GetProperty("sha256").GetString());
        var id = file.GetProperty("id").GetInt32();

        // Stored under {yyyy}/{MM}/{guid}.mp4, never under the original name.
        var onDisk = Directory.GetFiles(_factory.FileRoot, "*.mp4", SearchOption.AllDirectories).Single();
        Assert.DoesNotContain("mock interview", onDisk, StringComparison.Ordinal);

        using var range = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/files/{id}/content");
        range.Headers.Range = new RangeHeaderValue(1000, 1999);
        var partial = await api.SendAsync(range);
        Assert.Equal(HttpStatusCode.PartialContent, partial.StatusCode);
        Assert.Equal(video[1000..2000], await partial.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", partial.Headers.GetValues("X-Content-Type-Options").Single());

        var download = await api.GetAsync($"/api/v1/files/{id}/download");
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal(video, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task DuplicateIsDetectedAndCanBeLinked()
    {
        var api = await _factory.SetUpAsync();
        var log = await api.PostAsync($"/api/v1/daily-logs/by-date/{Json.Today:yyyy-MM-dd}/ensure", null).OkAsync();
        var logId = log.GetProperty("id").GetInt32();
        var content = "# Notes\nsame content"u8.ToArray();
        await api.PostAsync("/api/v1/files", Json.Files(("a.md", content))).OkAsync();
        var second = await api.PostAsync($"/api/v1/files?entityType=DailyLog&entityId={logId}", Json.Files(("b.md", content))).OkAsync();
        Assert.Equal("Duplicate", second[0].GetProperty("status").GetString());
        var existingId = second[0].GetProperty("file").GetProperty("id").GetInt32();
        Assert.Single(Directory.GetFiles(_factory.FileRoot, "*.md", SearchOption.AllDirectories));

        var linked = await api.PostAsJsonAsync($"/api/v1/files/{existingId}/links", new { entityType = "DailyLog", entityId = logId }).OkAsync();
        Assert.Single(linked.GetProperty("links").EnumerateArray());
        var attached = await api.GetAsync($"/api/v1/files?entityType=DailyLog&entityId={logId}").OkAsync();
        Assert.Equal(1, attached.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task BlockedExtensionIsRejectedAndSvgIsNeverInline()
    {
        var api = await _factory.SetUpAsync();
        var res = await api.PostAsync("/api/v1/files", Json.Files(("run.ps1", "Write-Host hi"u8.ToArray()), ("pic.svg", "<svg onload=\"alert(1)\"></svg>"u8.ToArray()))).OkAsync();
        Assert.Equal("Rejected", res[0].GetProperty("status").GetString());
        Assert.Equal("Stored", res[1].GetProperty("status").GetString());
        var svgId = res[1].GetProperty("file").GetProperty("id").GetInt32();
        Assert.False(res[1].GetProperty("file").GetProperty("inlineAllowed").GetBoolean());
        var content = await api.GetAsync($"/api/v1/files/{svgId}/content");
        Assert.Equal("attachment", content.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("application/octet-stream", content.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task TooLargeFileIsRejectedAndLeavesNothingBehind()
    {
        var api = await _factory.SetUpAsync();
        var settings = await api.GetAsync("/api/v1/settings").OkAsync();
        await api.PutAsJsonAsync("/api/v1/settings", new
        {
            planStartDate = settings.GetProperty("planStartDate").GetString(),
            timeZone = "UTC",
            dsaUnaidedMinMinutes = 25,
            reviewIntervalsCsv = "1,3,7,21",
            highConfidenceReviewDays = 30,
            travelAnchorFriday = (string?)null,
            travelEnabled = false,
            maxUploadMb = 1,
            trashRetentionDays = 30,
            nightlyBackupEnabled = true,
            allowDangerousExtensions = false,
            rowVersion = settings.GetProperty("rowVersion").GetString(),
        }).OkAsync();
        var res = await api.PostAsync("/api/v1/files", Json.Files(("big.mp4", FakeMp4(2 * 1024 * 1024))));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, res.StatusCode);
        Assert.Empty(Directory.GetFiles(_factory.FileRoot, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task SoftDeleteThenPurgeRemovesTheFileFromDisk()
    {
        var api = await _factory.SetUpAsync();
        var res = await api.PostAsync("/api/v1/files", Json.Files(("note.txt", "hello world"u8.ToArray()))).OkAsync();
        var id = res[0].GetProperty("file").GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await api.DeleteAsync($"/api/v1/files/{id}")).StatusCode);
        Assert.Single(Directory.GetFiles(_factory.FileRoot, "*.txt", SearchOption.AllDirectories)); // soft delete moves nothing
        Assert.Equal(HttpStatusCode.NoContent, (await api.DeleteAsync($"/api/v1/trash/FileAsset/{id}")).StatusCode);
        Assert.Empty(Directory.GetFiles(_factory.FileRoot, "*.txt", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ExpiredTrashIsPurgedByTheJob()
    {
        var api = await _factory.SetUpAsync();
        var res = await api.PostAsync("/api/v1/files", Json.Files(("old.txt", "old"u8.ToArray()))).OkAsync();
        var id = res[0].GetProperty("file").GetProperty("id").GetInt32();
        await api.DeleteAsync($"/api/v1/files/{id}");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfTracker.Infrastructure.Persistence.AppDbContext>();
            var row = db.FileAssets.IgnoreQueryFiltersFind(id);
            row.DeletedAt = DateTimeOffset.UtcNow.AddDays(-40);
            await db.SaveChangesAsync();
        }

        var purge = _factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<TrashPurgeService>().Single();
        Assert.Equal(1, await purge.RunOnceAsync(CancellationToken.None));
        Assert.Empty(Directory.GetFiles(_factory.FileRoot, "*.txt", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task BR17_VerifyReportsMissingAndTamperedFiles()
    {
        var api = await _factory.SetUpAsync();
        var res = await api.PostAsync("/api/v1/files", Json.Files(("a.txt", "alpha"u8.ToArray()), ("b.txt", "bravo"u8.ToArray()))).OkAsync();
        Assert.Equal(0, (await api.PostAsync("/api/v1/files/maintenance/verify", null).OkAsync()).GetProperty("issues").GetArrayLength());

        var files = Directory.GetFiles(_factory.FileRoot, "*.txt", SearchOption.AllDirectories).Order().ToList();
        File.WriteAllText(files[0], "tampered");
        File.Delete(files[1]);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(files[0])!, "stray.bin"), "x");
        var report = await api.PostAsync("/api/v1/files/maintenance/verify", null).OkAsync();
        Assert.Equal(1, report.GetProperty("hashMismatches").GetInt32());
        Assert.Equal(1, report.GetProperty("missing").GetInt32());
        Assert.Equal(1, report.GetProperty("orphans").GetInt32());
    }

    [Fact]
    public async Task ExportProducesAVerifiableBackup()
    {
        var api = await _factory.SetUpAsync();
        await api.PostAsync("/api/v1/files", Json.Files(("keep.txt", "keep me"u8.ToArray()))).OkAsync();
        var export = await api.PostAsync("/api/v1/data/export", null).OkAsync();
        var name = export.GetProperty("fileName").GetString();
        Assert.Equal(1, export.GetProperty("fileCount").GetInt32());

        var zip = await api.GetByteArrayAsync($"/api/v1/data/backups/{name}");
        var form = new MultipartFormDataContent { { new ByteArrayContent(zip), "file", name! } };
        var summary = await api.PostAsync("/api/v1/data/import?dryRun=true", form).OkAsync();
        Assert.Equal(0, summary.GetProperty("hashMismatches").GetInt32());
        Assert.Equal(1, summary.GetProperty("fileCount").GetInt32());
    }
}

internal static class DbTestExtensions
{
    public static T IgnoreQueryFiltersFind<T>(this Microsoft.EntityFrameworkCore.DbSet<T> set, int id) where T : SelfTracker.Domain.Common.Entity =>
        Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreQueryFilters(set).Single(e => e.Id == id);
}
