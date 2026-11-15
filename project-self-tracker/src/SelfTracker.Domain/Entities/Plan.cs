using SelfTracker.Domain.Common;

namespace SelfTracker.Domain.Entities;

/// <summary>Single settings row (spec 4.1).</summary>
public class AppSettings : Entity
{
    public bool SetupCompleted { get; set; }
    public DateOnly? PlanStartDate { get; set; }
    public string TimeZone { get; set; } = "Asia/Kolkata";
    public DdiaEdition DdiaEdition { get; set; } = DdiaEdition.Unconfirmed;
    public DateOnly? DdiaEditionConfirmedOn { get; set; }
    public int DsaUnaidedMinMinutes { get; set; } = 25;
    public string ReviewIntervalsCsv { get; set; } = "1,3,7,21";
    public int HighConfidenceReviewDays { get; set; } = 30;
    public DateOnly? TravelAnchorFriday { get; set; }
    public bool TravelEnabled { get; set; }
    public string FileRoot { get; set; } = "data/files";
    public int MaxUploadMb { get; set; } = 500;
    public int TrashRetentionDays { get; set; } = 30;
    public bool NightlyBackupEnabled { get; set; } = true;
    public bool AllowDangerousExtensions { get; set; }
    public DateTimeOffset? LastBackupAt { get; set; }
}

public class PauseRange : Entity
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public PauseReason Reason { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Per-week override of the alternate-week travel pattern (BR-04).</summary>
public class TravelOverride : Entity
{
    /// <summary>The Friday the (possible) trip starts.</summary>
    public DateOnly WeekFriday { get; set; }
    public bool IsTravel { get; set; }
    public string? Notes { get; set; }
}

public class Phase : SeedableEntity
{
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public int DayFrom { get; set; }
    public int DayTo { get; set; }
    public int WeekFrom { get; set; }
    public int WeekTo { get; set; }
    public string Objective { get; set; } = "";
    public string ExitEvidence { get; set; } = "";
}

public class PlanTask : SeedableEntity
{
    public int PhaseId { get; set; }
    public Phase? Phase { get; set; }
    public PlanTrack Track { get; set; }
    public PlanTaskKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? OutputRequired { get; set; }
    public string? Fallback { get; set; }
    public int DayFrom { get; set; }
    public int DayTo { get; set; }
    /// <summary>Plan week a derived DSA/DDIA task tracks (BR-18).</summary>
    public int? PlanWeek { get; set; }
    public PlanTaskStatus Status { get; set; }
    /// <summary>Manual Deferred/Skipped that overrides a derived status.</summary>
    public PlanTaskStatus? StatusOverride { get; set; }
    public DateOnly? CompletedOn { get; set; }
    public string? NotesMd { get; set; }
    public string? EvidenceUrl { get; set; }
    public int SortOrder { get; set; }
}

public class Checkpoint : SeedableEntity
{
    public int PlanDay { get; set; }
    public int? DayTo { get; set; }
    public string Title { get; set; } = "";
    public string Question { get; set; } = "";
    public string? AnswerMd { get; set; }
    public CheckpointOutcome? Outcome { get; set; }
    public DateOnly? AnsweredOn { get; set; }
}

public class ScheduleBlock : SeedableEntity
{
    public SchedulePhase SchedulePhase { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    /// <summary>Display label when the roadmap gives a compound time, for example "07:15 W4; 07:00 W5-8".</summary>
    public string? TimeLabel { get; set; }
    public string Title { get; set; } = "";
    public string Rule { get; set; } = "";
    public bool IsStretch { get; set; }
    public int SortOrder { get; set; }
}

public class GymPlanDay : SeedableEntity
{
    public SchedulePhase SchedulePhase { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public string SessionName { get; set; } = "";
    public bool IsOptional { get; set; }
    public string? Notes { get; set; }
}
