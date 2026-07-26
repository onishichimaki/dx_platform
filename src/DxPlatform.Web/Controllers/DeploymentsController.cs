using System.Security.Claims;
using DxPlatform.Application.Abstractions;
using DxPlatform.Application.Security;
using DxPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DxPlatform.Web.Controllers;

[Authorize]
[Route("deployments")]
public sealed class DeploymentsController(
    AppDbContext dbContext,
    IApplicationAccessService accessService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        Guid? applicationId,
        string? environment,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var hasGlobalAccess = User.IsInRole(RoleNames.SystemAdmin) ||
                              User.IsInRole(RoleNames.CatalogAdmin);
        var visibleIds = await accessService.GetVisibleApplicationIdsAsync(
            userId,
            hasGlobalAccess,
            cancellationToken);
        var query = dbContext.Deployments.AsNoTracking()
            .Include(x => x.ApplicationAsset)
            .Where(x => visibleIds.Contains(x.ApplicationAssetId))
            .AsQueryable();
        if (applicationId.HasValue)
        {
            query = query.Where(x => x.ApplicationAssetId == applicationId);
        }

        if (!string.IsNullOrWhiteSpace(environment))
        {
            query = query.Where(x => x.Environment == environment);
        }

        ViewBag.ApplicationId = applicationId;
        ViewBag.Environment = environment;
        ViewBag.Applications = await dbContext.ApplicationAssets.AsNoTracking()
            .Where(x => x.IsActive && visibleIds.Contains(x.Id))
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return View(await query.OrderByDescending(x => x.StartedAt)
            .Take(200)
            .ToListAsync(cancellationToken));
    }
}
