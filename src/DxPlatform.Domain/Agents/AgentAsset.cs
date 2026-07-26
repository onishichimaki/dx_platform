using System.ComponentModel.DataAnnotations;
using DxPlatform.Domain.Common;
using DxPlatform.Domain.Enums;

namespace DxPlatform.Domain.Agents;

public sealed class AgentAsset : BaseEntity
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public AgentType AgentType { get; set; }

    [Required, StringLength(160)]
    public string Platform { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    public string Purpose { get; set; } = string.Empty;

    [StringLength(160)]
    public string? OwnerDepartment { get; set; }

    [StringLength(160)]
    public string? ManagerName { get; set; }

    [StringLength(160)]
    public string? ModelName { get; set; }

    [StringLength(2048)]
    public string? GitHubRepositoryUrl { get; set; }

    [StringLength(64)]
    public string? PromptVersion { get; set; }

    [StringLength(1000)]
    public string? PromptSummary { get; set; }

    [StringLength(500)]
    public string? PromptLocationReference { get; set; }

    public DateTimeOffset? PromptLastReviewedAt { get; set; }
    public LifecycleStatus Status { get; set; } = LifecycleStatus.Draft;

    [StringLength(100)]
    public string? PublicationScope { get; set; }

    public bool HasWriteOperations { get; set; }
    public bool SendsDataExternally { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public EvaluationStatus EvaluationStatus { get; set; }
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public DateTimeOffset? LastReviewedAt { get; set; }

    [StringLength(500)]
    public string? TagsText { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public sealed class ToolAsset : BaseEntity
{
    [Required, StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string ToolType { get; set; } = string.Empty;

    [StringLength(2048)]
    public string? ReferenceUrl { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }
}

