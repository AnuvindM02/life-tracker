using SelfTracker.Application.Abstractions;
using SelfTracker.Application.Features;
using SelfTracker.Domain.Entities;

namespace SelfTracker.Api.Endpoints;

public static class DsaEndpoints
{
    public static void MapDsaEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/dsa");
        g.MapCrud<DsaTopic, DsaTopicDto, DsaTopicInput>("/topics", "DSA");
        g.MapCrud<DsaProblem, DsaProblemDto, DsaProblemInput>("/problems", "DSA", "topic", "status", "week", "q");

        var d = g.MapGroup("").WithTags("DSA");
        d.MapPost("/problems/import", (ImportProblemsRequest body, IHandler<ImportProblemsRequest, ImportProblemsResult> h, CancellationToken ct) =>
            h.HandleAsync(body, ct));

        d.MapGet("/attempts", (DateOnly? from, DateOnly? to, int? problemId, int? confidence, AttemptType? type, string? tag, string? company, bool? counted,
            [AsParameters] Paging paging, IHandler<ListDsaAttempts, PagedResult<DsaAttemptSummaryDto>> h, CancellationToken ct) =>
            h.HandleAsync(new ListDsaAttempts(from, to, problemId, confidence, type, tag, company, counted, paging.ToQuery()), ct));
        d.MapGet("/attempts/{id:int}", (int id, IHandler<GetDsaAttempt, DsaAttemptDto> h, CancellationToken ct) => h.HandleAsync(new GetDsaAttempt(id), ct));
        d.MapPost("/attempts", async (DsaAttemptInput body, IHandler<CreateDsaAttempt, DsaAttemptDto> h, CancellationToken ct) =>
        {
            var created = await h.HandleAsync(new CreateDsaAttempt(body), ct);
            return TypedResults.Created($"/api/v1/dsa/attempts/{created.Id}", created);
        });
        d.MapPut("/attempts/{id:int}", (int id, DsaAttemptInput body, IHandler<UpdateDsaAttempt, DsaAttemptDto> h, CancellationToken ct) =>
            h.HandleAsync(new UpdateDsaAttempt(id, body), ct));
        d.MapDelete("/attempts/{id:int}", async (int id, IHandler<DeleteDsaAttempt, bool> h, CancellationToken ct) =>
        {
            await h.HandleAsync(new DeleteDsaAttempt(id), ct);
            return TypedResults.NoContent();
        });

        d.MapGet("/reviews", (ReviewBucket? bucket, int? problemId, IHandler<ListDsaReviews, IReadOnlyList<DsaReviewDto>> h, CancellationToken ct) =>
            h.HandleAsync(new ListDsaReviews(bucket ?? ReviewBucket.Due, problemId), ct));
        d.MapPost("/reviews/{id:int}/skip", async (int id, SkipReviewRequest body, IHandler<SkipReview, bool> h, CancellationToken ct) =>
        {
            await h.HandleAsync(new SkipReview(id, body), ct);
            return TypedResults.NoContent();
        });
        d.MapPost("/reviews/{id:int}/reschedule", async (int id, RescheduleReviewRequest body, IHandler<RescheduleReview, bool> h, CancellationToken ct) =>
        {
            await h.HandleAsync(new RescheduleReview(id, body), ct);
            return TypedResults.NoContent();
        });

        d.MapGet("/progress", (IHandler<GetDsaProgress, DsaProgressDto> h, CancellationToken ct) => h.HandleAsync(new GetDsaProgress(), ct));
        d.MapGet("/weakest", (int? take, IHandler<GetWeakest, IReadOnlyList<WeakProblemDto>> h, CancellationToken ct) =>
            h.HandleAsync(new GetWeakest(take ?? 20), ct));
        d.MapGet("/tags", (TagKind? kind, IHandler<ListTags, IReadOnlyList<TagDto>> h, CancellationToken ct) =>
            h.HandleAsync(new ListTags(kind ?? TagKind.Mistake), ct));
    }
}
