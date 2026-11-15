using SelfTracker.Domain.Common;

namespace SelfTracker.Domain.Entities;

public enum ChapterStatus { NotStarted, Reading, NoteDone, Explained }

public enum NoteStatus { Draft, Complete }

public enum DrillStatus { NotStarted, Written, SpokenMock, Reviewed }

public enum ArticulationKind { WhyXOverY, Explanation90, Other }

/// <summary>A DDIA chapter of one edition (Appendix D). Never mix edition numbers (spec 4.5).</summary>
public class DdiaChapter : SeedableEntity
{
    public DdiaEdition Edition { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public int PlannedWeekFrom { get; set; }
    public int PlannedWeekTo { get; set; }
    public string? Topics { get; set; }
    public string? AskOpsConnection { get; set; }
    public int? TotalPages { get; set; }
    /// <summary>Sum of the chapter's reading-log pages (maintained on write).</summary>
    public int PagesRead { get; set; }
    public ChapterStatus Status { get; set; }
    public DateOnly? StartedOn { get; set; }
    public DateOnly? FinishedOn { get; set; }
}

public class ReadingLog : Entity
{
    public DateOnly Date { get; set; }
    public int ChapterId { get; set; }
    public DdiaChapter? Chapter { get; set; }
    public int PagesRead { get; set; }
    public int Minutes { get; set; }
    public string? Note { get; set; }
}

/// <summary>Template C: one-page chapter note.</summary>
public class DdiaNote : Entity
{
    public int ChapterId { get; set; }
    public DdiaChapter? Chapter { get; set; }
    public string? KeyIdea1 { get; set; }
    public string? KeyIdea2 { get; set; }
    public string? KeyIdea3 { get; set; }
    public string? KeyIdea4 { get; set; }
    public string? KeyIdea5 { get; set; }
    public string? TradeOffsMd { get; set; }
    public string? FailureModesMd { get; set; }
    public string? AskOpsDecision { get; set; }
    /// <summary>90-second explanation self-rating (1-5).</summary>
    public int? ExplanationRating { get; set; }
    public string? ExplanationNotes { get; set; }
    public NoteStatus Status { get; set; }
    public List<InterviewQa> Questions { get; set; } = [];
}

public class InterviewQa : Entity
{
    public int DdiaNoteId { get; set; }
    public DdiaNote? DdiaNote { get; set; }
    public string Question { get; set; } = "";
    public string? Answer60sMd { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Appendix H system-design drill with the 45-minute framework.</summary>
public class DesignDrill : SeedableEntity
{
    public int Number { get; set; }
    public string Prompt { get; set; } = "";
    public string Group { get; set; } = "";
    public int? PlannedWeek { get; set; }
    public DrillStatus Status { get; set; }
    public string? ClarifyNotes { get; set; }
    public string? EstimateNotes { get; set; }
    public string? ApiDataNotes { get; set; }
    public string? HighLevelNotes { get; set; }
    public string? DeepDiveNotes { get; set; }
    public string? TradeOffNotes { get; set; }
    public bool SpokenMock { get; set; }
    public DateOnly? PracticedOn { get; set; }
    public int Minutes { get; set; }
    public DateOnly? ReviewedOn { get; set; }
}

public class ArticulationLog : Entity
{
    public DateOnly Date { get; set; }
    public ArticulationKind Kind { get; set; }
    public string Prompt { get; set; } = "";
    public string? AnswerMd { get; set; }
    public string? TradeOffsMd { get; set; }
    public string? EvidenceMd { get; set; }
    public int? SelfRating { get; set; }
    public string? Notes { get; set; }
    public int Minutes { get; set; }
}

/// <summary>Seeded recurring prompts (Appendix H articulation training); editable.</summary>
public class ArticulationPrompt : SeedableEntity
{
    public ArticulationKind Kind { get; set; }
    public string Text { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
