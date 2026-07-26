using DxPlatform.Domain.Groups;
using Microsoft.AspNetCore.Identity;

namespace DxPlatform.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }
    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();
    public ICollection<GroupMember> GroupMemberships { get; set; } = [];
}

public sealed class ApplicationRole : IdentityRole
{
}

