using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;

namespace SelfTracker.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, IClock clock) : DbContext(options), IAppDbContext
{
    /// <summary>True while the seeder writes, so seeded rows are not flagged as user-modified (BR-15).</summary>
    public bool SeedingMode { get; set; }

    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<PauseRange> PauseRanges => Set<PauseRange>();
    public DbSet<TravelOverride> TravelOverrides => Set<TravelOverride>();
    public DbSet<Phase> Phases => Set<Phase>();
    public DbSet<PlanTask> PlanTasks => Set<PlanTask>();
    public DbSet<Checkpoint> Checkpoints => Set<Checkpoint>();
    public DbSet<ScheduleBlock> ScheduleBlocks => Set<ScheduleBlock>();
    public DbSet<GymPlanDay> GymPlanDays => Set<GymPlanDay>();
    public DbSet<DailyLog> DailyLogs => Set<DailyLog>();
    public DbSet<DailyTick> DailyTicks => Set<DailyTick>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<FileAsset> FileAssets => Set<FileAsset>();
    public DbSet<FileLink> FileLinks => Set<FileLink>();
    public DbSet<DsaTopic> DsaTopics => Set<DsaTopic>();
    public DbSet<DsaProblem> DsaProblems => Set<DsaProblem>();
    public DbSet<DsaAttempt> DsaAttempts => Set<DsaAttempt>();
    public DbSet<DsaReviewItem> DsaReviewItems => Set<DsaReviewItem>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<DsaAttemptTag> DsaAttemptTags => Set<DsaAttemptTag>();
    public DbSet<DdiaChapter> DdiaChapters => Set<DdiaChapter>();
    public DbSet<ReadingLog> ReadingLogs => Set<ReadingLog>();
    public DbSet<DdiaNote> DdiaNotes => Set<DdiaNote>();
    public DbSet<InterviewQa> InterviewQas => Set<InterviewQa>();
    public DbSet<DesignDrill> DesignDrills => Set<DesignDrill>();
    public DbSet<ArticulationLog> ArticulationLogs => Set<ArticulationLog>();
    public DbSet<ArticulationPrompt> ArticulationPrompts => Set<ArticulationPrompt>();
    public DbSet<ExitCriterion> ExitCriteria => Set<ExitCriterion>();
    public DbSet<ReplanEvent> ReplanEvents => Set<ReplanEvent>();
    public DbSet<WeeklyReview> WeeklyReviews => Set<WeeklyReview>();
    public DbSet<MonthlyReview> MonthlyReviews => Set<MonthlyReview>();
    public DbSet<SomedayItem> SomedayItems => Set<SomedayItem>();
    public DbSet<WeeklyHourTarget> WeeklyHourTargets => Set<WeeklyHourTarget>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Enums stored as strings (spec section 5).
        builder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);
        builder.Properties<string>().HaveMaxLength(1000);
        builder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        foreach (var et in b.Model.GetEntityTypes().Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)).ToList())
        {
            var builder = b.Entity(et.ClrType);
            builder.Property(nameof(Entity.RowVersion)).IsRowVersion();
            builder.HasQueryFilter(SoftDeleteFilter(et.ClrType));
            if (typeof(ISeedable).IsAssignableFrom(et.ClrType))
            {
                builder.Property(nameof(ISeedable.SeedKey)).HasMaxLength(80);
                builder.HasIndex(nameof(ISeedable.SeedKey)).IsUnique().HasFilter("[SeedKey] IS NOT NULL");
            }
        }

        b.Entity<AppSettings>(e =>
        {
            e.Property(x => x.FileRoot).HasMaxLength(400);
            e.Property(x => x.TimeZone).HasMaxLength(64);
            e.Property(x => x.ReviewIntervalsCsv).HasMaxLength(60);
        });

        b.Entity<PauseRange>().HasIndex(x => new { x.StartDate, x.EndDate });
        b.Entity<TravelOverride>().HasIndex(x => x.WeekFriday).IsUnique().HasFilter("[IsDeleted] = 0");

        b.Entity<Phase>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80);
            e.HasIndex(x => x.Number);
        });

        b.Entity<PlanTask>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.NotesMd).HasMaxLength(-1);
            e.Property(x => x.Description).HasMaxLength(-1);
            e.Property(x => x.EvidenceUrl).HasMaxLength(500);
            e.HasOne(x => x.Phase).WithMany().HasForeignKey(x => x.PhaseId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.DayFrom, x.DayTo });
            e.HasIndex(x => x.PlanWeek);
        });

        b.Entity<Checkpoint>(e =>
        {
            e.Property(x => x.AnswerMd).HasMaxLength(-1);
            e.HasIndex(x => x.PlanDay);
        });

        b.Entity<ScheduleBlock>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(120);
            e.Property(x => x.TimeLabel).HasMaxLength(60);
            e.HasIndex(x => new { x.SchedulePhase, x.SortOrder });
        });

        b.Entity<GymPlanDay>(e =>
        {
            e.Property(x => x.SessionName).HasMaxLength(120);
            e.Property(x => x.DayOfWeek).HasConversion<string>().HasMaxLength(10);
        });

        b.Entity<DailyLog>(e =>
        {
            e.HasIndex(x => x.Date).IsUnique().HasFilter("[IsDeleted] = 0");
            e.Property(x => x.NotesMd).HasMaxLength(-1);
            e.Property(x => x.GymNote).HasMaxLength(2000);
            e.Property(x => x.BlocksDoneCsv).HasMaxLength(400);
            e.Property(x => x.SleepHours).HasPrecision(4, 2);
            e.HasMany(x => x.Ticks).WithOne(x => x.DailyLog).HasForeignKey(x => x.DailyLogId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.TimeEntries).WithOne(x => x.DailyLog).HasForeignKey(x => x.DailyLogId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DailyTick>(e =>
        {
            e.Property(x => x.EvidenceNote).HasMaxLength(2000);
            e.Property(x => x.EvidenceUrl).HasMaxLength(500);
            e.Property(x => x.LinkedEntityType).HasMaxLength(40);
            e.HasIndex(x => new { x.DailyLogId, x.Type }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        b.Entity<TimeEntry>(e =>
        {
            e.HasIndex(x => x.Date);
            e.HasIndex(x => new { x.SourceEntityType, x.SourceEntityId });
            e.Property(x => x.SourceEntityType).HasMaxLength(40);
        });

        b.Entity<FileAsset>(e =>
        {
            e.Property(x => x.RelativePath).HasMaxLength(300);
            e.Property(x => x.OriginalName).HasMaxLength(260);
            e.Property(x => x.DisplayTitle).HasMaxLength(260);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.ContentType).HasMaxLength(150);
            e.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength();
            e.Property(x => x.Tags).HasMaxLength(500);
            e.HasIndex(x => x.Sha256);
            e.HasIndex(x => x.StorageKey).IsUnique();
            e.HasMany(x => x.Links).WithOne(x => x.FileAsset).HasForeignKey(x => x.FileAssetId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DsaTopic>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80);
            e.HasMany(x => x.Problems).WithOne(x => x.Topic).HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<DsaProblem>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Url).HasMaxLength(500);
            e.HasIndex(x => new { x.TopicId, x.SortOrder });
            e.HasIndex(x => x.PlanWeek);
        });

        b.Entity<DsaAttempt>(e =>
        {
            e.Property(x => x.TitleFreeText).HasMaxLength(200);
            e.Property(x => x.Pattern).HasMaxLength(200);
            e.Property(x => x.KeyInsight).HasMaxLength(2000);
            e.Property(x => x.WhatIMissed).HasMaxLength(2000);
            e.Property(x => x.TimeComplexity).HasMaxLength(60);
            e.Property(x => x.SpaceComplexity).HasMaxLength(60);
            e.Property(x => x.CodeMd).HasMaxLength(-1);
            e.Property(x => x.Company).HasMaxLength(120);
            e.HasOne(x => x.Problem).WithMany().HasForeignKey(x => x.ProblemId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Date);
            e.HasIndex(x => x.ProblemId);
        });

        b.Entity<DsaReviewItem>(e =>
        {
            e.HasOne(x => x.Problem).WithMany().HasForeignKey(x => x.ProblemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SourceAttempt).WithMany().HasForeignKey(x => x.SourceAttemptId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.SkipReason).HasMaxLength(500);
            e.Property(x => x.RescheduleReason).HasMaxLength(500);
            e.HasIndex(x => new { x.DueDate, x.Status });
            e.HasIndex(x => new { x.ProblemId, x.Status });
        });

        b.Entity<Tag>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(60);
            e.HasIndex(x => new { x.Kind, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        b.Entity<DsaAttemptTag>(e =>
        {
            e.HasKey(x => new { x.DsaAttemptId, x.TagId });
            e.HasOne(x => x.DsaAttempt).WithMany(x => x.Tags).HasForeignKey(x => x.DsaAttemptId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Tag).WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Restrict);
            // Both ends are soft-deletable; hide join rows whose attempt is deleted.
            e.HasQueryFilter(x => !x.DsaAttempt!.IsDeleted);
        });

        b.Entity<DdiaChapter>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Topics).HasMaxLength(2000);
            e.Property(x => x.AskOpsConnection).HasMaxLength(2000);
            e.HasIndex(x => new { x.Edition, x.Number });
        });

        b.Entity<ReadingLog>(e =>
        {
            e.HasOne(x => x.Chapter).WithMany().HasForeignKey(x => x.ChapterId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasIndex(x => x.Date);
            e.HasIndex(x => x.ChapterId);
        });

        b.Entity<DdiaNote>(e =>
        {
            e.HasOne(x => x.Chapter).WithMany().HasForeignKey(x => x.ChapterId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ChapterId).IsUnique().HasFilter("[IsDeleted] = 0");
            foreach (var p in new[] { nameof(DdiaNote.KeyIdea1), nameof(DdiaNote.KeyIdea2), nameof(DdiaNote.KeyIdea3), nameof(DdiaNote.KeyIdea4), nameof(DdiaNote.KeyIdea5), nameof(DdiaNote.AskOpsDecision) })
            {
                e.Property(p).HasMaxLength(2000);
            }

            e.Property(x => x.TradeOffsMd).HasMaxLength(-1);
            e.Property(x => x.FailureModesMd).HasMaxLength(-1);
            e.Property(x => x.ExplanationNotes).HasMaxLength(2000);
            e.HasMany(x => x.Questions).WithOne(x => x.DdiaNote).HasForeignKey(x => x.DdiaNoteId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<InterviewQa>(e =>
        {
            e.Property(x => x.Question).HasMaxLength(500);
            e.Property(x => x.Answer60sMd).HasMaxLength(-1);
        });

        b.Entity<DesignDrill>(e =>
        {
            e.Property(x => x.Prompt).HasMaxLength(200);
            e.Property(x => x.Group).HasMaxLength(20);
            foreach (var p in new[] { nameof(DesignDrill.ClarifyNotes), nameof(DesignDrill.EstimateNotes), nameof(DesignDrill.ApiDataNotes), nameof(DesignDrill.HighLevelNotes), nameof(DesignDrill.DeepDiveNotes), nameof(DesignDrill.TradeOffNotes) })
            {
                e.Property(p).HasMaxLength(-1);
            }

            e.HasIndex(x => x.Number);
        });

        b.Entity<ArticulationLog>(e =>
        {
            e.Property(x => x.Prompt).HasMaxLength(500);
            e.Property(x => x.AnswerMd).HasMaxLength(-1);
            e.Property(x => x.TradeOffsMd).HasMaxLength(-1);
            e.Property(x => x.EvidenceMd).HasMaxLength(-1);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => x.Date);
        });

        b.Entity<ArticulationPrompt>().Property(x => x.Text).HasMaxLength(500);

        b.Entity<ExitCriterion>(e =>
        {
            e.HasOne(x => x.Phase).WithMany().HasForeignKey(x => x.PhaseId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Group).HasMaxLength(80);
            e.Property(x => x.EvidenceText).HasMaxLength(2000);
        });

        b.Entity<ReplanEvent>(e =>
        {
            e.HasOne(x => x.Checkpoint).WithMany().HasForeignKey(x => x.CheckpointId).OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.EvidenceMd).HasMaxLength(-1);
            e.Property(x => x.DecisionMd).HasMaxLength(-1);
            e.HasIndex(x => x.Date);
        });

        b.Entity<WeeklyReview>(e =>
        {
            e.HasIndex(x => x.WeekStart).IsUnique().HasFilter("[IsDeleted] = 0");
            e.Property(x => x.SnapshotJson).HasMaxLength(-1);
            e.Property(x => x.ShippedMd).HasMaxLength(-1);
            e.Property(x => x.BlockedMd).HasMaxLength(-1);
            e.Property(x => x.NextSliceMd).HasMaxLength(-1);
        });

        b.Entity<MonthlyReview>(e =>
        {
            e.HasIndex(x => x.Month).IsUnique().HasFilter("[IsDeleted] = 0");
            e.Property(x => x.PhaseProgressMd).HasMaxLength(-1);
            e.Property(x => x.MistakeThemesMd).HasMaxLength(-1);
            e.Property(x => x.SomedayTriageMd).HasMaxLength(-1);
        });

        b.Entity<SomedayItem>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.NotesMd).HasMaxLength(-1);
            e.HasIndex(x => x.Status);
        });

        b.Entity<WeeklyHourTarget>(e =>
        {
            e.Property(x => x.Stage).HasMaxLength(20);
            e.Property(x => x.Label).HasMaxLength(120);
            e.Property(x => x.Categories).HasMaxLength(200);
            e.Property(x => x.MinHours).HasPrecision(5, 2);
            e.Property(x => x.MaxHours).HasPrecision(5, 2);
        });

        b.Entity<FileLink>(e =>
        {
            e.Property(x => x.EntityType).HasMaxLength(40);
            e.Property(x => x.Label).HasMaxLength(120);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Stamp();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        Stamp();
        return base.SaveChanges();
    }

    private void Stamp()
    {
        var now = clock.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                if (!SeedingMode && entry.Entity is ISeedable { SeedKey: not null } seeded
                    && entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not (nameof(Entity.IsDeleted) or nameof(Entity.DeletedAt) or nameof(Entity.UpdatedAt))))
                {
                    seeded.IsUserModified = true;
                }
            }
        }
    }

    private static LambdaExpression SoftDeleteFilter(Type type)
    {
        var p = Expression.Parameter(type, "e");
        return Expression.Lambda(Expression.Not(Expression.Property(p, nameof(Entity.IsDeleted))), p);
    }
}
