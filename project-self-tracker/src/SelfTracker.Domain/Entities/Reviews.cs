using SelfTracker.Domain.Common;

namespace SelfTracker.Domain.Entities;

public enum ReplanTrigger { SkillMismatch, Phase1Behind, LifeChange, PackageBlocker, Checkpoint }

public enum SomedayCategory { Tool, Course, Framework, ProjectIdea, Other }

public enum SomedayStatus { Parked, Promoted, Dropped }

/// <summary>Appendix G exit criteria (and the Day 270 readiness checklist), seeded per phase.</summary>
public class ExitCriterion : SeedableEntity
{
    public int PhaseId { get; set; }
    public Phase? Phase { get; set; }
    /// <summary>Optional sub-heading, e.g. "Day 270 readiness".</summary>
    public string? Group { get; set; }
    public string Text { get; set; } = "";
    public string? EvidenceHint { get; set; }
    public bool Done { get; set; }
    public DateOnly? DoneOn { get; set; }
    public string? EvidenceText { get; set; }
    public int SortOrder { get; set; }
}

public class ReplanEvent : Entity
{
    public DateOnly Date { get; set; }
    public ReplanTrigger Trigger { get; set; }
    public string? EvidenceMd { get; set; }
    public string? DecisionMd { get; set; }
    public int? CheckpointId { get; set; }
    public Checkpoint? Checkpoint { get; set; }
}

/// <summary>Template D (Sunday, 30 minutes). WeekStart is a Monday.</summary>
public class WeeklyReview : Entity
{
    public DateOnly WeekStart { get; set; }
    /// <summary>Computed facts at save time (JSON of the prefill).</summary>
    public string? SnapshotJson { get; set; }
    public int? Energy { get; set; }
    public int? Sleep { get; set; }
    public int? Mood { get; set; }
    public string? ShippedMd { get; set; }
    public string? BlockedMd { get; set; }
    public string? NextSliceMd { get; set; }
    public string? RecoveryNote { get; set; }
    public string? RepeatedRequirement { get; set; }
    public string? Start { get; set; }
    public string? Stop { get; set; }
    public string? Keep { get; set; }
}

public class MonthlyReview : Entity
{
    /// <summary>First day of the month.</summary>
    public DateOnly Month { get; set; }
    public string? PhaseProgressMd { get; set; }
    public string? MistakeThemesMd { get; set; }
    public int PostingsCaptured { get; set; }
    public bool LighterWeekNeeded { get; set; }
    public string? SomedayTriageMd { get; set; }
}

/// <summary>Replaces someday-list.md: new tools and course ideas wait here until the monthly review.</summary>
public class SomedayItem : Entity
{
    public string Title { get; set; } = "";
    public string? NotesMd { get; set; }
    public SomedayCategory Category { get; set; }
    public SomedayStatus Status { get; set; }
    public DateOnly? ReviewedOn { get; set; }
    public int? PromotedTaskId { get; set; }
}

/// <summary>Appendix J weekly hour targets. Stage is A, B, Phase1..Phase4 or Full (week 9+ totals).</summary>
public class WeeklyHourTarget : SeedableEntity
{
    public string Stage { get; set; } = "";
    public string Label { get; set; } = "";
    /// <summary>Comma-separated TimeCategory names; empty for a total.</summary>
    public string? Categories { get; set; }
    public decimal MinHours { get; set; }
    public decimal MaxHours { get; set; }
    public bool IsTotal { get; set; }
    public bool IsStretch { get; set; }
    public int SortOrder { get; set; }
}
