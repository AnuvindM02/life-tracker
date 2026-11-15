using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SelfTracker.Domain.Common;

namespace SelfTracker.Application.Abstractions;

public interface IVersioned
{
    string? RowVersion { get; }
}

/// <summary>
/// Describes a simple editable list (pause ranges, schedule blocks, gym plan ...).
/// Richer modules use dedicated handlers; see DECISIONS.md D-005.
/// </summary>
public interface ICrudSpec<TEntity, TDto, in TInput>
    where TEntity : Entity, new()
{
    string Name { get; }

    string DefaultSort { get; }

    Expression<Func<TEntity, TDto>> Projection { get; }

    IQueryable<TEntity> Filter(IQueryable<TEntity> query, IReadOnlyDictionary<string, string?> filters) => query;

    Task ValidateAsync(TInput input, TEntity? existing, CancellationToken ct) => Task.CompletedTask;

    Task ValidateDeleteAsync(TEntity entity, CancellationToken ct) => Task.CompletedTask;

    void Apply(TInput input, TEntity entity);
}

public sealed class CrudService<TEntity, TDto, TInput>(IAppDbContext db, ICrudSpec<TEntity, TDto, TInput> spec, IClock clock)
    where TEntity : Entity, new()
    where TInput : IVersioned
{
    public Task<PagedResult<TDto>> ListAsync(ListQuery list, IReadOnlyDictionary<string, string?> filters, CancellationToken ct) =>
        spec.Filter(db.Set<TEntity>().AsNoTracking(), filters)
            .ApplySort(list.Sort, spec.DefaultSort)
            .Select(spec.Projection)
            .ToPagedAsync(list, ct);

    public async Task<TDto> GetAsync(int id, CancellationToken ct) =>
        await db.Set<TEntity>().AsNoTracking().Where(e => e.Id == id).Select(spec.Projection).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException($"{spec.Name} {id}");

    public async Task<TDto> CreateAsync(TInput input, CancellationToken ct)
    {
        await spec.ValidateAsync(input, null, ct);
        var entity = new TEntity();
        spec.Apply(input, entity);
        db.Set<TEntity>().Add(entity);
        await db.SaveChangesAsync(ct);
        return await GetAsync(entity.Id, ct);
    }

    public async Task<TDto> UpdateAsync(int id, TInput input, CancellationToken ct)
    {
        var entity = await db.Set<TEntity>().FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException($"{spec.Name} {id}");
        db.ExpectVersion(entity, input.RowVersion);
        await spec.ValidateAsync(input, entity, ct);
        spec.Apply(input, entity);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await db.Set<TEntity>().FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException($"{spec.Name} {id}");
        await spec.ValidateDeleteAsync(entity, ct);
        entity.SoftDelete(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}
