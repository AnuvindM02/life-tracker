using System.Text.RegularExpressions;
using SelfTracker.Domain.Common;

namespace SelfTracker.Domain.Services;

public sealed record TickEvidenceInput(
    TickType Type,
    string? EvidenceNote,
    string? EvidenceUrl,
    string? LinkedEntityType,
    int? LinkedEntityId,
    int AttachmentCount);

/// <summary>BR-06: what earns each tracker tick.</summary>
public static partial class TickRules
{
    public static readonly IReadOnlySet<string> CoreLinkTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DdiaNote", "DdiaChapter", "DesignDrill", "Adr", "DesignDoc", "ArticulationLog" };

    public static readonly IReadOnlySet<string> BuildLinkTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FlagshipSession", "Adr", "DesignDoc" };

    public const string DsaDerivedMessage = "The DSA tick is derived from logged attempts and cold reviews; it cannot be set by hand.";

    /// <summary>Returns validation errors; an empty list means the tick is backed by evidence.</summary>
    public static IReadOnlyList<string> Validate(TickEvidenceInput input)
    {
        if (input.Type == TickType.Dsa)
        {
            return [DsaDerivedMessage];
        }

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(input.EvidenceNote))
        {
            errors.Add("An evidence note is required: what did this block produce?");
        }

        if (!string.IsNullOrWhiteSpace(input.EvidenceUrl) && !IsUrlOrCommitHash(input.EvidenceUrl))
        {
            errors.Add("Evidence link must be an http(s) URL or a commit hash (7-40 hex characters).");
        }

        var linked = input.LinkedEntityId is not null && !string.IsNullOrWhiteSpace(input.LinkedEntityType);
        var hasUrl = !string.IsNullOrWhiteSpace(input.EvidenceUrl);
        var hasFile = input.AttachmentCount > 0;

        if (input.Type == TickType.Core)
        {
            var linkedOk = linked && CoreLinkTypes.Contains(input.LinkedEntityType!);
            if (!(linkedOk || hasFile || hasUrl || NonEmptyLines(input.EvidenceNote) >= 3))
            {
                errors.Add("Core tick needs an artifact: a linked note/drill/ADR/design note, an attachment, a URL, or a note of 3+ lines.");
            }
        }
        else
        {
            var linkedOk = linked && BuildLinkTypes.Contains(input.LinkedEntityType!);
            if (!(linkedOk || hasFile || hasUrl))
            {
                errors.Add("Build tick needs a durable result: a commit hash/URL, a PR URL, a linked flagship session/ADR, or an attachment.");
            }
        }

        return errors;
    }

    /// <summary>DSA tick (derived): at least one counted attempt or completed review on the date.</summary>
    public static bool DsaTick(int countedAttempts, int completedReviews) => countedAttempts + completedReviews > 0;

    public static int NonEmptyLines(string? text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : text.Split('\n').Count(l => !string.IsNullOrWhiteSpace(l));

    public static bool IsUrlOrCommitHash(string value)
    {
        var v = value.Trim();
        if (Uri.TryCreate(v, UriKind.Absolute, out var uri))
        {
            return uri.Scheme is "http" or "https";
        }

        return CommitHash().IsMatch(v);
    }

    [GeneratedRegex("^[0-9a-fA-F]{7,40}$")]
    private static partial Regex CommitHash();
}
