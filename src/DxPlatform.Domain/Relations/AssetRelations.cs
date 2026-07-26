using DxPlatform.Domain.Agents;
using DxPlatform.Domain.Applications;
using DxPlatform.Domain.Common;
using DxPlatform.Domain.DataAssets;

namespace DxPlatform.Domain.Relations;

public sealed class ApplicationDataAsset : BaseEntity
{
    public Guid ApplicationAssetId { get; set; }
    public ApplicationAsset ApplicationAsset { get; set; } = null!;
    public Guid DataAssetId { get; set; }
    public DataAsset DataAsset { get; set; } = null!;
}

public sealed class AgentDataAsset : BaseEntity
{
    public Guid AgentAssetId { get; set; }
    public AgentAsset AgentAsset { get; set; } = null!;
    public Guid DataAssetId { get; set; }
    public DataAsset DataAsset { get; set; } = null!;
}

public sealed class AgentToolAsset : BaseEntity
{
    public Guid AgentAssetId { get; set; }
    public AgentAsset AgentAsset { get; set; } = null!;
    public Guid ToolAssetId { get; set; }
    public ToolAsset ToolAsset { get; set; } = null!;
}

