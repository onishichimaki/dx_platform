using DxPlatform.Application.Security;
using DxPlatform.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DxPlatform.Web.Controllers;

[Authorize(Policy = PolicyNames.SystemAdminOnly)]
[Route("settings")]
public sealed class SettingsController(
    IConfiguration configuration,
    IWebHostEnvironment environment) : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        var safeSettings = new Dictionary<string, string>
        {
            ["実行環境"] = environment.EnvironmentName,
            ["DB Provider"] = configuration["Database:Provider"] ?? "未設定",
            ["デモデータ"] = configuration.GetValue("DemoData:Enabled", false) ? "有効" : "無効",
            ["初期管理者設定"] =
                HasValue($"{BootstrapAdminOptions.SectionName}:Email") &&
                HasValue($"{BootstrapAdminOptions.SectionName}:Password")
                    ? "設定済み"
                    : "未設定",
            ["Deployment API"] = HasValue($"{DeploymentApiOptions.SectionName}:Token")
                ? "設定済み"
                : "未設定",
            ["Health Check timeout"] =
                $"{configuration.GetValue($"{HealthCheckOptions.SectionName}:TimeoutSeconds", 10)} 秒"
        };
        return View(safeSettings);
    }

    private bool HasValue(string key) => !string.IsNullOrWhiteSpace(configuration[key]);
}

