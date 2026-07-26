using System.ComponentModel.DataAnnotations;
using DxPlatform.Domain.Common;
using DxPlatform.Domain.Enums;

namespace DxPlatform.Domain.DataAssets;

public sealed class DataAsset : BaseEntity
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public DataAssetType DataType { get; set; }

    [Required, StringLength(200)]
    public string SystemName { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(160)]
    public string? OwnerDepartment { get; set; }

    [StringLength(160)]
    public string? DataOwner { get; set; }

    [StringLength(100)]
    public string? UpdateFrequency { get; set; }

    [StringLength(100)]
    public string? ConfidentialityClassification { get; set; }

    [StringLength(100)]
    public string? RetentionPeriod { get; set; }

    public LifecycleStatus Status { get; set; } = LifecycleStatus.Draft;

    [StringLength(200)]
    public string? ConnectionReferenceName { get; set; }

    [StringLength(500)]
    public string? TagsText { get; set; }

    public DateTimeOffset? LastReviewedAt { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public sealed class DataAssetRelation : BaseEntity
{
    public Guid SourceDataAssetId { get; set; }
    public DataAsset SourceDataAsset { get; set; } = null!;
    public Guid TargetDataAssetId { get; set; }
    public DataAsset TargetDataAsset { get; set; } = null!;

    [Required, StringLength(100)]
    public string RelationType { get; set; } = "Related";
}

