namespace DxPlatform.Domain.Enums;

public enum ApplicationAssetType
{
    WebApplication,
    MotionBoard,
    PowerApps,
    ExcelVba,
    Access,
    Rpa,
    Api,
    Other
}

public enum LifecycleStatus
{
    Draft,
    Testing,
    Production,
    Suspended,
    Retired
}

public enum PermissionLevel
{
    None = 0,
    View = 1,
    Use = 2,
    Manage = 3
}

public enum DataAssetType
{
    SqlServer,
    DrSum,
    Excel,
    Csv,
    SharePoint,
    Api,
    File,
    Other
}

public enum AgentType
{
    CopilotStudio,
    Dify,
    Custom,
    AzureAi,
    Bedrock,
    Gemini,
    Gpt,
    AsanaAiTeammate,
    Other
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum EvaluationStatus
{
    NotEvaluated,
    InProgress,
    Approved,
    Rejected,
    Expired
}

public enum DeploymentResult
{
    InProgress,
    Succeeded,
    Failed,
    RolledBack
}

public enum HealthStatus
{
    Succeeded,
    Failed,
    TimedOut
}

public enum AssetKind
{
    Application,
    DataAsset,
    Agent,
    Tool
}

