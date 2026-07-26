using System.Security.Claims;
using DxPlatform.Application.Abstractions;
using DxPlatform.Application.Security;
using DxPlatform.Application.Validation;
using DxPlatform.Domain.Enums;
using DxPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DxPlatform.Web.Controllers;

[Authorize]
[Route("apps")]
public sealed class AppsController(
    AppDbContext dbContext,
    IApplicationAccessService accessService,
    IUrlSafetyValidator urlValidator) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? keyword,
        ApplicationAssetType? type,
        LifecycleStatus? status,
        string? tag,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var visibleIds = await accessService.GetVisibleApplicationIdsAsync(
            userId,
            User.IsInRole(RoleNames.SystemAdmin),
            cancellationToken);

        var query = dbContext.ApplicationAssets.AsNoTracking()
            .Where(x => x.IsActive && visibleIds.Contains(x.Id));
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) ||
                                     x.Code.Contains(keyword) ||
                                     (x.Description != null && x.Description.Contains(keyword)));
        }

        if (type.HasValue)
        {
            query = query.Where(x => x.AssetType == type);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            query = query.Where(x => x.TagsText != null && x.TagsText.Contains(tag));
        }

        ViewBag.Keyword = keyword;
        ViewBag.Type = type;
        ViewBag.Status = status;
        ViewBag.Tag = tag;
        return View(await query.OrderBy(x => x.Name).ToListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var app = await dbContext.ApplicationAssets.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);
        if (app is null)
        {
            return NotFound();
        }

        var level = await GetLevelAsync(id, cancellationToken);
        if (level < PermissionLevel.View)
        {
            return Forbid();
        }

        ViewBag.PermissionLevel = level;
        ViewBag.RelatedDataAssets = await dbContext.ApplicationDataAssets.AsNoTracking()
            .Where(x => x.ApplicationAssetId == id && x.DataAsset.IsActive)
            .Select(x => x.DataAsset)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        ViewBag.HealthChecks = await dbContext.HealthCheckResults.AsNoTracking()
            .Where(x => x.ApplicationAssetId == id)
            .OrderByDescending(x => x.CheckedAt)
            .Take(10)
            .ToListAsync(cancellationToken);
        ViewBag.Deployments = await dbContext.Deployments.AsNoTracking()
            .Where(x => x.ApplicationAssetId == id)
            .OrderByDescending(x => x.StartedAt)
            .Take(10)
            .ToListAsync(cancellationToken);
        return View(app);
    }

    [HttpGet("{id:guid}/launch")]
    public async Task<IActionResult> Launch(Guid id, CancellationToken cancellationToken)
    {
        var app = await dbContext.ApplicationAssets.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);
        if (app is null)
        {
            return NotFound();
        }

        if (await GetLevelAsync(id, cancellationToken) < PermissionLevel.Use)
        {
            return Forbid();
        }

        if (!urlValidator.IsSafeHttpUrl(app.Url, allowEmpty: false))
        {
            return BadRequest("このアプリには安全な起動URLが登録されていません。");
        }

        return Redirect(app.Url!);
    }

    private Task<PermissionLevel> GetLevelAsync(Guid id, CancellationToken cancellationToken) =>
        accessService.GetLevelAsync(
            id,
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            User.IsInRole(RoleNames.SystemAdmin),
            cancellationToken);
}

