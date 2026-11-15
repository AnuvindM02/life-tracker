using Microsoft.EntityFrameworkCore;
using SelfTracker.Domain.Common;

namespace SelfTracker.Application.Abstractions;

public static class Queryables
{
    /// <summary>Sort by "field" or "-field" (descending). Only real entity properties are accepted.</summary>
    public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, string? sort, string defaultSort)
    {
        var spec = string.IsNullOrWhiteSpace(sort) ? defaultSort : sort;
        var desc = spec.StartsWith('-');
        var name = spec.TrimStart('-', '+');
        var prop = typeof(T).GetProperties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
            && (p.PropertyType.IsValueType || p.PropertyType == typeof(string)));
        if (prop is null)
        {
            throw new BadRequestException($"Cannot sort by '{name}'.");
        }

        var ordered = desc
            ? query.OrderByDescending(e => EF.Property<object>(e!, prop.Name))
            : query.OrderBy(e => EF.Property<object>(e!, prop.Name));
        return ordered.ThenBy(e => EF.Property<object>(e!, "Id"));
    }

    public static async Task<PagedResult<TDto>> ToPagedAsync<TDto>(this IQueryable<TDto> query, ListQuery list, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((list.SafePage - 1) * list.SafePageSize).Take(list.SafePageSize).ToListAsync(ct);
        return new PagedResult<TDto>(items, total, list.SafePage, list.SafePageSize);
    }

    /// <summary>Set the original RowVersion so SaveChanges detects a stale edit (BR-16).</summary>
    public static void ExpectVersion<T>(this IAppDbContext db, T entity, string? rowVersion) where T : Entity
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
        {
            throw RuleViolationException.For("rowVersion", "rowVersion is required for updates (send it in the body or If-Match).");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(rowVersion.Trim('"'));
        }
        catch (FormatException)
        {
            throw RuleViolationException.For("rowVersion", "rowVersion is not valid base64.");
        }

        db.Entry(entity).Property(e => e.RowVersion).OriginalValue = bytes;
    }

    public static string Version(this Entity entity) => Convert.ToBase64String(entity.RowVersion);

    public static void SoftDelete(this Entity entity, DateTimeOffset now)
    {
        entity.IsDeleted = true;
        entity.DeletedAt = now;
    }
}
