using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SelfTracker.Application.Abstractions;
using SelfTracker.Domain.Common;
using SelfTracker.Domain.Entities;

namespace SelfTracker.Application.Features;

public sealed record SettingsDto(
    bool SetupCompleted,
    DateOnly? PlanStartDate,
    string TimeZone,
    DdiaEdition DdiaEdition,
    DateOnly? DdiaEditionConfirmedOn,
    int DsaUnaidedMinMinutes,
    string ReviewIntervalsCsv,
    int HighConfidenceReviewDays,
    DateOnly? TravelAnchorFriday,
    bool TravelEnabled,
    string FileRoot,
    int MaxUploadMb,
    int TrashRetentionDays,
    bool NightlyBackupEnabled,
    bool AllowDangerousExtensions,
    DateTimeOffset? LastBackupAt,
    string RowVersion)
{
    public static SettingsDto From(AppSettings s) => new(
        s.SetupCompleted, s.PlanStartDate, s.TimeZone, s.DdiaEdition, s.DdiaEditionConfirmedOn, s.DsaUnaidedMinMinutes,
        s.ReviewIntervalsCsv, s.HighConfidenceReviewDays, s.TravelAnchorFriday, s.TravelEnabled, s.FileRoot, s.MaxUploadMb,
        s.TrashRetentionDays, s.NightlyBackupEnabled, s.AllowDangerousExtensions, s.LastBackupAt,
        s.RowVersion.Length == 0 ? "" : s.Version());
}

/// <summary>Editable settings. FileRoot is chosen at setup and is read-only afterwards (spec v1.1, 4.1).</summary>
public sealed record UpdateSettingsRequest(
    [property: Required] DateOnly? PlanStartDate,
    [property: Required, StringLength(64)] string TimeZone,
    [property: Range(5, 120)] int DsaUnaidedMinMinutes,
    [property: Required, RegularExpression(@"^\d{1,3}(,\d{1,3}){0,9}$")] string ReviewIntervalsCsv,
    [property: Range(1, 365)] int HighConfidenceReviewDays,
    DateOnly? TravelAnchorFriday,
    bool TravelEnabled,
    [property: Range(1, 4096)] int MaxUploadMb,
    [property: Range(1, 365)] int TrashRetentionDays,
    bool NightlyBackupEnabled,
    bool AllowDangerousExtensions,
    string? RowVersion) : IVersioned;

public sealed record SetupRequest(
    [property: Required] DateOnly? PlanStartDate,
    DdiaEdition DdiaEdition,
    DateOnly? TravelAnchorFriday,
    bool TravelEnabled,
    [property: StringLength(400)] string? FileRoot,
    [property: StringLength(64)] string? TimeZone);

public sealed record ConfirmDdiaEditionRequest(DdiaEdition Edition);

public sealed record GetSettings;

public sealed class GetSettingsHandler(PlanContext plan) : IHandler<GetSettings, SettingsDto>
{
    public async Task<SettingsDto> HandleAsync(GetSettings request, CancellationToken ct) =>
        SettingsDto.From(await plan.GetSettingsAsync(ct));
}

public static class SettingsRules
{
    public static void ValidateCommon(DateOnly? travelAnchor, string timeZone)
    {
        if (travelAnchor is { } anchor && anchor.DayOfWeek != DayOfWeek.Friday)
        {
            throw RuleViolationException.For("travelAnchorFriday", "The travel anchor must be a Friday.");
        }

        if (PlanContext.ResolveZone(timeZone) == TimeZoneInfo.Utc && timeZone is not ("UTC" or "Etc/UTC"))
        {
            throw RuleViolationException.For("timeZone", $"Unknown time zone '{timeZone}'.");
        }
    }
}

public sealed class UpdateSettingsHandler(IAppDbContext db, PlanContext plan) : IHandler<UpdateSettingsRequest, SettingsDto>
{
    public async Task<SettingsDto> HandleAsync(UpdateSettingsRequest r, CancellationToken ct)
    {
        var s = await db.AppSettings.OrderBy(x => x.Id).FirstOrDefaultAsync(ct)
            ?? throw new ConflictException("Run first-run setup first.");
        db.ExpectVersion(s, r.RowVersion);
        SettingsRules.ValidateCommon(r.TravelAnchorFriday, r.TimeZone);
        s.PlanStartDate = r.PlanStartDate;
        s.TimeZone = r.TimeZone;
        s.DsaUnaidedMinMinutes = r.DsaUnaidedMinMinutes;
        s.ReviewIntervalsCsv = r.ReviewIntervalsCsv;
        s.HighConfidenceReviewDays = r.HighConfidenceReviewDays;
        s.TravelAnchorFriday = r.TravelAnchorFriday;
        s.TravelEnabled = r.TravelEnabled && r.TravelAnchorFriday is not null;
        s.MaxUploadMb = r.MaxUploadMb;
        s.TrashRetentionDays = r.TrashRetentionDays;
        s.NightlyBackupEnabled = r.NightlyBackupEnabled;
        s.AllowDangerousExtensions = r.AllowDangerousExtensions;
        await db.SaveChangesAsync(ct);
        plan.Invalidate();
        return SettingsDto.From(s);
    }
}

public sealed class SetupHandler(IAppDbContext db, PlanContext plan, IClock clock) : IHandler<SetupRequest, SettingsDto>
{
    public async Task<SettingsDto> HandleAsync(SetupRequest r, CancellationToken ct)
    {
        var s = await db.AppSettings.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (s is { SetupCompleted: true })
        {
            throw new ConflictException("Setup has already been completed. Change values on the Settings page.");
        }

        var tz = string.IsNullOrWhiteSpace(r.TimeZone) ? "Asia/Kolkata" : r.TimeZone;
        SettingsRules.ValidateCommon(r.TravelAnchorFriday, tz);
        if (s is null)
        {
            s = new AppSettings();
            db.AppSettings.Add(s);
        }

        s.SetupCompleted = true;
        s.PlanStartDate = r.PlanStartDate;
        s.TimeZone = tz;
        s.DdiaEdition = r.DdiaEdition;
        s.DdiaEditionConfirmedOn = r.DdiaEdition == DdiaEdition.Unconfirmed
            ? null
            : PlanContext.TodayIn(clock.UtcNow, PlanContext.ResolveZone(tz));
        s.TravelAnchorFriday = r.TravelAnchorFriday;
        s.TravelEnabled = r.TravelEnabled && r.TravelAnchorFriday is not null;
        if (!string.IsNullOrWhiteSpace(r.FileRoot))
        {
            s.FileRoot = r.FileRoot.Trim();
        }

        await db.SaveChangesAsync(ct);
        plan.Invalidate();
        return SettingsDto.From(s);
    }
}

public sealed class ConfirmDdiaEditionHandler(IAppDbContext db, PlanContext plan, IClock clock) : IHandler<ConfirmDdiaEditionRequest, SettingsDto>
{
    public async Task<SettingsDto> HandleAsync(ConfirmDdiaEditionRequest r, CancellationToken ct)
    {
        if (r.Edition == DdiaEdition.Unconfirmed)
        {
            throw RuleViolationException.For("edition", "Choose First or Second.");
        }

        var s = await db.AppSettings.OrderBy(x => x.Id).FirstOrDefaultAsync(ct)
            ?? throw new ConflictException("Run first-run setup first.");
        s.DdiaEdition = r.Edition;
        s.DdiaEditionConfirmedOn = PlanContext.TodayIn(clock.UtcNow, PlanContext.ResolveZone(s.TimeZone));
        await db.SaveChangesAsync(ct);
        plan.Invalidate();
        return SettingsDto.From(s);
    }
}
