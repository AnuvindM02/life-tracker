using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(SelfTracker.Api.Tests.SqlServerContainer))]

namespace SelfTracker.Api.Tests;

/// <summary>One SQL Server container for the whole test run; every test class gets its own database.</summary>
public sealed class SqlServerContainer : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-CU11-ubuntu-24.04").Build();

    public string ConnectionStringFor(string database) =>
        new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = database }.ConnectionString;

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

public sealed class TrackerFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public TrackerFactory(SqlServerContainer sql)
    {
        DataRoot = Path.Combine(Path.GetTempPath(), "selftracker-tests", Guid.NewGuid().ToString("N"), "data");
        FileRoot = Path.Combine(DataRoot, "files");
        Directory.CreateDirectory(DataRoot);
        _connectionString = sql.ConnectionStringFor("t_" + Guid.NewGuid().ToString("N")[..12]);
    }

    public string DataRoot { get; }

    public string FileRoot { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        Environment.SetEnvironmentVariable("SELFTRACKER_DATA", DataRoot);
    }

    /// <summary>A client that behaves like the app's own SPA (loopback host, guard header).</summary>
    public HttpClient Api()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });
        client.DefaultRequestHeaders.Add("X-SelfTracker", "1");
        return client;
    }

    public async Task<HttpClient> SetUpAsync(DateOnly? start = null, DateOnly? travelAnchor = null)
    {
        var api = Api();
        var res = await api.PostAsJsonAsync("/api/v1/settings/setup", new
        {
            planStartDate = start ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-20),
            ddiaEdition = "Second",
            travelAnchorFriday = travelAnchor,
            travelEnabled = travelAnchor is not null,
            fileRoot = FileRoot,
            timeZone = "UTC",
        }, Json.Options);
        res.EnsureSuccessStatusCode();
        return api;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        try
        {
            Directory.Delete(Path.GetDirectoryName(DataRoot)!, recursive: true);
        }
        catch (IOException)
        {
            // best effort
        }
    }
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static async Task<JsonElement> ReadAsync(this HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        return string.IsNullOrEmpty(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    public static async Task<JsonElement> OkAsync(this Task<HttpResponseMessage> call)
    {
        var res = await call;
        var body = await res.ReadAsync();
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {body}");
        return body;
    }

    public static MultipartFormDataContent Files(params (string Name, byte[] Content)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, content) in files)
        {
            var part = new ByteArrayContent(content);
            part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(part, "files", name);
        }

        return form;
    }

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
