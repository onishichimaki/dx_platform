using System.Net;
using DxPlatform.Application.Security;
using DxPlatform.Domain.Applications;
using DxPlatform.Domain.Enums;
using DxPlatform.Domain.Groups;
using DxPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DxPlatform.IntegrationTests;

public sealed class AuthenticationAndAuthorizationTests(
    DxPlatformWebApplicationFactory factory)
    : IClassFixture<DxPlatformWebApplicationFactory>
{
    [Fact]
    public async Task AnonymousDashboardAccess_RedirectsToLogin()
    {
        using var client = IntegrationTestHelpers.CreateClient(factory);
        using var response = await client.GetAsync("/dashboard");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task BootstrapAdmin_CanLoginAndOpenAdminPage()
    {
        using var client = IntegrationTestHelpers.CreateClient(factory);
        await IntegrationTestHelpers.LoginAsync(
            client,
            DxPlatformWebApplicationFactory.AdminEmail,
            DxPlatformWebApplicationFactory.AdminPassword);

        using var response = await client.GetAsync("/admin/users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_CannotOpenAdminUsersPage()
    {
        var email = $"viewer-{Guid.NewGuid():N}@example.test";
        await IntegrationTestHelpers.CreateUserAsync(factory, email, RoleNames.Viewer);
        using var client = IntegrationTestHelpers.CreateClient(factory);
        await IntegrationTestHelpers.LoginAsync(
            client,
            email,
            DxPlatformWebApplicationFactory.ViewerPassword);

        using var response = await client.GetAsync("/admin/users");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/access-denied", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task GroupViewPermission_MakesApplicationVisible()
    {
        var email = $"group-viewer-{Guid.NewGuid():N}@example.test";
        var user = await IntegrationTestHelpers.CreateUserAsync(factory, email, RoleNames.Viewer);
        var appName = $"Group Visible {Guid.NewGuid():N}";

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var group = new Group { Code = $"G-{Guid.NewGuid():N}", Name = "Test Group" };
            var app = new ApplicationAsset
            {
                Code = $"APP-{Guid.NewGuid():N}",
                Name = appName,
                AssetType = ApplicationAssetType.WebApplication
            };
            dbContext.AddRange(group, app);
            dbContext.GroupMembers.Add(new GroupMember { Group = group, UserId = user.Id });
            dbContext.ApplicationPermissions.Add(new ApplicationPermission
            {
                ApplicationAsset = app,
                GroupId = group.Id,
                Level = PermissionLevel.View
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = IntegrationTestHelpers.CreateClient(factory);
        await IntegrationTestHelpers.LoginAsync(
            client,
            email,
            DxPlatformWebApplicationFactory.ViewerPassword);
        using var response = await client.GetAsync("/apps");
        response.EnsureSuccessStatusCode();
        Assert.Contains(appName, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ViewerWithoutPermission_CannotOpenApplicationDirectly()
    {
        var email = $"denied-viewer-{Guid.NewGuid():N}@example.test";
        await IntegrationTestHelpers.CreateUserAsync(factory, email, RoleNames.Viewer);
        Guid appId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var app = new ApplicationAsset
            {
                Code = $"DENIED-{Guid.NewGuid():N}",
                Name = "Denied Test App",
                AssetType = ApplicationAssetType.WebApplication
            };
            dbContext.ApplicationAssets.Add(app);
            await dbContext.SaveChangesAsync();
            appId = app.Id;
        }

        using var client = IntegrationTestHelpers.CreateClient(factory);
        await IntegrationTestHelpers.LoginAsync(
            client,
            email,
            DxPlatformWebApplicationFactory.ViewerPassword);
        using var response = await client.GetAsync($"/apps/{appId}");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/access-denied", response.Headers.Location?.AbsolutePath);
    }
}
