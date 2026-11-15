using Microsoft.EntityFrameworkCore;
using SelfTracker.Application.Abstractions;
using SelfTracker.Application.Features;
using SelfTracker.Domain.Entities;

namespace SelfTracker.Api.Endpoints;

public static class DdiaEndpoints
{
    public static void MapDdiaEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/ddia");
        g.MapCrud<DdiaChapter, DdiaChapterDto, DdiaChapterInput>("/chapters", "DDIA", "edition");
        g.MapCrud<ArticulationPrompt, ArticulationPromptDto, ArticulationPromptInput>("/articulation-prompts", "DDIA");

        var d = g.MapGroup("").WithTags("DDIA");
        d.MapGet("/summary", (IHandler<GetDdiaSummary, DdiaSummaryDto> h, CancellationToken ct) => h.HandleAsync(new GetDdiaSummary(), ct));
        d.MapGet("/artifacts", (DateOnly date, IHandler<ListLinkableArtifacts, IReadOnlyList<LinkableArtifact>> h, CancellationToken ct) =>
            h.HandleAsync(new ListLinkableArtifacts(date), ct));

        d.MapGet("/reading-log", (int? chapterId, DateOnly? from, DateOnly? to, [AsParameters] Paging paging, ReadingLogService svc, CancellationToken ct) =>
            svc.ListAsync(chapterId, from, to, paging.ToQuery(), ct));
        d.MapPost("/reading-log", async (ReadingLogInput body, ReadingLogService svc, CancellationToken ct) =>
        {
            var created = await svc.SaveAsync(null, body, ct);
            return TypedResults.Created($"/api/v1/ddia/reading-log/{created.Id}", created);
        });
        d.MapPut("/reading-log/{id:int}", (int id, ReadingLogInput body, ReadingLogService svc, CancellationToken ct) => svc.SaveAsync(id, body, ct));
        d.MapDelete("/reading-log/{id:int}", async (int id, ReadingLogService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        d.MapGet("/notes", async (IAppDbContext db, CancellationToken ct) =>
            await db.DdiaNotes.AsNoTracking().OrderBy(n => n.Chapter!.Edition).ThenBy(n => n.Chapter!.Number)
                .Select(n => new DdiaNoteSummaryDto(n.Id, n.ChapterId, n.Chapter!.Edition, n.Chapter.Number, n.Chapter.Title, n.Status, n.ExplanationRating, n.UpdatedAt))
                .ToListAsync(ct));
        d.MapGet("/notes/{id:int}", (int id, DdiaNoteService svc, CancellationToken ct) => svc.GetAsync(id, ct));
        d.MapGet("/notes/by-chapter/{chapterId:int}", async (int chapterId, DdiaNoteService svc, CancellationToken ct) =>
            await svc.GetByChapterAsync(chapterId, ct) is { } note ? Results.Ok(note) : Results.Problem(statusCode: 404, title: "No note for this chapter yet."))
            .Produces<DdiaNoteDto>();
        d.MapPut("/notes/by-chapter/{chapterId:int}", (int chapterId, DdiaNoteInput body, DdiaNoteService svc, CancellationToken ct) =>
            svc.UpsertAsync(chapterId, body, ct));
        d.MapDelete("/notes/{id:int}", async (int id, DdiaNoteService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        d.MapGet("/drills", (DrillStatus? status, DesignDrillService svc, CancellationToken ct) => svc.ListAsync(status, ct));
        d.MapGet("/drills/{id:int}", (int id, DesignDrillService svc, CancellationToken ct) => svc.GetAsync(id, ct));
        d.MapPost("/drills", async (DesignDrillInput body, DesignDrillService svc, CancellationToken ct) =>
        {
            var created = await svc.SaveAsync(null, body, ct);
            return TypedResults.Created($"/api/v1/ddia/drills/{created.Id}", created);
        });
        d.MapPut("/drills/{id:int}", (int id, DesignDrillInput body, DesignDrillService svc, CancellationToken ct) => svc.SaveAsync(id, body, ct));
        d.MapDelete("/drills/{id:int}", async (int id, DesignDrillService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        d.MapGet("/articulation", (ArticulationKind? kind, DateOnly? from, DateOnly? to, [AsParameters] Paging paging, ArticulationService svc, CancellationToken ct) =>
            svc.ListAsync(kind, from, to, paging.ToQuery(), ct));
        d.MapGet("/articulation/{id:int}", (int id, ArticulationService svc, CancellationToken ct) => svc.GetAsync(id, ct));
        d.MapPost("/articulation", async (ArticulationLogInput body, ArticulationService svc, CancellationToken ct) =>
        {
            var created = await svc.SaveAsync(null, body, ct);
            return TypedResults.Created($"/api/v1/ddia/articulation/{created.Id}", created);
        });
        d.MapPut("/articulation/{id:int}", (int id, ArticulationLogInput body, ArticulationService svc, CancellationToken ct) => svc.SaveAsync(id, body, ct));
        d.MapDelete("/articulation/{id:int}", async (int id, ArticulationService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });
    }
}
