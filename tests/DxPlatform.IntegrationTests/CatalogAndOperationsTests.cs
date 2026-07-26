using System.Net;
using DxPlatform.Domain.Enums;
using DxPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DxPlatform.IntegrationTests;

public sealed class CatalogAndOperationsTests(DxPlatformWebApplicationFactory factory)
    : IClassFixture<DxPlatformWebApplicationFactory>
{
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

