using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SelfTracker.Domain.Entities;

namespace SelfTracker.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>A tiny handler abstraction instead of MediatR (spec section 2).</summary>
public interface IHandler<in TIn, TOut>
{
    Task<TOut> HandleAsync(TIn request, CancellationToken ct);
}

/// <summary>The application's view of the database. No repository wrapper (spec section 2).</summary>
public interface IAppDbContext
{
    DbSet<AppSettings> AppSettings { get; }
    DbSet<PauseRange> PauseRanges { get; }
    DbSet<TravelOverride> TravelOverrides { get; }
    DbSet<Phase> Phases { get; }
    DbSet<PlanTask> PlanTasks { get; }
    DbSet<Checkpoint> Checkpoints { get; }
    DbSet<ScheduleBlock> ScheduleBlocks { get; }
    DbSet<GymPlanDay> GymPlanDays { get; }
    DbSet<DailyLog> DailyLogs { get; }
    DbSet<DailyTick> DailyTicks { get; }
    DbSet<TimeEntry> TimeEntries { get; }
    DbSet<FileAsset> FileAssets { get; }
    DbSet<FileLink> FileLinks { get; }
    DbSet<DsaTopic> DsaTopics { get; }
    DbSet<DsaProblem> DsaProblems { get; }
    DbSet<DsaAttempt> DsaAttempts { get; }
    DbSet<DsaReviewItem> DsaReviewItems { get; }
    DbSet<Tag> Tags { get; }
    DbSet<DsaAttemptTag> DsaAttemptTags { get; }
    DbSet<DdiaChapter> DdiaChapters { get; }
    DbSet<ReadingLog> ReadingLogs { get; }
    DbSet<DdiaNote> DdiaNotes { get; }
    DbSet<InterviewQa> InterviewQas { get; }
    DbSet<DesignDrill> DesignDrills { get; }
    DbSet<ArticulationLog> ArticulationLogs { get; }
    DbSet<ArticulationPrompt> ArticulationPrompts { get; }
    DbSet<ExitCriterion> ExitCriteria { get; }
    DbSet<ReplanEvent> ReplanEvents { get; }
    DbSet<WeeklyReview> WeeklyReviews { get; }
    DbSet<MonthlyReview> MonthlyReviews { get; }
    DbSet<SomedayItem> SomedayItems { get; }
    DbSet<WeeklyHourTarget> WeeklyHourTargets { get; }

    DbSet<T> Set<T>() where T : class;

    EntityEntry<T> Entry<T>(T entity) where T : class;

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record StoredFile(string TempPath, long SizeBytes, string Sha256, byte[] Head);

/// <summary>Disk storage under FileRoot. Paths passed in and out are relative to the root.</summary>
public interface IFileStorage
{
    string Root { get; }

    /// <summary>Stream to a temp file in the same volume while hashing; throws <see cref="FileTooLargeException"/> past the limit.</summary>
    Task<StoredFile> WriteTempAsync(Stream source, long maxBytes, CancellationToken ct);

    /// <summary>Atomically move a temp file into place at the relative path.</summary>
    void Commit(string tempPath, string relativePath);

    void DeleteTemp(string tempPath);

    Stream OpenRead(string relativePath);

    bool Exists(string relativePath);

    void Delete(string relativePath);

    long? FreeBytes();

    bool IsWritable();

    /// <summary>All stored files, as relative paths (excludes the temp folder).</summary>
    IEnumerable<string> EnumerateFiles();

    Task<string> ComputeSha256Async(string relativePath, CancellationToken ct);
}

public sealed record ListQuery(int Page = 1, int PageSize = 50, string? Sort = null)
{
    public const int MaxPageSize = 200;

    public int SafePage => Math.Max(1, Page);

    public int SafePageSize => Math.Clamp(PageSize, 1, MaxPageSize);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

// ---- Errors mapped to RFC 9457 ProblemDetails by the API ----

public abstract class AppException(string message) : Exception(message);

public sealed class NotFoundException(string what) : AppException($"{what} was not found.");

public sealed class ConflictException(string message) : AppException(message);

public sealed class BadRequestException(string message) : AppException(message);

public sealed class FileTooLargeException(long maxBytes) : AppException($"File is larger than the {maxBytes / (1024 * 1024)} MB limit.");

/// <summary>Business-rule violation (HTTP 422) with field errors.</summary>
public sealed class RuleViolationException(IDictionary<string, string[]> errors, string message = "One or more rules were not met.")
    : AppException(message)
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public static RuleViolationException For(string field, params string[] messages) =>
        new(new Dictionary<string, string[]> { [field] = messages });
}
