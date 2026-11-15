using SelfTracker.Domain.Common;

namespace SelfTracker.Domain.Entities;

public class FileAsset : Entity
{
    public Guid StorageKey { get; set; }
    /// <summary>Path relative to FileRoot: {yyyy}/{MM}/{StorageKey}{ext}. Never derived from the original name.</summary>
    public string RelativePath { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string DisplayTitle { get; set; } = "";
    public string? Description { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public FileKind Kind { get; set; }
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public DateTimeOffset UploadedAt { get; set; }
    public string? Tags { get; set; }
    public List<FileLink> Links { get; set; } = [];
}

public class FileLink : Entity
{
    public int FileAssetId { get; set; }
    public FileAsset? FileAsset { get; set; }
    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }
    public string? Label { get; set; }
}
