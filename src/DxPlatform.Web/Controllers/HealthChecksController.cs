using System.Security.Claims;
using DxPlatform.Application.Abstractions;
using DxPlatform.Application.Security;
using DxPlatform.Domain.Enums;
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
    IHealthCheckTargetValidator targetValidator,
    IApplicationAccessService accessService,
    IOptions<HealthCheckOptions> options,
    IAuditService auditService,
    ILogger<HealthChecksController> logger) : Controller
{
    private static readonly Action<ILogger, Guid, Guid, HealthStatus, Exception?>
        HealthCheckCompleted = LoggerMessage.Define<Guid, Guid, HealthStatus>(
            LogLevel.Information,
            new EventId(2201, nameof(HealthCheckCompleted)),
            "Manual health check completed. AssetId={AssetId} HealthCheckResultId={HealthCheckResultId} Status={Status}");

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var manageableIds = await GetManageableIdsAsync(cancellationToken);
        ViewBag.Applications = await dbContext.ApplicationAssets.AsNoTracking()
            .Where(x => x.IsActive && x.HealthCheckUrl != null && manageableIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return View(await dbContext.HealthCheckResults.AsNoTracking()
            .Include(x => x.ApplicationAsset)
            .Where(x => manageableIds.Contains(x.ApplicationAssetId))
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
        var manageableIds = await GetManageableIdsAsync(cancellationToken);
        if (!manageableIds.Contains(applicationId))
        {
            return Forbid();
        }

        var app = await dbContext.ApplicationAssets
            .SingleOrDefaultAsync(x => x.Id == applicationId && x.IsActive, cancellationToken);
        if (app is null)
        {
            return NotFound();
        }

        if (!await targetValidator.IsAllowedAsync(app.HealthCheckUrl, cancellationToken))
        {
            TempData["ErrorMessage"] = "ヘルスチェックURLが会社許可リストまたはネットワーク安全条件を満たしていません。";
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
        HealthCheckCompleted(
            logger,
            applicationId,
            result.Id,
            result.Status,
            null);
        TempData["StatusMessage"] = $"ヘルスチェックを実行しました: {result.Status}";
        return RedirectToAction(nameof(Index));
    }

    private Task<IReadOnlySet<Guid>> GetManageableIdsAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var hasGlobalAccess = User.IsInRole(RoleNames.SystemAdmin) ||
                              User.IsInRole(RoleNames.CatalogAdmin);
        return accessService.GetManageableApplicationIdsAsync(
            userId,
            hasGlobalAccess,
            cancellationToken);
    }
}
