namespace SelfTracker.Domain.Common;

/// <summary>Common columns for every editable record (spec 3.2).</summary>
public abstract class Entity
{
    public int Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>Rows that come from seed/*.json (BR-15).</summary>
public interface ISeedable
{
    string? SeedKey { get; set; }
    bool IsUserModified { get; set; }
}

public abstract class SeedableEntity : Entity, ISeedable
{
    public string? SeedKey { get; set; }
    public bool IsUserModified { get; set; }
}
