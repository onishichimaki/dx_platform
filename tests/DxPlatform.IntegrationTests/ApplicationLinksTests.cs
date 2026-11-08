using System.Net;
using DxPlatform.Domain.Agents;
using DxPlatform.Domain.Applications;
using DxPlatform.Domain.Enums;
using DxPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DxPlatform.IntegrationTests;

/// <summary>アプリ詳細／Agent詳細の関連リンク（GitHub、資料フォルダ、Runbook）の表示と入力検証。</summary>
public sealed class ApplicationLinksTests(DxPlatformWebApplicationFactory factory)
    : IClassFixture<DxPlatformWebApplicationFactory>
{
    private const string GitHubUrl = "https://github.example.test/sample/sample-app";
    private const string DocumentsUrl = "https://sharepoint.example.test/sites/sample/Shared%20Documents/sample-app";
    private const string RunbookUrl = "https://wiki.example.test/runbooks/sample-app";

    [Fact]
    public async Task Admin_CanRegisterLinks_AndTheyAreShownAsSafeLinksOnTheDetailsPage()
    {
        using var client = await LoginAsAdminAsync();
        var code = $"LINK-{Guid.NewGuid():N}";
        await PostCreateAsync(client, code, GitHubUrl, DocumentsUrl, RunbookUrl, expected: HttpStatusCode.Redirect);

        var id = await FindAppIdAsync(code);
        var html = await GetDecodedAsync(client, $"/apps/{id}");

        foreach (var url in new[] { GitHubUrl, DocumentsUrl, RunbookUrl })
        {
            Assert.Contains($"href=\"{url}\"", html);
        }

        // 外部サイトへのリンクは、新しいタブで開き、opener/referrerを渡さない（URL未設定のため「アプリを開く」ボタンは出ない）。
        Assert.Equal(3, CountOccurrences(html, "rel=\"noopener noreferrer\""));
        Assert.Equal(3, CountOccurrences(html, "target=\"_blank\""));
        Assert.Contains("関連リンク", html);

        await using var scope = factory.Services.CreateAsyncScope();
        var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .ApplicationAssets.AsNoTracking().SingleAsync(x => x.Code == code);
        Assert.Equal(DocumentsUrl, saved.DocumentsUrl);
        Assert.Equal(RunbookUrl, saved.RunbookUrl);
    }

    [Fact]
    public async Task Details_ShowsUnregisteredWhenNoLinksAreSet()
    {
        using var client = await LoginAsAdminAsync();
        var code = $"NOLINK-{Guid.NewGuid():N}";
        await PostCreateAsync(client, code, null, null, null, expected: HttpStatusCode.Redirect);

        var html = await GetDecodedAsync(client, $"/apps/{await FindAppIdAsync(code)}");

        Assert.Contains("関連リンク", html);
        Assert.Equal(3, CountOccurrences(html, "未登録"));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://files.example.test/docs")]
    [InlineData("https://user:secret@docs.example.test/")]
    [InlineData("/relative/path")]
    public async Task UnsafeUrls_AreRejected_ForDocumentsAndRunbook_AndNothingIsSaved(string unsafeUrl)
    {
        using var client = await LoginAsAdminAsync();

        foreach (var (field, label) in new[] { ("DocumentsUrl", "資料フォルダURL"), ("RunbookUrl", "RunbookURL") })
        {
            var code = $"BAD-{Guid.NewGuid():N}";
            var response = await PostCreateAsync(
                client,
                code,
                null,
                field == "DocumentsUrl" ? unsafeUrl : null,
                field == "RunbookUrl" ? unsafeUrl : null,
                expected: HttpStatusCode.OK);

            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains($"{label}はHTTPまたはHTTPSの安全な絶対URLを指定してください。", html);

            await using var scope = factory.Services.CreateAsyncScope();
            Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                .ApplicationAssets.AnyAsync(x => x.Code == code));
        }
    }

    [Fact]
    public async Task Details_DoesNotRenderAnUnsafeStoredUrlAsALink()
    {
        // 入力検証を経ずに保存された値（過去のデータ、直接のDB更新）に備え、表示時にも検査する。
        var code = $"LEGACY-{Guid.NewGuid():N}";
        Guid id;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = new ApplicationAsset
            {
                Code = code,
                Name = "Legacy link application",
                AssetType = ApplicationAssetType.WebApplication,
                Status = LifecycleStatus.Draft,
                GitHubRepositoryUrl = "javascript:alert(document.domain)",
                DocumentsUrl = "https://sharepoint.example.test/sites/legacy"
            };
            dbContext.ApplicationAssets.Add(entity);
            await dbContext.SaveChangesAsync();
            id = entity.Id;
        }

        using var client = await LoginAsAdminAsync();
        var html = await GetDecodedAsync(client, $"/apps/{id}");

        Assert.DoesNotContain("href=\"javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("安全なURLではないため、リンクにしていません", html);
        Assert.Contains("href=\"https://sharepoint.example.test/sites/legacy\"", html);
    }

    [Fact]
    public async Task AgentDetails_ShowsTheGitHubRepositoryAsALink()
    {
        Guid id;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = new AgentAsset
            {
                Name = $"Link agent {Guid.NewGuid():N}",
                AgentType = AgentType.Custom,
                Platform = "Sample platform",
                Purpose = "Link display test",
                Status = LifecycleStatus.Draft,
                GitHubRepositoryUrl = GitHubUrl
            };
            dbContext.AgentAssets.Add(entity);
            await dbContext.SaveChangesAsync();
            id = entity.Id;
        }

        using var client = await LoginAsAdminAsync();
        var html = await GetDecodedAsync(client, $"/agents/{id}");

        Assert.Contains($"href=\"{GitHubUrl}\"", html);
    }

    private async Task<HttpClient> LoginAsAdminAsync()
    {
        var client = IntegrationTestHelpers.CreateClient(factory);
        await IntegrationTestHelpers.LoginAsync(
            client,
            DxPlatformWebApplicationFactory.AdminEmail,
            DxPlatformWebApplicationFactory.AdminPassword);
        return client;
    }

    private static async Task<HttpResponseMessage> PostCreateAsync(
        HttpClient client,
        string code,
        string? gitHubUrl,
        string? documentsUrl,
        string? runbookUrl,
        HttpStatusCode expected)
    {
        var token = await IntegrationTestHelpers.GetAntiForgeryTokenAsync(client, "/admin/apps/create");
        var fields = new Dictionary<string, string>
        {
            ["Code"] = code,
            ["Name"] = "Link test application",
            ["AssetType"] = ApplicationAssetType.WebApplication.ToString(),
            ["Status"] = LifecycleStatus.Draft.ToString(),
            ["__RequestVerificationToken"] = token
        };
        if (gitHubUrl is not null) { fields["GitHubRepositoryUrl"] = gitHubUrl; }
        if (documentsUrl is not null) { fields["DocumentsUrl"] = documentsUrl; }
        if (runbookUrl is not null) { fields["RunbookUrl"] = runbookUrl; }

        var response = await client.PostAsync("/admin/apps/create", new FormUrlEncodedContent(fields));
        Assert.Equal(expected, response.StatusCode);
        return response;
    }

    private async Task<Guid> FindAppIdAsync(string code)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .ApplicationAssets.AsNoTracking().Where(x => x.Code == code).Select(x => x.Id).SingleAsync();
    }

    private static async Task<string> GetDecodedAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal);
             index >= 0;
             index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
