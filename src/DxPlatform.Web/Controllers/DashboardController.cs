using System.Security.Claims;
using DxPlatform.Application.Abstractions;
using DxPlatform.Application.Security;
using DxPlatform.Domain.Enums;
using DxPlatform.Infrastructure.Persistence;
using DxPlatform.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DxPlatform.Web.Controllers;

[Authorize]
[Route("dashboard")]
public sealed class DashboardController(
    AppDbContext dbContext,
    IApplicationAccessService accessService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var reviewThreshold = DateTimeOffset.UtcNow.AddDays(-90);
        var isSystemAdmin = User.IsInRole(RoleNames.SystemAdmin);
        var hasGlobalApplicationAccess = isSystemAdmin || User.IsInRole(RoleNames.CatalogAdmin);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var visibleApplicationIds = await accessService.GetVisibleApplicationIdsAsync(
            userId,
            hasGlobalApplicationAccess,
            cancellationToken);

        var model = new DashboardViewModel
        {
            ApplicationCount = await dbContext.ApplicationAssets.CountAsync(
                x => x.IsActive && visibleApplicationIds.Contains(x.Id), cancellationToken),
            ProductionApplicationCount = await dbContext.ApplicationAssets.CountAsync(
                x => x.IsActive && visibleApplicationIds.Contains(x.Id) &&
                     x.Status == LifecycleStatus.Production, cancellationToken),
            DataAssetCount = await dbContext.DataAssets.CountAsync(x => x.IsActive, cancellationToken),
            AgentCount = await dbContext.AgentAssets.CountAsync(x => x.IsActive, cancellationToken),
            SuspendedAssetCount =
                await dbContext.ApplicationAssets.CountAsync(x => x.IsActive && visibleApplicationIds.Contains(x.Id) && x.Status == LifecycleStatus.Suspended, cancellationToken) +
                await dbContext.DataAssets.CountAsync(x => x.IsActive && x.Status == LifecycleStatus.Suspended, cancellationToken) +
                await dbContext.AgentAssets.CountAsync(x => x.IsActive && x.Status == LifecycleStatus.Suspended, cancellationToken),
            MissingManagerCount =
                await dbContext.ApplicationAssets.CountAsync(x => x.IsActive && visibleApplicationIds.Contains(x.Id) && x.ManagerName == null, cancellationToken) +
                await dbContext.AgentAssets.CountAsync(x => x.IsActive && x.ManagerName == null, cancellationToken),
            MissingConfidentialityCount =
                await dbContext.ApplicationAssets.CountAsync(x => x.IsActive && visibleApplicationIds.Contains(x.Id) && x.ConfidentialityClassification == null, cancellationToken) +
                await dbContext.DataAssets.CountAsync(x => x.IsActive && x.ConfidentialityClassification == null, cancellationToken),
            OverdueReviewCount =
                await dbContext.ApplicationAssets.CountAsync(
                    x => x.IsActive && visibleApplicationIds.Contains(x.Id) &&
                         (x.LastReviewedAt == null || x.LastReviewedAt < reviewThreshold), cancellationToken) +
                await dbContext.DataAssets.CountAsync(
                    x => x.IsActive && (x.LastReviewedAt == null || x.LastReviewedAt < reviewThreshold), cancellationToken) +
                await dbContext.AgentAssets.CountAsync(
                    x => x.IsActive && (x.LastReviewedAt == null || x.LastReviewedAt < reviewThreshold), cancellationToken),
            RecentDeployments = await dbContext.Deployments
                .AsNoTracking()
                .Include(x => x.ApplicationAsset)
                .Where(x => visibleApplicationIds.Contains(x.ApplicationAssetId))
                .OrderByDescending(x => x.StartedAt)
                .Take(5)
                .ToListAsync(cancellationToken),
            RecentAuditLogs = isSystemAdmin
                ? await dbContext.AuditLogs.AsNoTracking()
                    .OrderByDescending(x => x.OccurredAt)
                    .Take(5)
                    .ToListAsync(cancellationToken)
                : []
        };

        return View(model);
    }
}
