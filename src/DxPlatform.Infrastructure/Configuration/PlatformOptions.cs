namespace DxPlatform.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public string Provider { get; set; } = "Sqlite";
    public bool ApplyMigrationsOnStartup { get; set; }
}

public sealed class BootstrapAdminOptions
{
    public const string SectionName = "BootstrapAdmin";
    public string? Email { get; set; }
    public string? Password { get; set; }
}

public sealed class DeploymentApiOptions
{
    public const string SectionName = "DeploymentApi";
    public string? Token { get; set; }
}

public sealed class HealthCheckOptions
{
    public const string SectionName = "HealthChecks";
    public int TimeoutSeconds { get; set; } = 10;
    public List<string> AllowedHosts { get; set; } = [];
    public List<int> AllowedPorts { get; set; } = [443];
    public bool AllowPrivateAddresses { get; set; }
}
