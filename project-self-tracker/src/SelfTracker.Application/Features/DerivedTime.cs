using Microsoft.EntityFrameworkCore;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;

namespace SelfTracker.Application.Features;

/// <summary>
/// BR-19: a source record owns at most one derived time entry. Sync creates, moves, updates or removes it;
/// the entry is attached to the date's daily log (created In progress if needed). Callers save.
/// </summary>
public sealed class DerivedTime(IAppDbContext db, DailyLogService logs, IClock clock)
{
    public async Task SyncAsync(string sourceType, int sourceId, DateOnly? date, int minutes, TimeCategory category, string note, CancellationToken ct)
    {
        var entry = await db.TimeEntries.FirstOrDefaultAsync(t => t.SourceEntityType == sourceType && t.SourceEntityId == sourceId, ct);
        if (date is null || minutes <= 0)
        {
            if (entry is not null)
            {
                db.TimeEntries.Remove(entry);
            }

            return;
        }

        var log = await logs.EnsureAsync(date.Value, ct);
        entry ??= db.TimeEntries.Add(new TimeEntry { Source = TimeSource.Derived, SourceEntityType = sourceType, SourceEntityId = sourceId }).Entity;
        entry.Date = date.Value;
        entry.DailyLogId = log.Id;
        entry.Category = category;
        entry.Minutes = minutes;
        entry.Note = note.Length > 500 ? note[..500] : note;
    }

    /// <summary>Soft-delete with the source's timestamp so a Trash restore of the source brings it back.</summary>
    public async Task SoftDeleteAsync(string sourceType, int sourceId, DateTimeOffset? stamp, CancellationToken ct)
    {
        foreach (var te in await db.TimeEntries.Where(t => t.SourceEntityType == sourceType && t.SourceEntityId == sourceId).ToListAsync(ct))
        {
            te.SoftDelete(stamp ?? clock.UtcNow);
        }
    }
}
