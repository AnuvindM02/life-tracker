using Microsoft.AspNetCore.Mvc;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Common;

namespace SelfTracker.Api.Endpoints;

public static class EndpointHelpers
{
    /// <summary>Standard list/get/create/update/delete for a simple editable list (spec section 7 conventions).</summary>
    public static RouteGroupBuilder MapCrud<TEntity, TDto, TInput>(this RouteGroupBuilder group, string path, string tag, params string[] filterNames)
        where TEntity : Entity, new()
        where TInput : IVersioned
    {
        var g = group.MapGroup(path).WithTags(tag);

        g.MapGet("/", async (HttpContext ctx, [AsParameters] Paging paging, CrudService<TEntity, TDto, TInput> svc, CancellationToken ct) =>
        {
            var filters = filterNames.ToDictionary(f => f, f => (string?)ctx.Request.Query[f].FirstOrDefault(), StringComparer.OrdinalIgnoreCase);
            return await svc.ListAsync(paging.ToQuery(), filters, ct);
        });

        g.MapGet("/{id:int}", (int id, CrudService<TEntity, TDto, TInput> svc, CancellationToken ct) => svc.GetAsync(id, ct));

        g.MapPost("/", async (TInput input, CrudService<TEntity, TDto, TInput> svc, HttpContext ctx, CancellationToken ct) =>
        {
            var created = await svc.CreateAsync(input, ct);
            return TypedResults.Created($"{ctx.Request.Path.Value?.TrimEnd('/')}/{IdOf(created)}", created);
        });

        g.MapPut("/{id:int}", (int id, TInput input, CrudService<TEntity, TDto, TInput> svc, CancellationToken ct) => svc.UpdateAsync(id, input, ct));

        g.MapDelete("/{id:int}", async (int id, CrudService<TEntity, TDto, TInput> svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        return g;
    }

    public static object? IdOf(object? dto) => dto?.GetType().GetProperty("Id")?.GetValue(dto);
}

/// <summary>Shared paging/sort query parameters.</summary>
public sealed record Paging([FromQuery(Name = "page")] int? Page, [FromQuery(Name = "pageSize")] int? PageSize, [FromQuery(Name = "sort")] string? Sort)
{
    public ListQuery ToQuery() => new(Page ?? 1, PageSize ?? 50, Sort);
}
