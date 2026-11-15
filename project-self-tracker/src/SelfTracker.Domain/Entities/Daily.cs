using SelfTracker.Domain.Common;

namespace SelfTracker.Domain.Entities;

public class DailyLog : Entity
{
    public DateOnly Date { get; set; }
    /// <summary>Null means In progress (spec v1.1, 4.3).</summary>
    public DayType? DayType { get; set; }
    public bool IsTravelDay { get; set; }
    public bool LoggedLate { get; set; }
    public int? Energy { get; set; }
    public decimal? SleepHours { get; set; }
    public TimeOnly? WakeTime { get; set; }
    public TimeOnly? LightsOutTime { get; set; }
    public string? DsaLine { get; set; }
    public string? CoreLine { get; set; }
    public string? BuildLine { get; set; }
    public string? TomorrowDsa { get; set; }
    public string? TomorrowBuild { get; set; }
    public string? NotesMd { get; set; }

    // Five-minute start ritual (Appendix L.3)
    public bool RitualOpenTracker { get; set; }
    public bool RitualReadWeekTarget { get; set; }
    public bool RitualWriteTargets { get; set; }
    public bool RitualPhoneAway { get; set; }
    public bool RitualStartTimer { get; set; }

    // Close day
    public bool CloseUpdateTracker { get; set; }
    public bool CloseTomorrowTargets { get; set; }
    public bool ClosePrep { get; set; }
    public bool CloseScreensOff { get; set; }

    /// <summary>Optional ticks of schedule blocks (comma-separated ScheduleBlock ids).</summary>
    public string? BlocksDoneCsv { get; set; }

    public GymStatus? GymStatus { get; set; }
    public string? GymSession { get; set; }
    public string? GymNote { get; set; }

    public List<DailyTick> Ticks { get; set; } = [];
    public List<TimeEntry> TimeEntries { get; set; } = [];
}

/// <summary>Core and Build ticks. The DSA tick is derived, never stored (BR-06).</summary>
public class DailyTick : Entity
{
    public int DailyLogId { get; set; }
    public DailyLog? DailyLog { get; set; }
    public TickType Type { get; set; }
    public string EvidenceNote { get; set; } = "";
    public string? LinkedEntityType { get; set; }
    public int? LinkedEntityId { get; set; }
    public string? EvidenceUrl { get; set; }
}

public class TimeEntry : Entity
{
    public DateOnly Date { get; set; }
    public int? DailyLogId { get; set; }
    public DailyLog? DailyLog { get; set; }
    public TimeCategory Category { get; set; }
    public int Minutes { get; set; }
    public string? Note { get; set; }
    public TimeSource Source { get; set; }
    public string? SourceEntityType { get; set; }
    public int? SourceEntityId { get; set; }
}
