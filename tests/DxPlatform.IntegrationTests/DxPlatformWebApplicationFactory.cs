using DxPlatform.Application.Abstractions;
using DxPlatform.Domain.Enums;
using DxPlatform.Infrastructure.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DxPlatform.IntegrationTests;

public sealed class DxPlatformWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@example.test";
    public const string AdminPassword = "Test-Only-Admin-123!";
    public const string ViewerPassword = "Test-Only-Viewer-123!";

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "DxPlatformTests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_directory);
        var databasePath = Path.Combine(_directory, "integration.db");
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={databasePath}",
                ["BootstrapAdmin:Email"] = AdminEmail,
                ["BootstrapAdmin:Password"] = AdminPassword,
                ["DeploymentApi:Token"] = "test-only-deployment-token",
                ["DemoData:Enabled"] = "false",
                ["HealthChecks:TimeoutSeconds"] = "2",
                ["Logging:File:Path"] = ""
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IApplicationHealthCheckService>();
            services.AddSingleton<IApplicationHealthCheckService, StubHealthCheckService>();
            services.RemoveAll<IHealthCheckTargetValidator>();
            services.AddSingleton<IHealthCheckTargetValidator, StubHealthCheckTargetValidator>();
            services.Configure<BootstrapAdminOptions>(options =>
            {
                options.Email = AdminEmail;
                options.Password = AdminPassword;
            });
            services.Configure<DeploymentApiOptions>(options =>
                options.Token = "test-only-deployment-token");
            services.Configure<HealthCheckOptions>(options =>
                options.TimeoutSeconds = 2);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_directory))
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
                // Windows上でテストホスト終了直後にファイルハンドル解放が遅れる場合は、
                // OSの一時領域に残し、次回の一時領域清掃へ委ねる。
            }
        }
    }

    private sealed class StubHealthCheckService : IApplicationHealthCheckService
    {
        public Task<HealthCheckExecutionResult> CheckAsync(
            string url,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new HealthCheckExecutionResult(
                HealthStatus.Succeeded,
                200,
                12,
                null));
    }

    private sealed class StubHealthCheckTargetValidator : IHealthCheckTargetValidator
    {
        public Task<bool> IsAllowedAsync(
            string? url,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<IReadOnlyList<System.Net.IPAddress>> GetAllowedAddressesAsync(
            string host,
            int port,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<System.Net.IPAddress>>(
                [System.Net.IPAddress.Parse("203.0.113.10")]);
    }
}
