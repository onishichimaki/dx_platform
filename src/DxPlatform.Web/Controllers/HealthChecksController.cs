using System.Security.Claims;
using DxPlatform.Application.Abstractions;
using DxPlatform.Application.Security;
using DxPlatform.Application.Validation;
using DxPlatform.Domain.Operations;
using DxPlatform.Infrastructure.Configuration;
using DxPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DxPlatform.Web.Controllers;

[Authorize(Policy = PolicyNames.ApplicationManagement)]
[Route("admin/health-checks")]
public sealed class HealthChecksController(
    AppDbContext dbContext,
    IApplicationHealthCheckService healthCheckService,
    IUrlSafetyValidator urlValidator,
    IOptions<HealthCheckOptions> options,
    IAuditService auditService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewBag.Applications = await dbContext.ApplicationAssets.AsNoTracking()
            .Where(x => x.IsActive && x.HealthCheckUrl != null)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return View(await dbContext.HealthCheckResults.AsNoTracking()
            .Include(x => x.ApplicationAsset)
            .OrderByDescending(x => x.CheckedAt)
            .Take(100)
            .ToListAsync(cancellationToken));
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{applicationId:guid}/run")]
    public async Task<IActionResult> Run(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var app = await dbContext.ApplicationAssets
            .SingleOrDefaultAsync(x => x.Id == applicationId && x.IsActive, cancellationToken);
        if (app is null)
        {
            return NotFound();
        }

        if (!urlValidator.IsSafeHttpUrl(app.HealthCheckUrl, allowEmpty: false))
        {
            TempData["ErrorMessage"] = "安全なヘルスチェックURLが登録されていません。";
            return RedirectToAction(nameof(Index));
        }

        var lastCheck = await dbContext.HealthCheckResults
            .Where(x => x.ApplicationAssetId == applicationId)
            .OrderByDescending(x => x.CheckedAt)
            .Select(x => (DateTimeOffset?)x.CheckedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (lastCheck > DateTimeOffset.UtcNow.AddSeconds(-30))
        {
            TempData["ErrorMessage"] = "過剰アクセス防止のため、30秒以上空けて実行してください。";
            return RedirectToAction(nameof(Index));
        }

        var timeout = TimeSpan.FromSeconds(Math.Clamp(options.Value.TimeoutSeconds, 1, 60));
        var execution = await healthCheckService.CheckAsync(
            app.HealthCheckUrl!,
            timeout,
            cancellationToken);
        var result = new HealthCheckResult
        {
            ApplicationAssetId = applicationId,
            Status = execution.Status,
            HttpStatusCode = execution.HttpStatusCode,
            ResponseTimeMilliseconds = execution.ResponseTimeMilliseconds,
            CheckedAt = DateTimeOffset.UtcNow,
            ErrorSummary = execution.ErrorSummary,
            CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier),
            UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)
        };
        dbContext.HealthCheckResults.Add(result);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "HealthCheckExecuted",
            "ApplicationAsset",
            applicationId.ToString(),
            true,
            $"手動ヘルスチェックを実行しました。結果: {result.Status}",
            cancellationToken);
        TempData["StatusMessage"] = $"ヘルスチェックを実行しました: {result.Status}";
        return RedirectToAction(nameof(Index));
    }
}

