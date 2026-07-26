using System.Net;
using System.Text.RegularExpressions;
using DxPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DxPlatform.IntegrationTests;

internal static partial class IntegrationTestHelpers
{
    public static HttpClient CreateClient(DxPlatformWebApplicationFactory factory) =>
        factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    public static async Task LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        var token = await GetAntiForgeryTokenAsync(client, "/login");
        using var response = await client.PostAsync(
            "/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = password,
                ["RememberMe"] = "false",
                ["__RequestVerificationToken"] = token
            }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/dashboard", response.Headers.Location?.OriginalString);
    }

    public static async Task<string> GetAntiForgeryTokenAsync(
        HttpClient client,
        string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var match = AntiForgeryTokenRegex().Match(html);
        Assert.True(match.Success, $"Anti-forgery token was not found at {path}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    public static async Task<ApplicationUser> CreateUserAsync(
        DxPlatformWebApplicationFactory factory,
        string email,
        string role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = $"Test User {Guid.NewGuid():N}"[..24],
                EmailConfirmed = true,
                IsActive = true
            };
            var result = await userManager.CreateAsync(
                user,
                DxPlatformWebApplicationFactory.ViewerPassword);
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(x => x.Code)));
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var roleResult = await userManager.AddToRoleAsync(user, role);
            Assert.True(roleResult.Succeeded);
        }

        return user;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiForgeryTokenRegex();
}
