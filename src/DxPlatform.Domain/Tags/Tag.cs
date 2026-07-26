using System.ComponentModel.DataAnnotations;
using DxPlatform.Domain.Common;
using DxPlatform.Domain.Enums;

namespace DxPlatform.Domain.Tags;

public sealed class Tag : BaseEntity
{
    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;
}

public sealed class AssetTag : BaseEntity
{
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
    public AssetKind AssetKind { get; set; }
    public Guid AssetId { get; set; }
}

