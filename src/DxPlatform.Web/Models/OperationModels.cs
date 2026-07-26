using System.ComponentModel.DataAnnotations;
using DxPlatform.Domain.Operations;

namespace DxPlatform.Web.Models;

public sealed class DashboardViewModel
{
    public int ApplicationCount { get; init; }
    public int ProductionApplicationCount { get; init; }
    public int DataAssetCount { get; init; }
    public int AgentCount { get; init; }
    public int SuspendedAssetCount { get; init; }
    public int MissingManagerCount { get; init; }
    public int MissingConfidentialityCount { get; init; }
    public int OverdueReviewCount { get; init; }
    public IReadOnlyList<Deployment> RecentDeployments { get; init; } = [];
    public IReadOnlyList<AuditLog> RecentAuditLogs { get; init; } = [];
}

public sealed class DeploymentRegistrationRequest
{
    [Required]
    public Guid ApplicationAssetId { get; set; }

    [Required, StringLength(64)]
    public string Version { get; set; } = string.Empty;

    [Required, StringLength(64), RegularExpression("^[0-9a-fA-F]{7,64}$")]
    public string GitCommitSha { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Branch { get; set; }

    [Required, StringLength(160)]
    public string ExecutedBy { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    [Required, StringLength(32), RegularExpression("^(InProgress|Succeeded|Failed|RolledBack)$")]
    public string Result { get; set; } = "InProgress";

    [Required, StringLength(100)]
    public string Environment { get; set; } = string.Empty;

    public Guid? RollbackFromDeploymentId { get; set; }

    [StringLength(2048)]
    public string? LogReference { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

