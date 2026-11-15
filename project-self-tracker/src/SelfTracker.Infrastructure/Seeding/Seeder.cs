using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;
using SelfTracker.Infrastructure.Persistence;

namespace SelfTracker.Infrastructure.Seeding;

public sealed record SeedResult(string List, int Inserted, int Skipped);

/// <summary>
/// BR-15: idempotent seeding by SeedKey. Inserts missing rows only (soft-deleted rows count as present) and never
/// overwrites a row the user has edited. "Restore defaults" re-applies seed values to unmodified or chosen rows.
/// </summary>
public sealed class Seeder(AppDbContext db, ILogger<Seeder> logger)
{
    /// <summary>Seed lists in dependency order: file name (without .json) and entity type.</summary>
    public static readonly IReadOnlyList<(string List, Type Entity)> Lists =
    [
        ("phases", typeof(Phase)),
        ("plan-tasks", typeof(PlanTask)),
        ("checkpoints", typeof(Checkpoint)),
        ("schedule-blocks", typeof(ScheduleBlock)),
        ("gym-plan", typeof(GymPlanDay)),
        ("dsa-topics", typeof(DsaTopic)),
        ("dsa-problems", typeof(DsaProblem)),
        ("ddia-chapters-2e", typeof(DdiaChapter)),
        ("ddia-chapters-1e", typeof(DdiaChapter)),
        ("design-drills", typeof(DesignDrill)),
        ("articulation-prompts", typeof(ArticulationPrompt)),
        ("exit-criteria", typeof(ExitCriterion)),
        ("weekly-hour-targets", typeof(WeeklyHourTarget)),
    ];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };

    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "id", "createdAt", "updatedAt", "isDeleted", "deletedAt", "rowVersion", "isUserModified", "phaseNumber", "topicSeedKey",
    };

    public static string SeedDirectory => Path.Combine(AppContext.BaseDirectory, "seed");

    public async Task<IReadOnlyList<SeedResult>> SeedAsync(CancellationToken ct)
    {
        var results = new List<SeedResult>();
        db.SeedingMode = true;
        try
        {
            foreach (var (list, type) in Lists)
            {
                var rows = Load(list);
                if (rows is null)
                {
                    continue;
                }

                var existing = await ExistingKeysAsync(type, ct);
                int inserted = 0, skipped = 0;
                foreach (var row in rows.OfType<JsonNode>())
                {
                    var key = row["seedKey"]?.GetValue<string>() ?? throw new InvalidOperationException($"seed/{list}.json: row without seedKey");
                    if (existing.Contains(key))
                    {
                        skipped++;
                        continue;
                    }

                    var entity = (Entity)row.Deserialize(type, Json)!;
                    await ResolveReferencesAsync(entity, row, ct);
                    db.Add(entity);
                    inserted++;
                }

                await db.SaveChangesAsync(ct);
                results.Add(new SeedResult(list, inserted, skipped));
                logger.LogInformation("Seed {List}: {Inserted} inserted, {Skipped} skipped", list, inserted, skipped);
            }
        }
        finally
        {
            db.SeedingMode = false;
        }

        return results;
    }

    /// <summary>Re-apply seed values for a list: rows not modified by the user, plus any explicitly chosen keys.</summary>
    public async Task<int> RestoreDefaultsAsync(string list, IReadOnlyCollection<string>? chosenKeys, CancellationToken ct)
    {
        var (_, type) = Lists.FirstOrDefault(l => l.List == list);
        if (type is null)
        {
            throw new NotFoundException($"Seed list '{list}'");
        }

        var rows = Load(list) ?? [];
        var entities = await AllSeededAsync(type, ct);
        var restored = 0;
        db.SeedingMode = true;
        try
        {
            foreach (var row in rows.OfType<JsonNode>())
            {
                var key = row["seedKey"]!.GetValue<string>();
                if (!entities.TryGetValue(key, out var entity))
                {
                    var fresh = (Entity)row.Deserialize(type, Json)!;
                    await ResolveReferencesAsync(fresh, row, ct);
                    db.Add(fresh);
                    restored++;
                    continue;
                }

                var seeded = (ISeedable)entity;
                if (seeded.IsUserModified && chosenKeys?.Contains(key) != true)
                {
                    continue;
                }

                var fresh2 = (Entity)row.Deserialize(type, Json)!;
                await ResolveReferencesAsync(fresh2, row, ct);
                CopySeedValues(row, fresh2, entity);
                seeded.IsUserModified = false;
                entity.IsDeleted = false;
                entity.DeletedAt = null;
                restored++;
            }

            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.SeedingMode = false;
        }

        return restored;
    }

    private static JsonArray? Load(string list)
    {
        var path = Path.Combine(SeedDirectory, $"{list}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonNode.Parse(File.ReadAllText(path))!["rows"]!.AsArray();
    }

    private async Task ResolveReferencesAsync(Entity entity, JsonNode row, CancellationToken ct)
    {
        if (entity is ExitCriterion criterion)
        {
            var phase = row["phaseNumber"]?.GetValue<int>() ?? throw new InvalidOperationException($"Exit criterion {criterion.SeedKey} needs phaseNumber");
            criterion.PhaseId = await db.Phases.IgnoreQueryFilters().Where(p => p.Number == phase).Select(p => p.Id).FirstAsync(ct);
        }
        else if (entity is PlanTask task)
        {
            var number = row["phaseNumber"]?.GetValue<int>() ?? throw new InvalidOperationException($"Plan task {task.SeedKey} needs phaseNumber");
            task.PhaseId = await db.Phases.IgnoreQueryFilters().Where(p => p.Number == number).Select(p => p.Id).FirstAsync(ct);
        }
        else if (entity is DsaProblem problem)
        {
            var topicKey = row["topicSeedKey"]?.GetValue<string>() ?? throw new InvalidOperationException($"Problem {problem.SeedKey} needs topicSeedKey");
            problem.TopicId = await db.DsaTopics.IgnoreQueryFilters().Where(t => t.SeedKey == topicKey).Select(t => t.Id).FirstAsync(ct);
        }
    }

    private static void CopySeedValues(JsonNode row, Entity source, Entity target)
    {
        var props = target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var name in row.AsObject().Select(p => p.Key).Where(k => !Protected.Contains(k)))
        {
            var prop = props.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (prop is { CanWrite: true })
            {
                prop.SetValue(target, prop.GetValue(source));
            }
        }

        if (source is PlanTask s && target is PlanTask t)
        {
            t.PhaseId = s.PhaseId;
        }
        else if (source is ExitCriterion se && target is ExitCriterion te)
        {
            te.PhaseId = se.PhaseId;
        }
        else if (source is DsaProblem sp && target is DsaProblem tp)
        {
            tp.TopicId = sp.TopicId;
        }
    }

    private async Task<HashSet<string>> ExistingKeysAsync(Type type, CancellationToken ct) =>
        (await AllSeededAsync(type, ct)).Keys.ToHashSet(StringComparer.Ordinal);

    private async Task<Dictionary<string, Entity>> AllSeededAsync(Type type, CancellationToken ct)
    {
        var method = typeof(Seeder).GetMethod(nameof(AllSeededCore), BindingFlags.NonPublic | BindingFlags.Instance)!.MakeGenericMethod(type);
        return await (Task<Dictionary<string, Entity>>)method.Invoke(this, [ct])!;
    }

    private async Task<Dictionary<string, Entity>> AllSeededCore<T>(CancellationToken ct) where T : Entity, ISeedable =>
        (await db.Set<T>().IgnoreQueryFilters().Where(e => e.SeedKey != null).ToListAsync(ct))
        .ToDictionary(e => e.SeedKey!, e => (Entity)e, StringComparer.Ordinal);
}
