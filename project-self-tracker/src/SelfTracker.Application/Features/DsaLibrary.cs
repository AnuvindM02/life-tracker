using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Entities;
using SelfTracker.Domain.Services;

namespace SelfTracker.Application.Features;

// ---- Topics ----

public sealed record DsaTopicDto(int Id, string Name, int SortOrder, bool IsDeferred, int ProblemCount, string RowVersion);

public sealed record DsaTopicInput([property: Required, StringLength(80)] string Name, int SortOrder, bool IsDeferred, string? RowVersion) : IVersioned;

public sealed class DsaTopicSpec(IAppDbContext db) : ICrudSpec<DsaTopic, DsaTopicDto, DsaTopicInput>
{
    public string Name => "DSA topic";

    public string DefaultSort => "sortOrder";

    public Expression<Func<DsaTopic, DsaTopicDto>> Projection =>
        t => new DsaTopicDto(t.Id, t.Name, t.SortOrder, t.IsDeferred, t.Problems.Count(p => !p.IsDeleted), Convert.ToBase64String(t.RowVersion));

    public async Task ValidateAsync(DsaTopicInput input, DsaTopic? existing, CancellationToken ct)
    {
        var name = input.Name.Trim();
        if (await db.DsaTopics.AnyAsync(t => t.Name == name && t.Id != (existing == null ? 0 : existing.Id), ct))
        {
            throw RuleViolationException.For("name", $"A topic called '{name}' already exists.");
        }
    }

    public async Task ValidateDeleteAsync(DsaTopic entity, CancellationToken ct)
    {
        if (await db.DsaProblems.AnyAsync(p => p.TopicId == entity.Id, ct))
        {
            throw new ConflictException("Move or delete this topic's problems first, or mark the topic as deferred.");
        }
    }

    public void Apply(DsaTopicInput input, DsaTopic entity)
    {
        entity.Name = input.Name.Trim();
        entity.SortOrder = input.SortOrder;
        entity.IsDeferred = input.IsDeferred;
    }
}

// ---- Problems ----

public sealed record DsaProblemDto(
    int Id,
    int TopicId,
    string TopicName,
    string Title,
    string? Url,
    Difficulty? Difficulty,
    int? PlanWeek,
    int SortOrder,
    ProblemStatus Status,
    int AttemptCount,
    DateOnly? LastAttemptOn,
    int? LastConfidence,
    DateOnly? NextReviewDue,
    string? SeedKey,
    string RowVersion);

public sealed record DsaProblemInput(
    int TopicId,
    [property: Required, StringLength(200)] string Title,
    [property: StringLength(500)] string? Url,
    Difficulty? Difficulty,
    [property: Range(1, 52)] int? PlanWeek,
    int SortOrder,
    ProblemStatus Status,
    string? RowVersion) : IVersioned;

public sealed class DsaProblemSpec(IAppDbContext db) : ICrudSpec<DsaProblem, DsaProblemDto, DsaProblemInput>
{
    public string Name => "DSA problem";

    public string DefaultSort => "sortOrder";

    public Expression<Func<DsaProblem, DsaProblemDto>> Projection => DsaProjections.Problem(db);

    public IQueryable<DsaProblem> Filter(IQueryable<DsaProblem> query, IReadOnlyDictionary<string, string?> filters)
    {
        if (filters.TryGetValue("topic", out var t) && int.TryParse(t, out var topicId))
        {
            query = query.Where(p => p.TopicId == topicId);
        }

        if (filters.TryGetValue("status", out var s) && Enum.TryParse<ProblemStatus>(s, true, out var status))
        {
            query = query.Where(p => p.Status == status);
        }

        if (filters.TryGetValue("week", out var w) && int.TryParse(w, out var week))
        {
            query = query.Where(p => p.PlanWeek == week);
        }

        if (filters.TryGetValue("q", out var q) && !string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(p => p.Title.Contains(q));
        }

        return query;
    }

    public async Task ValidateAsync(DsaProblemInput input, DsaProblem? existing, CancellationToken ct)
    {
        if (!await db.DsaTopics.AnyAsync(t => t.Id == input.TopicId, ct))
        {
            throw RuleViolationException.For("topicId", "Choose an existing topic.");
        }

        if (!string.IsNullOrWhiteSpace(input.Url) && !(Uri.TryCreate(input.Url, UriKind.Absolute, out var u) && u.Scheme is "http" or "https"))
        {
            throw RuleViolationException.For("url", "URL must start with http:// or https://.");
        }
    }

    public void Apply(DsaProblemInput input, DsaProblem entity)
    {
        entity.TopicId = input.TopicId;
        entity.Title = input.Title.Trim();
        entity.Url = string.IsNullOrWhiteSpace(input.Url) ? null : input.Url.Trim();
        entity.Difficulty = input.Difficulty;
        entity.PlanWeek = input.PlanWeek;
        entity.SortOrder = input.SortOrder;
        entity.Status = input.Status;
    }
}

