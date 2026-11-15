using SelfTracker.Application.Abstractions;
using SelfTracker.Application.Features;
using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;
using SelfTracker.Infrastructure.Seeding;

namespace SelfTracker.Api.Endpoints;

public sealed record RestoreDefaultsRequest(string[]? Keys);

public sealed record RestoreDefaultsResult(string List, int Restored);

public static class CoreEndpoints
{
    public static void MapSettingsEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/settings").WithTags("Settings");
        g.MapGet("/", (IHandler<GetSettings, SettingsDto> h, CancellationToken ct) => h.HandleAsync(new GetSettings(), ct));
        g.MapPut("/", (UpdateSettingsRequest body, IHandler<UpdateSettingsRequest, SettingsDto> h, CancellationToken ct) => h.HandleAsync(body, ct));
        g.MapPost("/setup", (SetupRequest body, IHandler<SetupRequest, SettingsDto> h, CancellationToken ct) => h.HandleAsync(body, ct));
        g.MapPost("/confirm-ddia-edition", (ConfirmDdiaEditionRequest body, IHandler<ConfirmDdiaEditionRequest, SettingsDto> h, CancellationToken ct) =>
            h.HandleAsync(body, ct));
        g.MapPost("/restore-defaults/{list}", async (string list, RestoreDefaultsRequest? body, Seeder seeder, CancellationToken ct) =>
            new RestoreDefaultsResult(list, await seeder.RestoreDefaultsAsync(list, body?.Keys, ct)));
        g.MapGet("/seed-lists", () => Seeder.Lists.Select(l => l.List).ToArray());
    }

    public static void MapPlanEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/plan").WithTags("Plan");
        g.MapGet("/phases", (IHandler<GetPhases, IReadOnlyList<PhaseDto>> h, CancellationToken ct) => h.HandleAsync(new GetPhases(), ct));
        g.MapGet("/day", (DateOnly? date, IHandler<GetPlanDay, PlanDayDto> h, CancellationToken ct) => h.HandleAsync(new GetPlanDay(date), ct));
        g.MapGet("/tasks", (int? week, int? day, PlanTrack? track, PlanTaskStatus? status, int? phase, [AsParameters] Paging paging,
            IHandler<ListPlanTasks, PagedResult<PlanTaskDto>> h, CancellationToken ct) =>
            h.HandleAsync(new ListPlanTasks(week, day, track, status, phase, paging.ToQuery()), ct));
        g.MapCrud<PauseRange, PauseRangeDto, PauseRangeInput>("/pause-ranges", "Plan");
        g.MapCrud<TravelOverride, TravelOverrideDto, TravelOverrideInput>("/travel-overrides", "Plan");
    }

    public static void MapScheduleEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/schedule");
        g.MapCrud<ScheduleBlock, ScheduleBlockDto, ScheduleBlockInput>("/blocks", "Schedule", "phase");
        g.MapCrud<GymPlanDay, GymPlanDayDto, GymPlanDayInput>("/gym-plan", "Schedule", "phase");
    }

    public static void MapDailyLogEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/daily-logs").WithTags("Daily logs");
        g.MapGet("/", (DateOnly? from, DateOnly? to, DayType? type, bool? inProgress, [AsParameters] Paging paging,
            IHandler<ListDailyLogs, PagedResult<DailyLogSummaryDto>> h, CancellationToken ct) =>
            h.HandleAsync(new ListDailyLogs(from, to, type, inProgress, paging.ToQuery()), ct));
        g.MapGet("/{id:int}", (int id, IHandler<GetDailyLog, DailyLogDto> h, CancellationToken ct) => h.HandleAsync(new GetDailyLog(id), ct));
        g.MapGet("/by-date/{date}", (DateOnly date, IHandler<GetDailyLogByDate, DailyLogDto> h, CancellationToken ct) =>
            h.HandleAsync(new GetDailyLogByDate(date), ct));
        g.MapPost("/by-date/{date}/ensure", (DateOnly date, IHandler<EnsureDailyLog, DailyLogDto> h, CancellationToken ct) =>
            h.HandleAsync(new EnsureDailyLog(date), ct));
        g.MapPost("/", async (CreateDailyLogRequest body, IHandler<CreateDailyLogRequest, DailyLogDto> h, CancellationToken ct) =>
        {
            var created = await h.HandleAsync(body, ct);
            return TypedResults.Created($"/api/v1/daily-logs/{created.Id}", created);
        });
        g.MapPut("/{id:int}", (int id, DailyLogInput body, IHandler<UpdateDailyLog, DailyLogDto> h, CancellationToken ct) =>
            h.HandleAsync(new UpdateDailyLog(id, body), ct));
        g.MapDelete("/{id:int}", async (int id, IHandler<DeleteDailyLog, bool> h, CancellationToken ct) =>
        {
            await h.HandleAsync(new DeleteDailyLog(id), ct);
            return TypedResults.NoContent();
        });

        g.MapPut("/{id:int}/ticks/{type}", (int id, TickType type, TickRequest body, IHandler<SetTick, DailyLogDto> h, CancellationToken ct) =>
            h.HandleAsync(new SetTick(id, type, body), ct));
        g.MapDelete("/{id:int}/ticks/{type}", (int id, TickType type, IHandler<ClearTick, DailyLogDto> h, CancellationToken ct) =>
            h.HandleAsync(new ClearTick(id, type), ct));

        g.MapPost("/by-date/{date}/time-entries", (DateOnly date, TimeEntryInput body, IHandler<AddTimeEntry, DailyLogDto> h, CancellationToken ct) =>
            h.HandleAsync(new AddTimeEntry(date, body), ct));
        var t = api.MapGroup("/time-entries").WithTags("Daily logs");
        t.MapPut("/{id:int}", (int id, TimeEntryInput body, IHandler<UpdateTimeEntry, DailyLogDto> h, CancellationToken ct) =>
            h.HandleAsync(new UpdateTimeEntry(id, body), ct));
        t.MapDelete("/{id:int}", (int id, IHandler<DeleteTimeEntry, DailyLogDto> h, CancellationToken ct) =>
            h.HandleAsync(new DeleteTimeEntry(id), ct));
    }

    public static void MapTrashEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/trash").WithTags("Trash");
        g.MapGet("/", (string? entityType, IHandler<ListTrash, IReadOnlyList<TrashItemDto>> h, CancellationToken ct) =>
            h.HandleAsync(new ListTrash(entityType), ct));
        g.MapPost("/{entityType}/{id:int}/restore", (string entityType, int id, IHandler<RestoreTrashItem, TrashItemDto> h, CancellationToken ct) =>
            h.HandleAsync(new RestoreTrashItem(entityType, id), ct));
        g.MapDelete("/{entityType}/{id:int}", async (string entityType, int id, IHandler<PurgeTrashItem, bool> h, CancellationToken ct) =>
        {
            await h.HandleAsync(new PurgeTrashItem(entityType, id), ct);
            return TypedResults.NoContent();
        });
    }

    public static void MapViewEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/views").WithTags("Views");
        g.MapGet("/today", (DateOnly? date, IHandler<GetToday, TodayView> h, CancellationToken ct) => h.HandleAsync(new GetToday(date), ct));
        g.MapGet("/heatmap", (DateOnly? from, DateOnly? to, IHandler<GetHeatmap, HeatmapView> h, CancellationToken ct) =>
            h.HandleAsync(new GetHeatmap(from, to), ct));
        g.MapGet("/metrics", (DateOnly? date, IHandler<GetMetrics, MetricsDto> h, CancellationToken ct) => h.HandleAsync(new GetMetrics(date), ct));
    }
}
