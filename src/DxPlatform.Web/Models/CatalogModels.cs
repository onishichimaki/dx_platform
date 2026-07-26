using System.ComponentModel.DataAnnotations;
using DxPlatform.Domain.Agents;
using DxPlatform.Domain.Applications;
using DxPlatform.Domain.DataAssets;
using DxPlatform.Domain.Enums;

namespace DxPlatform.Web.Models;

public sealed class ApplicationAssetInputModel : IValidatableObject
{
    public Guid? Id { get; set; }

    [Required, StringLength(64), RegularExpression("^[A-Za-z0-9_-]+$"), Display(Name = "アプリコード")]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200), Display(Name = "アプリ名")]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000), Display(Name = "説明")]
    public string? Description { get; set; }

    [StringLength(2048), Display(Name = "URL")]
    public string? Url { get; set; }

    [Display(Name = "資産種別")]
    public ApplicationAssetType AssetType { get; set; }

    [StringLength(500), Display(Name = "技術スタック")]
    public string? TechnologyStack { get; set; }

    [StringLength(160), Display(Name = "所有部署")]
    public string? OwnerDepartment { get; set; }

    [StringLength(160), Display(Name = "管理責任者")]
    public string? ManagerName { get; set; }

    [Display(Name = "管理責任者ユーザー")]
    public string? OwnerUserId { get; set; }

    [StringLength(160), Display(Name = "開発責任者")]
    public string? DevelopmentLead { get; set; }

    [StringLength(2048), Display(Name = "GitHubリポジトリURL")]
    public string? GitHubRepositoryUrl { get; set; }

    [StringLength(160), Display(Name = "稼働環境")]
    public string? RuntimeEnvironment { get; set; }

    [Display(Name = "ステータス")]
    public LifecycleStatus Status { get; set; } = LifecycleStatus.Draft;

    [StringLength(64), Display(Name = "バージョン")]
    public string? Version { get; set; }

    [StringLength(2048), Display(Name = "ヘルスチェックURL")]
    public string? HealthCheckUrl { get; set; }

    [StringLength(100), Display(Name = "機密区分")]
    public string? ConfidentialityClassification { get; set; }

    [DataType(System.ComponentModel.DataAnnotations.DataType.Date), Display(Name = "最終確認日")]
    public DateTimeOffset? LastReviewedAt { get; set; }

    [StringLength(500), Display(Name = "タグ（カンマ区切り）")]
    public string? TagsText { get; set; }

    [StringLength(2000), Display(Name = "備考")]
    public string? Notes { get; set; }

    public string? RowVersion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Url?.Length > 0 && !Uri.TryCreate(Url, UriKind.Absolute, out _))
        {
            yield return new ValidationResult("絶対URLを入力してください。", [nameof(Url)]);
        }
    }

    public void ApplyTo(ApplicationAsset entity)
    {
        entity.Code = Code.Trim();
        entity.Name = Name.Trim();
        entity.Description = Description?.Trim();
        entity.Url = Url?.Trim();
        entity.AssetType = AssetType;
        entity.TechnologyStack = TechnologyStack?.Trim();
        entity.OwnerDepartment = OwnerDepartment?.Trim();
        entity.ManagerName = ManagerName?.Trim();
        entity.OwnerUserId = string.IsNullOrWhiteSpace(OwnerUserId) ? null : OwnerUserId;
        entity.DevelopmentLead = DevelopmentLead?.Trim();
        entity.GitHubRepositoryUrl = GitHubRepositoryUrl?.Trim();
        entity.RuntimeEnvironment = RuntimeEnvironment?.Trim();
        entity.Status = Status;
        entity.Version = Version?.Trim();
        entity.HealthCheckUrl = HealthCheckUrl?.Trim();
        entity.ConfidentialityClassification = ConfidentialityClassification?.Trim();
        entity.LastReviewedAt = LastReviewedAt;
        entity.TagsText = TagsText?.Trim();
        entity.Notes = Notes?.Trim();
    }

    public static ApplicationAssetInputModel FromEntity(ApplicationAsset entity) => new()
    {
        Id = entity.Id,
        Code = entity.Code,
        Name = entity.Name,
        Description = entity.Description,
        Url = entity.Url,
        AssetType = entity.AssetType,
        TechnologyStack = entity.TechnologyStack,
        OwnerDepartment = entity.OwnerDepartment,
        ManagerName = entity.ManagerName,
        OwnerUserId = entity.OwnerUserId,
        DevelopmentLead = entity.DevelopmentLead,
        GitHubRepositoryUrl = entity.GitHubRepositoryUrl,
        RuntimeEnvironment = entity.RuntimeEnvironment,
        Status = entity.Status,
        Version = entity.Version,
        HealthCheckUrl = entity.HealthCheckUrl,
        ConfidentialityClassification = entity.ConfidentialityClassification,
        LastReviewedAt = entity.LastReviewedAt,
        TagsText = entity.TagsText,
        Notes = entity.Notes,
        RowVersion = Convert.ToBase64String(entity.RowVersion)
    };
}