public static class DsaProjections
{
    public static Expression<Func<DsaProblem, DsaProblemDto>> Problem(IAppDbContext db) => p => new DsaProblemDto(
        p.Id,
        p.TopicId,
        p.Topic!.Name,
        p.Title,
        p.Url,
        p.Difficulty,
        p.PlanWeek,
        p.SortOrder,
        p.Status,
        db.DsaAttempts.Count(a => a.ProblemId == p.Id),
        db.DsaAttempts.Where(a => a.ProblemId == p.Id).Max(a => (DateOnly?)a.Date),
        db.DsaAttempts.Where(a => a.ProblemId == p.Id).OrderByDescending(a => a.Date).ThenByDescending(a => a.Id).Select(a => (int?)a.Confidence).FirstOrDefault(),
        db.DsaReviewItems.Where(r => r.ProblemId == p.Id && r.Status == ReviewStatus.Pending).Min(r => (DateOnly?)r.DueDate),
        p.SeedKey,
        Convert.ToBase64String(p.RowVersion));
}

// ---- CSV import (title, topic, url, difficulty, week) ----

public sealed record ImportProblemsRequest([property: Required] string Csv, bool DryRun);

public sealed record ImportProblemsResult(bool DryRun, int Created, int Skipped, IReadOnlyList<string> TopicsCreated, IReadOnlyList<string> Errors);

public sealed class ImportProblemsHandler(IAppDbContext db) : IHandler<ImportProblemsRequest, ImportProblemsResult>
{
    public async Task<ImportProblemsResult> HandleAsync(ImportProblemsRequest r, CancellationToken ct)
    {
        var rows = Csv.Parse(r.Csv);
        if (rows.Count == 0)
        {
            return new(r.DryRun, 0, 0, [], ["The CSV is empty."]);
        }

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(string name) => header.IndexOf(name);
        if (Col("title") < 0 || Col("topic") < 0)
        {
            return new(r.DryRun, 0, 0, [], ["The first row must be a header with at least: title, topic (optional: url, difficulty, week)."]);
        }

        var topics = await db.DsaTopics.ToListAsync(ct);
        var existing = (await db.DsaProblems.Select(p => new { p.TopicId, p.Title }).ToListAsync(ct))
            .Select(p => (p.TopicId, p.Title.ToLowerInvariant())).ToHashSet();
        var maxSort = await db.DsaProblems.MaxAsync(p => (int?)p.SortOrder, ct) ?? 0;
        var errors = new List<string>();
        var newTopics = new List<string>();
        int created = 0, skipped = 0;

        string Cell(List<string> row, string name) => Col(name) is var i and >= 0 && i < row.Count ? row[i].Trim() : "";

        for (var line = 1; line < rows.Count; line++)
        {
            var row = rows[line];
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var title = Cell(row, "title");
            var topicName = Cell(row, "topic");
            if (title.Length is 0 or > 200 || topicName.Length is 0 or > 80)
            {
                errors.Add($"Row {line + 1}: title and topic are required (title up to 200, topic up to 80 characters).");
                continue;
            }

            Difficulty? difficulty = null;
            if (Cell(row, "difficulty") is { Length: > 0 } d)
            {
                if (!Enum.TryParse<Difficulty>(d, true, out var parsed))
                {
                    errors.Add($"Row {line + 1}: difficulty must be Easy, Medium or Hard.");
                    continue;
                }

                difficulty = parsed;
            }

            int? week = null;
            if (Cell(row, "week") is { Length: > 0 } w)
            {
                if (!int.TryParse(w, out var parsedWeek) || parsedWeek is < 1 or > 52)
                {
                    errors.Add($"Row {line + 1}: week must be 1-52.");
                    continue;
                }

                week = parsedWeek;
            }

            var url = Cell(row, "url");
            if (url.Length > 0 && !(Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme is "http" or "https"))
            {
                errors.Add($"Row {line + 1}: url must start with http:// or https://.");
                continue;
            }

            var topic = topics.FirstOrDefault(t => string.Equals(t.Name, topicName, StringComparison.OrdinalIgnoreCase));
            if (topic is null)
            {
                topic = new DsaTopic { Name = topicName, SortOrder = (topics.Max(t => (int?)t.SortOrder) ?? 0) + 10 };
                topics.Add(topic);
                newTopics.Add(topicName);
                db.DsaTopics.Add(topic);
            }

            if (topic.Id != 0 && existing.Contains((topic.Id, title.ToLowerInvariant())))
            {
                skipped++;
                continue;
            }

            db.DsaProblems.Add(new DsaProblem { Topic = topic, Title = title, Url = url.Length > 0 ? url : null, Difficulty = difficulty, PlanWeek = week, SortOrder = maxSort += 10 });
            created++;
        }

        if (!r.DryRun && errors.Count == 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return new(r.DryRun || errors.Count > 0, errors.Count == 0 ? created : 0, skipped, newTopics, errors);
    }
}

/// <summary>Minimal RFC 4180 CSV parser (quoted fields, doubled quotes, CRLF/LF).</summary>
public static class Csv
{
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = [];
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
