using System.ComponentModel.DataAnnotations;
using DxPlatform.Domain.Common;
using DxPlatform.Domain.Enums;

namespace DxPlatform.Domain.Applications;

public sealed class ApplicationAsset : BaseEntity
{
    [Required, StringLength(64)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(2048)]
    public string? Url { get; set; }

    public ApplicationAssetType AssetType { get; set; }

    [StringLength(500)]
    public string? TechnologyStack { get; set; }

    [StringLength(160)]
    public string? OwnerDepartment { get; set; }

    [StringLength(160)]
    public string? ManagerName { get; set; }

    [StringLength(450)]
    public string? OwnerUserId { get; set; }

    [StringLength(160)]
    public string? DevelopmentLead { get; set; }

    [StringLength(2048)]
    public string? GitHubRepositoryUrl { get; set; }

    [StringLength(160)]
    public string? RuntimeEnvironment { get; set; }

    public LifecycleStatus Status { get; set; } = LifecycleStatus.Draft;

    [StringLength(64)]
    public string? Version { get; set; }

    [StringLength(2048)]
    public string? HealthCheckUrl { get; set; }

    [StringLength(100)]
    public string? ConfidentialityClassification { get; set; }

    public DateTimeOffset? LastReviewedAt { get; set; }
    public DateTimeOffset? LastDeployedAt { get; set; }

    [StringLength(500)]
    public string? TagsText { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public ICollection<ApplicationPermission> Permissions { get; set; } = [];
}

public sealed class ApplicationPermission : BaseEntity
{
    public Guid ApplicationAssetId { get; set; }
    public ApplicationAsset ApplicationAsset { get; set; } = null!;

    [StringLength(450)]
    public string? UserId { get; set; }

    public Guid? GroupId { get; set; }
    public PermissionLevel Level { get; set; }
}

