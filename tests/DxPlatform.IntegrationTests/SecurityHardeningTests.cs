using System.Net;
using System.Net.Http.Json;
using DxPlatform.Application.Abstractions;
using DxPlatform.Application.Security;
using DxPlatform.Application.Validation;
using DxPlatform.Domain.Applications;
using DxPlatform.Domain.Enums;
using DxPlatform.Domain.Operations;
using DxPlatform.Infrastructure.Configuration;
using DxPlatform.Infrastructure.Persistence;
using DxPlatform.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DxPlatform.IntegrationTests;

public sealed class SecurityHardeningTests(
    DxPlatformWebApplicationFactory factory)
    : IClassFixture<DxPlatformWebApplicationFactory>
{
    [Fact]
    public async Task AppOwner_HealthCheckPageAndRun_AreLimitedToOwnedApplication()
    {
        var email = $"owner-{Guid.NewGuid():N}@example.test";
        var owner = await IntegrationTestHelpers.CreateUserAsync(factory, email, RoleNames.AppOwner);
        var ownedName = $"Owned Health {Guid.NewGuid():N}";
        var hiddenName = $"Hidden Health {Guid.NewGuid():N}";
        Guid hiddenId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owned = NewApplication(ownedName, owner.Id, "https://example.com/owned");
            var hidden = NewApplication(hiddenName, null, "https://example.com/hidden");
            db.AddRange(owned, hidden);
            await db.SaveChangesAsync();
            hiddenId = hidden.Id;
        }

        using var client = IntegrationTestHelpers.CreateClient(factory);
        await IntegrationTestHelpers.LoginAsync(
            client,
            email,
            DxPlatformWebApplicationFactory.ViewerPassword);
        var token = await IntegrationTestHelpers.GetAntiForgeryTokenAsync(
            client,
            "/admin/health-checks");
        using var page = await client.GetAsync("/admin/health-checks");
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains(ownedName, html);
        Assert.DoesNotContain(hiddenName, html);

        using var denied = await client.PostAsync(
            $"/admin/health-checks/{hiddenId}/run",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Equal("/access-denied", denied.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Viewer_DeploymentAndDashboard_HideUnauthorizedApplicationNames()
    {
        var email = $"deployment-viewer-{Guid.NewGuid():N}@example.test";
        var viewer = await IntegrationTestHelpers.CreateUserAsync(factory, email, RoleNames.Viewer);
        var visibleName = $"Visible Deployment {Guid.NewGuid():N}";
        var hiddenName = $"Hidden Deployment {Guid.NewGuid():N}";

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var visible = NewApplication(visibleName, null, null);
            var hidden = NewApplication(hiddenName, null, null);
            db.AddRange(visible, hidden);
            db.ApplicationPermissions.Add(new ApplicationPermission
            {
                ApplicationAsset = visible,
                UserId = viewer.Id,
                Level = PermissionLevel.View
            });
            db.Deployments.AddRange(NewDeployment(visible), NewDeployment(hidden));
            await db.SaveChangesAsync();
        }

        using var client = IntegrationTestHelpers.CreateClient(factory);
        await IntegrationTestHelpers.LoginAsync(
            client,
            email,
            DxPlatformWebApplicationFactory.ViewerPassword);

        foreach (var path in new[] { "/deployments", "/dashboard" })
        {
            using var response = await client.GetAsync(path);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains(visibleName, html);
            Assert.DoesNotContain(hiddenName, html);
        }
    }

    [Fact]
    public async Task Responses_IncludeBrowserSecurityHeaders()
    {
        using var client = IntegrationTestHelpers.CreateClient(factory);
        using var response = await client.GetAsync("/login");
        response.EnsureSuccessStatusCode();
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("default-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task DeploymentApi_RejectsBurstAboveConfiguredRate()
    {
        using var client = IntegrationTestHelpers.CreateClient(factory);
        var request = new
        {
            applicationAssetId = Guid.NewGuid(),
            version = "1.0.0-rate-test",
            gitCommitSha = "0123456789abcdef0123456789abcdef01234567",
            executedBy = "rate-test",
            result = "Succeeded",
            environment = "Test"
        };
        for (var attempt = 0; attempt < 60; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/deployments", request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using var limited = await client.PostAsJsonAsync("/api/deployments", request);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Theory]
    [InlineData("127.0.0.1", false, false)]
    [InlineData("169.254.169.254", true, false)]
    [InlineData("100.100.100.200", true, false)]
    [InlineData("fd00:ec2::254", true, false)]
    [InlineData("10.20.30.40", false, false)]
    [InlineData("10.20.30.40", true, true)]
    [InlineData("203.0.113.10", false, true)]
    public async Task HealthCheckTargetValidator_EnforcesNetworkPolicy(
        string address,
        bool allowPrivate,
        bool expected)
    {
        var validator = new HealthCheckTargetValidator(
            new UrlSafetyValidator(),
            new StubDnsResolver(IPAddress.Parse(address)),
            Options.Create(new HealthCheckOptions
            {
                AllowedHosts = ["health.example.test"],
                AllowedPorts = [443],
                AllowPrivateAddresses = allowPrivate
            }));

        Assert.Equal(expected, await validator.IsAllowedAsync(
            "https://health.example.test/status"));
        Assert.False(await validator.IsAllowedAsync("https://other.example.test/status"));
    }

    private static ApplicationAsset NewApplication(
        string name,
        string? ownerUserId,
        string? healthCheckUrl) => new()
        {
            Code = $"APP-{Guid.NewGuid():N}",
            Name = name,
            OwnerUserId = ownerUserId,
            HealthCheckUrl = healthCheckUrl,
            AssetType = ApplicationAssetType.WebApplication
        };

    private static Deployment NewDeployment(ApplicationAsset application) => new()
    {
        ApplicationAsset = application,
        Version = "1.0.0-security-test",
        GitCommitSha = "0123456789abcdef0123456789abcdef01234567",
        ExecutedBy = "security-test",
        StartedAt = DateTimeOffset.UtcNow,
        Result = DeploymentResult.Succeeded,
        Environment = "Test"
    };

    private sealed class StubDnsResolver(IPAddress address) : IDnsResolver
    {
        public Task<IPAddress[]> GetHostAddressesAsync(
            string host,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new[] { address });
    }
}
