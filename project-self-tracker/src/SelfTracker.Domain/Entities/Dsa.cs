using SelfTracker.Domain.Common;

namespace SelfTracker.Domain.Entities;

public enum ProblemStatus { NotStarted, Attempted, Solved, Deferred }

public enum Difficulty { Easy, Medium, Hard }

public enum AttemptType { New, ColdReview, Timed }

public enum HintLevel { None, Topic, Pattern, Solution }

public enum ReviewStatus { Pending, Done, Skipped, Superseded }

public enum TagKind { Mistake, Skill }

public class DsaTopic : SeedableEntity
{
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsDeferred { get; set; }
    public List<DsaProblem> Problems { get; set; } = [];
}

public class DsaProblem : SeedableEntity
{
    public int TopicId { get; set; }
    public DsaTopic? Topic { get; set; }
    public string Title { get; set; } = "";
    public string? Url { get; set; }
    public Difficulty? Difficulty { get; set; }
    public int? PlanWeek { get; set; }
    public int SortOrder { get; set; }
    public ProblemStatus Status { get; set; }
}

/// <summary>Template B mistake-log row plus the four-pass method (spec 4.4).</summary>
public class DsaAttempt : Entity
{
    public DateOnly Date { get; set; }
    public int? ProblemId { get; set; }
    public DsaProblem? Problem { get; set; }
    public string? TitleFreeText { get; set; }
    public AttemptType Type { get; set; }
    public int Pass1Min { get; set; }
    public HintLevel HintLevel { get; set; }
    public int Pass2Min { get; set; }
    public int Pass3Min { get; set; }
    public int TotalMin { get; set; }
    public bool SolvedUnaided { get; set; }
    public string? Pattern { get; set; }
    public string? KeyInsight { get; set; }
    public string? WhatIMissed { get; set; }
    public string? TimeComplexity { get; set; }
    public string? SpaceComplexity { get; set; }
    public int Confidence { get; set; }
    public string? CodeMd { get; set; }
    public string? Company { get; set; }
    public bool IsCounted { get; set; }
    public string? NotCountedReason { get; set; }
    /// <summary>The review item this cold review completed, if any.</summary>
    public int? ReviewItemId { get; set; }
    public List<DsaAttemptTag> Tags { get; set; } = [];
}

/// <summary>One step of a spaced-review ladder; the ladder is keyed by its source attempt (BR-09).</summary>
public class DsaReviewItem : Entity
{
    public int ProblemId { get; set; }
    public DsaProblem? Problem { get; set; }
    public int SourceAttemptId { get; set; }
    public DsaAttempt? SourceAttempt { get; set; }
    public DateOnly DueDate { get; set; }
    public int IntervalDays { get; set; }
    public ReviewStatus Status { get; set; }
    public int? CompletedAttemptId { get; set; }
    public string? SkipReason { get; set; }
    public string? RescheduleReason { get; set; }
}

public class Tag : Entity
{
    public string Name { get; set; } = "";
    public TagKind Kind { get; set; }
}

public class DsaAttemptTag
{
    public int DsaAttemptId { get; set; }
    public DsaAttempt? DsaAttempt { get; set; }
    public int TagId { get; set; }
    public Tag? Tag { get; set; }
}
