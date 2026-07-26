using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DxPlatform.Domain.Enums;
using DxPlatform.Infrastructure;
using DxPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DxPlatform.IntegrationTests;

public sealed class CatalogAndOperationsTests(DxPlatformWebApplicationFactory factory)
    : IClassFixture<DxPlatformWebApplicationFactory>
{
    [Fact]
    public void ProductionConfiguration_RejectsSqlite()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:DefaultConnection"] = "Data Source=production-is-forbidden.db"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDxPlatformInfrastructure(configuration, isProduction: true);
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<AppDbContext>());
        Assert.Contains("Production環境ではSQLiteを使用できません", exception.Message);
    }

    [Fact]
    public async Task Admin_CanCreateApplicationDataAssetAndAgent_WithAuditLogs()
    {
        using var client = IntegrationTestHelpers.CreateClient(factory);
        await IntegrationTestHelpers.LoginAsync(
            client,
            DxPlatformWebApplicationFactory.AdminEmail,
            DxPlatformWebApplicationFactory.AdminPassword);

        var appCode = $"CRUD-{Guid.NewGuid():N}";
        var appToken = await IntegrationTestHelpers.GetAntiForgeryTokenAsync(client, "/admin/apps/create");
        using var appResponse = await client.PostAsync(
            "/admin/apps/create",
            Form(new()
            {
                ["Code"] = appCode,
                ["Name"] = "CRUD Test Application",
                ["AssetType"] = ApplicationAssetType.WebApplication.ToString(),
                ["Status"] = LifecycleStatus.Draft.ToString(),
                ["__RequestVerificationToken"] = appToken
            }));
        Assert.Equal(HttpStatusCode.Redirect, appResponse.StatusCode);

        var dataName = $"CRUD Data {Guid.NewGuid():N}";
        var dataToken = await IntegrationTestHelpers.GetAntiForgeryTokenAsync(client, "/data-assets/create");
        using var dataResponse = await client.PostAsync(
            "/data-assets/create",
            Form(new()
            {
                ["Name"] = dataName,
                ["DataType"] = DataAssetType.SqlServer.ToString(),
                ["SystemName"] = "Sample CRUD System",
                ["Status"] = LifecycleStatus.Draft.ToString(),
                ["ConnectionReferenceName"] = "SampleCrudReference",
                ["__RequestVerificationToken"] = dataToken
            }));
        Assert.Equal(HttpStatusCode.Redirect, dataResponse.StatusCode);

        var agentName = $"CRUD Agent {Guid.NewGuid():N}";
        var agentToken = await IntegrationTestHelpers.GetAntiForgeryTokenAsync(client, "/agents/create");
        using var agentResponse = await client.PostAsync(
            "/agents/create",
            Form(new()
            {
                ["Name"] = agentName,
                ["AgentType"] = AgentType.Custom.ToString(),
                ["Platform"] = "Sample Agent Platform",
                ["Purpose"] = "Integration test purpose",
                ["Status"] = LifecycleStatus.Draft.ToString(),
                ["RiskLevel"] = RiskLevel.Low.ToString(),
                ["EvaluationStatus"] = EvaluationStatus.NotEvaluated.ToString(),
                ["__RequestVerificationToken"] = agentToken
            }));
        Assert.Equal(HttpStatusCode.Redirect, agentResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await dbContext.ApplicationAssets.AnyAsync(x => x.Code == appCode));
        Assert.True(await dbContext.DataAssets.AnyAsync(x => x.Name == dataName));
        Assert.True(await dbContext.AgentAssets.AnyAsync(x => x.Name == agentName));
        Assert.True(await dbContext.AuditLogs.AnyAsync(x => x.ActionType == "ApplicationCreated"));
        Assert.True(await dbContext.AuditLogs.AnyAsync(x => x.ActionType == "DataAssetCreated"));
        Assert.True(await dbContext.AuditLogs.AnyAsync(x => x.ActionType == "AgentCreated"));
    }

    [Fact]
    public async Task Admin_CanUpdateAndDisableApplicationDataAssetAndAgent()
    {
        Guid appId;
        Guid dataId;
        Guid agentId;
        string appRowVersion;
        string dataRowVersion;
        string agentRowVersion;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var app = new DxPlatform.Domain.Applications.ApplicationAsset
            {
                Code = $"UPDATE-{Guid.NewGuid():N}",
                Name = "Application Before Update",
                AssetType = ApplicationAssetType.WebApplication
            };
            var data = new DxPlatform.Domain.DataAssets.DataAsset
            {
                Name = "Data Before Update",
                DataType = DataAssetType.SqlServer,
                SystemName = "Sample Update System"
            };
            var agent = new DxPlatform.Domain.Agents.AgentAsset
            {
                Name = "Agent Before Update",
                AgentType = AgentType.Custom,
                Platform = "Sample Platform",
                Purpose = "Update integration test"
            };
            dbContext.AddRange(app, data, agent);
            await dbContext.SaveChangesAsync();
            appId = app.Id;
            dataId = data.Id;
            agentId = agent.Id;
            appRowVersion = Convert.ToBase64String(app.RowVersion);
            dataRowVersion = Convert.ToBase64String(data.RowVersion);
            agentRowVersion = Convert.ToBase64String(agent.RowVersion);
        }

        using var client = IntegrationTestHelpers.CreateClient(factory);
        await IntegrationTestHelpers.LoginAsync(
            client,
            DxPlatformWebApplicationFactory.AdminEmail,
            DxPlatformWebApplicationFactory.AdminPassword);

        var appToken = await IntegrationTestHelpers.GetAntiForgeryTokenAsync(
            client, $"/admin/apps/{appId}/edit");
        using var appResponse = await client.PostAsync(
            $"/admin/apps/{appId}/edit",
            Form(new()
            {
                ["Id"] = appId.ToString(),
                ["Code"] = $"UPDATED-{appId:N}",
                ["Name"] = "Application After Update",
                ["AssetType"] = ApplicationAssetType.WebApplication.ToString(),
                ["Status"] = LifecycleStatus.Testing.ToString(),
                ["RowVersion"] = appRowVersion,
                ["__RequestVerificationToken"] = appToken
            }));
        Assert.Equal(HttpStatusCode.Redirect, appResponse.StatusCode);

        var dataToken = await IntegrationTestHelpers.GetAntiForgeryTokenAsync(
            client, $"/data-assets/{dataId}/edit");
        using var dataResponse = await client.PostAsync(
            $"/data-assets/{dataId}/edit",
            Form(new()
            {
                ["Id"] = dataId.ToString(),
                ["Name"] = "Data After Update",
                ["DataType"] = DataAssetType.SqlServer.ToString(),
                ["SystemName"] = "Sample Update System",
                ["Status"] = LifecycleStatus.Testing.ToString(),
                ["ConnectionReferenceName"] = "SampleUpdateReference",
                ["RowVersion"] = dataRowVersion,
                ["__RequestVerificationToken"] = dataToken
            }));
        Assert.Equal(HttpStatusCode.Redirect, dataResponse.StatusCode);

        var agentToken = await IntegrationTestHelpers.GetAntiForgeryTokenAsync(
            client, $"/agents/{agentId}/edit");
        using var agentResponse = await client.PostAsync(
            $"/agents/{agentId}/edit",
            Form(new()
            {
                ["Id"] = agentId.ToString(),
                ["Name"] = "Agent After Update",
                ["AgentType"] = AgentType.Custom.ToString(),
                ["Platform"] = "Sample Platform",
                ["Purpose"] = "Update integration test",
                ["Status"] = LifecycleStatus.Testing.ToString(),
                ["RiskLevel"] = RiskLevel.Low.ToString(),
                ["EvaluationStatus"] = EvaluationStatus.InProgress.ToString(),
                ["RowVersion"] = agentRowVersion,
                ["__RequestVerificationToken"] = agentToken
            }));
        Assert.Equal(HttpStatusCode.Redirect, agentResponse.StatusCode);

        foreach (var path in new[]
                 {
                     $"/admin/apps/{appId}/disable",
                     $"/data-assets/{dataId}/disable",
                     $"/agents/{agentId}/disable"
                 })
        {
            var token = await IntegrationTestHelpers.GetAntiForgeryTokenAsync(client, path);
            using var response = await client.PostAsync(
                path,
                Form(new() { ["__RequestVerificationToken"] = token }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False((await verifyDb.ApplicationAssets.FindAsync(appId))!.IsActive);
        Assert.False((await verifyDb.DataAssets.FindAsync(dataId))!.IsActive);
        Assert.False((await verifyDb.AgentAssets.FindAsync(agentId))!.IsActive);
        Assert.True(await verifyDb.AuditLogs.AnyAsync(x => x.ActionType == "ApplicationUpdated"));
        Assert.True(await verifyDb.AuditLogs.AnyAsync(x => x.ActionType == "DataAssetUpdated"));
        Assert.True(await verifyDb.AuditLogs.AnyAsync(x => x.ActionType == "AgentUpdated"));
        Assert.True(await verifyDb.AuditLogs.AnyAsync(x => x.ActionType == "ApplicationDisabled"));
        Assert.True(await verifyDb.AuditLogs.AnyAsync(x => x.ActionType == "DataAssetDisabled"));
        Assert.True(await verifyDb.AuditLogs.AnyAsync(x => x.ActionType == "AgentDisabled"));
    }

    [Fact]
    public async Task ManualHealthCheck_StoresSanitizedResult()
    {
        Guid appId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var app = new DxPlatform.Domain.Applications.ApplicationAsset
            {
                Code = $"HEALTH-{Guid.NewGuid():N}",
                Name = "Health Test Application",
                AssetType = ApplicationAssetType.WebApplication,
                HealthCheckUrl = "https://example.com/health"
            };
            dbContext.ApplicationAssets.Add(app);
            await dbContext.SaveChangesAsync();
            appId = app.Id;
        }

        using var client = IntegrationTestHelpers.CreateClient(factory);
        await IntegrationTestHelpers.LoginAsync(
            client,
            DxPlatformWebApplicationFactory.AdminEmail,
            DxPlatformWebApplicationFactory.AdminPassword);
        var token = await IntegrationTestHelpers.GetAntiForgeryTokenAsync(client, "/admin/health-checks");
        using var response = await client.PostAsync(
            $"/admin/health-checks/{appId}/run",
            Form(new() { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var result = await verifyDb.HealthCheckResults
            .SingleAsync(x => x.ApplicationAssetId == appId);
        Assert.Equal(HealthStatus.Succeeded, result.Status);
        Assert.Equal(200, result.HttpStatusCode);
        Assert.Null(result.ErrorSummary);
    }

    [Fact]
    public async Task DeploymentApi_RequiresBearerTokenAndStoresHistory()
    {
        Guid appId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var app = new DxPlatform.Domain.Applications.ApplicationAsset
            {
                Code = $"DEPLOY-{Guid.NewGuid():N}",
                Name = "Deployment Test Application",
                AssetType = ApplicationAssetType.WebApplication
            };
            dbContext.ApplicationAssets.Add(app);
            await dbContext.SaveChangesAsync();
            appId = app.Id;
        }

        var request = new
        {
            applicationAssetId = appId,
            version = "1.0.0-test",
            gitCommitSha = "0123456789abcdef0123456789abcdef01234567",
            branch = "feature/integration-test",
            executedBy = "integration-test",
            startedAt = DateTimeOffset.UtcNow,
            finishedAt = DateTimeOffset.UtcNow,
            result = "Succeeded",
            environment = "Test",
            logReference = "https://example.com/test-run"
        };

        using var anonymousClient = IntegrationTestHelpers.CreateClient(factory);
        using var unauthorized = await anonymousClient.PostAsJsonAsync("/api/deployments", request);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var client = IntegrationTestHelpers.CreateClient(factory);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-only-deployment-token");
        using var response = await client.PostAsJsonAsync("/api/deployments", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await verifyDb.Deployments.AnyAsync(x =>
            x.ApplicationAssetId == appId && x.Version == "1.0.0-test"));
        Assert.True(await verifyDb.AuditLogs.AnyAsync(x =>
            x.ActionType == "DeploymentRegistered"));
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task PlatformHealthEndpoints_ReturnHealthy(string path)
    {
        using var client = IntegrationTestHelpers.CreateClient(factory);
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        Assert.Contains("Healthy", await response.Content.ReadAsStringAsync());
    }

    private static FormUrlEncodedContent Form(Dictionary<string, string> values) => new(values);
}
