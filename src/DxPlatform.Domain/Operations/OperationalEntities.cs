using System.ComponentModel.DataAnnotations;
using DxPlatform.Domain.Applications;
using DxPlatform.Domain.Common;
using DxPlatform.Domain.Enums;

namespace DxPlatform.Domain.Operations;

public sealed class Deployment : BaseEntity
{
    public Guid ApplicationAssetId { get; set; }
    public ApplicationAsset ApplicationAsset { get; set; } = null!;

    [Required, StringLength(64)]
    public string Version { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string GitCommitSha { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Branch { get; set; }

    [Required, StringLength(160)]
    public string ExecutedBy { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DeploymentResult Result { get; set; }

    [Required, StringLength(100)]
    public string Environment { get; set; } = string.Empty;

    public Guid? RollbackFromDeploymentId { get; set; }

    [StringLength(2048)]
    public string? LogReference { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public sealed class HealthCheckResult : BaseEntity
{
    public Guid ApplicationAssetId { get; set; }
    public ApplicationAsset ApplicationAsset { get; set; } = null!;
    public HealthStatus Status { get; set; }
    public int? HttpStatusCode { get; set; }
    public long ResponseTimeMilliseconds { get; set; }
    public DateTimeOffset CheckedAt { get; set; }

    [StringLength(500)]
    public string? ErrorSummary { get; set; }
}

public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    [StringLength(450)]
    public string? ActorUserId { get; set; }

    [StringLength(160)]
    public string? ActorDisplayName { get; set; }

    [Required, StringLength(100)]
    public string ActionType { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string TargetType { get; set; } = string.Empty;

    [StringLength(450)]
    public string? TargetId { get; set; }

    public bool Succeeded { get; set; }

    [Required, StringLength(1000)]
    public string Summary { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string CorrelationId { get; set; } = string.Empty;
}

