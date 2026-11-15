namespace SelfTracker.Application.Features;

public sealed record ExportResult(string FilePath, string FileName, long SizeBytes, IReadOnlyDictionary<string, int> RowCounts, int FileCount);

public sealed record ImportSummary(bool DryRun, bool DatabaseEmpty, IReadOnlyDictionary<string, int> RowCounts, int FileCount, int HashMismatches, IReadOnlyList<string> Problems);

/// <summary>Full backup as one zip: JSON per table plus the files folder (spec 4.15). Implemented in Infrastructure.</summary>
public interface IDataPorter
{
    Task<ExportResult> ExportAsync(string targetDirectory, CancellationToken ct);

    /// <summary>M1 skeleton: dry-run summary only. The full restore into an empty database ships with M8.</summary>
    Task<ImportSummary> InspectAsync(Stream zip, CancellationToken ct);
}
