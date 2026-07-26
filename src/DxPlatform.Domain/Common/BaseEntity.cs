namespace DxPlatform.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();
}

