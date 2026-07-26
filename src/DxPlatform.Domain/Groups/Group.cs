using System.ComponentModel.DataAnnotations;
using DxPlatform.Domain.Common;

namespace DxPlatform.Domain.Groups;

public sealed class Group : BaseEntity
{
    [Required, StringLength(64)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public ICollection<GroupMember> Members { get; set; } = [];
}

public sealed class GroupMember : BaseEntity
{
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;

    [Required, StringLength(450)]
    public string UserId { get; set; } = string.Empty;
}

