using SelfTracker.Application.Features;
using SelfTracker.Domain.Entities;

namespace SelfTracker.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void MapPlanExtraEndpoints(this RouteGroupBuilder api)
    {
        var p = api.MapGroup("/plan").WithTags("Plan");
        p.MapGet("/tasks/{id:int}", (int id, PlanTaskService svc, CancellationToken ct) => svc.GetAsync(id, ct));
        p.MapPost("/tasks", async (PlanTaskInput body, PlanTaskService svc, CancellationToken ct) =>
        {
            var created = await svc.SaveAsync(null, body, ct);
            return TypedResults.Created($"/api/v1/plan/tasks/{created.Id}", created);
        });
        p.MapPut("/tasks/{id:int}", (int id, PlanTaskInput body, PlanTaskService svc, CancellationToken ct) => svc.SaveAsync(id, body, ct));
        p.MapPatch("/tasks/{id:int}/status", (int id, PlanTaskStatusInput body, PlanTaskService svc, CancellationToken ct) => svc.SetStatusAsync(id, body, ct));
        p.MapDelete("/tasks/{id:int}", async (int id, PlanTaskService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        p.MapGet("/exit-criteria", (int? phase, ExitCriterionService svc, CancellationToken ct) => svc.ListAsync(phase, ct));
        p.MapPost("/exit-criteria", async (ExitCriterionInput body, ExitCriterionService svc, CancellationToken ct) =>
        {
            var created = await svc.SaveAsync(null, body, ct);
            return TypedResults.Created($"/api/v1/plan/exit-criteria/{created.Id}", created);
        });
        p.MapPut("/exit-criteria/{id:int}", (int id, ExitCriterionInput body, ExitCriterionService svc, CancellationToken ct) => svc.SaveAsync(id, body, ct));
        p.MapDelete("/exit-criteria/{id:int}", async (int id, ExitCriterionService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        p.MapGet("/checkpoints", (CheckpointService svc, CancellationToken ct) => svc.ListAsync(true, ct));
        p.MapPut("/checkpoints/{id:int}", (int id, CheckpointAnswerInput body, CheckpointService svc, CancellationToken ct) => svc.AnswerAsync(id, body, ct));
        p.MapCrud<ReplanEvent, ReplanDto, ReplanInput>("/replans", "Plan");
    }

    public static void MapReviewEndpoints(this RouteGroupBuilder api)
    {
        var w = api.MapGroup("/reviews/weekly").WithTags("Reviews");
        w.MapGet("/", (WeeklyReviewService svc, CancellationToken ct) => svc.ListAsync(ct));
        w.MapGet("/prefill", (DateOnly weekStart, WeeklyReviewService svc, CancellationToken ct) => svc.PrefillAsync(weekStart, ct));
        w.MapGet("/{weekStart}", async (DateOnly weekStart, WeeklyReviewService svc, CancellationToken ct) =>
            await svc.GetAsync(weekStart, ct) is { } r ? Results.Ok(r) : Results.Problem(statusCode: 404, title: "No review for this week yet."))
            .Produces<WeeklyReviewDto>();
        w.MapPut("/{weekStart}", (DateOnly weekStart, WeeklyReviewInput body, WeeklyReviewService svc, CancellationToken ct) => svc.SaveAsync(weekStart, body, ct));
        w.MapDelete("/by-id/{id:int}", async (int id, WeeklyReviewService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        var m = api.MapGroup("/reviews/monthly").WithTags("Reviews");
        m.MapGet("/", (MonthlyReviewService svc, CancellationToken ct) => svc.ListAsync(ct));
        m.MapGet("/prefill", (DateOnly month, MonthlyReviewService svc, CancellationToken ct) => svc.PrefillAsync(month, ct));
        m.MapGet("/{month}", async (DateOnly month, MonthlyReviewService svc, CancellationToken ct) =>
            await svc.GetAsync(month, ct) is { } r ? Results.Ok(r) : Results.Problem(statusCode: 404, title: "No review for this month yet."))
            .Produces<MonthlyReviewDto>();
        m.MapPut("/{month}", (DateOnly month, MonthlyReviewInput body, MonthlyReviewService svc, CancellationToken ct) => svc.SaveAsync(month, body, ct));
        m.MapDelete("/by-id/{id:int}", async (int id, MonthlyReviewService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        var s = api.MapGroup("");
        s.MapCrud<SomedayItem, SomedayDto, SomedayInput>("/someday", "Someday", "status");
        s.MapPost("/someday/{id:int}/promote", (int id, SomedayService svc, CancellationToken ct) => svc.PromoteAsync(id, ct)).WithTags("Someday");
        s.MapPost("/someday/{id:int}/triage", (int id, SomedayTriageInput body, SomedayService svc, CancellationToken ct) => svc.TriageAsync(id, body.Action, ct)).WithTags("Someday");

        var v = api.MapGroup("/views").WithTags("Views");
        v.MapGet("/nudges", (DateOnly? date, NudgeService svc, CancellationToken ct) => svc.ComputeAsync(date, ct));
        v.MapGet("/weekly-hours", async (DateOnly? weekStart, WeeklyHoursService svc, Application.Abstractions.PlanContext plan, CancellationToken ct) =>
            await svc.ForWeekAsync(weekStart ?? (await plan.LoadAsync(ct)).Today, ct));
    }
}