public sealed class DataAssetInputModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(200), Display(Name = "データ資産名")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "データ種別")]
    public DataAssetType DataType { get; set; }

    [Required, StringLength(200), Display(Name = "システム名")]
    public string SystemName { get; set; } = string.Empty;

    [StringLength(2000), Display(Name = "説明")]
    public string? Description { get; set; }

    [StringLength(160), Display(Name = "所有部署")]
    public string? OwnerDepartment { get; set; }

    [StringLength(160), Display(Name = "データオーナー")]
    public string? DataOwner { get; set; }

    [StringLength(100), Display(Name = "更新頻度")]
    public string? UpdateFrequency { get; set; }

    [StringLength(100), Display(Name = "機密区分")]
    public string? ConfidentialityClassification { get; set; }

    [StringLength(100), Display(Name = "保持期間")]
    public string? RetentionPeriod { get; set; }

    [Display(Name = "ステータス")]
    public LifecycleStatus Status { get; set; }

    [StringLength(200), Display(Name = "接続先参照名")]
    public string? ConnectionReferenceName { get; set; }

    [StringLength(500), Display(Name = "タグ（カンマ区切り）")]
    public string? TagsText { get; set; }

    [DataType(System.ComponentModel.DataAnnotations.DataType.Date), Display(Name = "最終確認日")]
    public DateTimeOffset? LastReviewedAt { get; set; }

    [StringLength(2000), Display(Name = "備考")]
    public string? Notes { get; set; }

    [Display(Name = "関連アプリ")]
    public List<Guid> RelatedApplicationIds { get; set; } = [];

    [Display(Name = "関連Agent")]
    public List<Guid> RelatedAgentIds { get; set; } = [];

    [Display(Name = "関連データ資産")]
    public List<Guid> RelatedDataAssetIds { get; set; } = [];

    public string? RowVersion { get; set; }

    public void ApplyTo(DataAsset entity)
    {
        entity.Name = Name.Trim();
        entity.DataType = DataType;
        entity.SystemName = SystemName.Trim();
        entity.Description = Description?.Trim();
        entity.OwnerDepartment = OwnerDepartment?.Trim();
        entity.DataOwner = DataOwner?.Trim();
        entity.UpdateFrequency = UpdateFrequency?.Trim();
        entity.ConfidentialityClassification = ConfidentialityClassification?.Trim();
        entity.RetentionPeriod = RetentionPeriod?.Trim();
        entity.Status = Status;
        entity.ConnectionReferenceName = ConnectionReferenceName?.Trim();
        entity.TagsText = TagsText?.Trim();
        entity.LastReviewedAt = LastReviewedAt;
        entity.Notes = Notes?.Trim();
    }
}

public sealed class AgentAssetInputModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(200), Display(Name = "Agent名")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Agent種別")]
    public AgentType AgentType { get; set; }

    [Required, StringLength(160), Display(Name = "利用プラットフォーム")]
    public string Platform { get; set; } = string.Empty;

    [Required, StringLength(1000), Display(Name = "用途")]
    public string Purpose { get; set; } = string.Empty;

    [StringLength(160), Display(Name = "所有部署")]
    public string? OwnerDepartment { get; set; }

    [StringLength(160), Display(Name = "管理責任者")]
    public string? ManagerName { get; set; }

    [StringLength(160), Display(Name = "利用モデル名")]
    public string? ModelName { get; set; }

    [StringLength(2048), Display(Name = "GitHubリポジトリURL")]
    public string? GitHubRepositoryUrl { get; set; }

    [StringLength(64), Display(Name = "プロンプトバージョン")]
    public string? PromptVersion { get; set; }

    [StringLength(1000), Display(Name = "プロンプト要約")]
    public string? PromptSummary { get; set; }

    [StringLength(500), Display(Name = "プロンプト保管場所参照")]
    public string? PromptLocationReference { get; set; }

    [DataType(DataType.Date), Display(Name = "プロンプト最終レビュー日")]
    public DateTimeOffset? PromptLastReviewedAt { get; set; }

    [Display(Name = "ステータス")]
    public LifecycleStatus Status { get; set; }

    [StringLength(100), Display(Name = "公開範囲")]
    public string? PublicationScope { get; set; }

    [Display(Name = "書き込み操作あり")]
    public bool HasWriteOperations { get; set; }

    [Display(Name = "外部送信あり")]
    public bool SendsDataExternally { get; set; }

    [Display(Name = "リスクレベル")]
    public RiskLevel RiskLevel { get; set; }

    [Display(Name = "評価ステータス")]
    public EvaluationStatus EvaluationStatus { get; set; }

    [DataType(DataType.Date), Display(Name = "最終評価日")]
    public DateTimeOffset? LastEvaluatedAt { get; set; }

    [DataType(DataType.Date), Display(Name = "最終確認日")]
    public DateTimeOffset? LastReviewedAt { get; set; }

    [StringLength(500), Display(Name = "タグ（カンマ区切り）")]
    public string? TagsText { get; set; }

    [StringLength(2000), Display(Name = "備考")]
    public string? Notes { get; set; }

    [Display(Name = "関連データ資産")]
    public List<Guid> RelatedDataAssetIds { get; set; } = [];

    [Display(Name = "利用ツール/API")]
    public List<Guid> RelatedToolAssetIds { get; set; } = [];

    public string? RowVersion { get; set; }

    public void ApplyTo(AgentAsset entity)
    {
        entity.Name = Name.Trim();
        entity.AgentType = AgentType;
        entity.Platform = Platform.Trim();
        entity.Purpose = Purpose.Trim();
        entity.OwnerDepartment = OwnerDepartment?.Trim();
        entity.ManagerName = ManagerName?.Trim();
        entity.ModelName = ModelName?.Trim();
        entity.GitHubRepositoryUrl = GitHubRepositoryUrl?.Trim();
        entity.PromptVersion = PromptVersion?.Trim();
        entity.PromptSummary = PromptSummary?.Trim();
        entity.PromptLocationReference = PromptLocationReference?.Trim();
        entity.PromptLastReviewedAt = PromptLastReviewedAt;
        entity.Status = Status;
        entity.PublicationScope = PublicationScope?.Trim();
        entity.HasWriteOperations = HasWriteOperations;
        entity.SendsDataExternally = SendsDataExternally;
        entity.RiskLevel = RiskLevel;
        entity.EvaluationStatus = EvaluationStatus;
        entity.LastEvaluatedAt = LastEvaluatedAt;
        entity.LastReviewedAt = LastReviewedAt;
        entity.TagsText = TagsText?.Trim();
        entity.Notes = Notes?.Trim();
    }
}

public sealed class ToolAssetInputModel
{
    [Required, StringLength(160), Display(Name = "ツール/API名")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(100), Display(Name = "種別")]
    public string ToolType { get; set; } = string.Empty;

    [StringLength(2048), Display(Name = "参照URL")]
    public string? ReferenceUrl { get; set; }

    [StringLength(1000), Display(Name = "説明")]
    public string? Description { get; set; }
}
