using System.Security.Claims;
using DxPlatform.Application.Abstractions;
using DxPlatform.Application.Applications;
using DxPlatform.Application.Security;
using DxPlatform.Application.Validation;
using DxPlatform.Domain.Applications;
using DxPlatform.Domain.Enums;
using DxPlatform.Infrastructure.Identity;
using DxPlatform.Infrastructure.Persistence;
using DxPlatform.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DxPlatform.Web.Controllers;

[Authorize]
[Route("admin/apps")]
public sealed class AdminAppsController(
    AppDbContext dbContext,
    IApplicationAccessService accessService,
    IUrlSafetyValidator urlValidator,
    IAuditService auditService,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? keyword, CancellationToken cancellationToken)
    {
        var query = dbContext.ApplicationAssets.AsNoTracking().AsQueryable();
        if (!User.IsInRole(RoleNames.SystemAdmin) &&
            !User.IsInRole(RoleNames.CatalogAdmin))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var candidates = await dbContext.ApplicationAssets
                .Where(x => x.OwnerUserId == userId)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            var visible = await accessService.GetVisibleApplicationIdsAsync(
                userId, false, cancellationToken);
            foreach (var id in visible)
            {
                if (await accessService.GetLevelAsync(id, userId, false, cancellationToken) ==
                    PermissionLevel.Manage)
                {
                    candidates.Add(id);
                }
            }

            query = query.Where(x => candidates.Contains(x.Id));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || x.Code.Contains(keyword));
        }

        ViewBag.Keyword = keyword;
        return View(await query.OrderBy(x => x.Name).ToListAsync(cancellationToken));
    }

    [Authorize(Policy = PolicyNames.CatalogManagement)]
    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        await SetOwnerChoicesAsync();
        return View("Edit", new ApplicationAssetInputModel());
    }

    [Authorize(Policy = PolicyNames.CatalogManagement)]
    [ValidateAntiForgeryToken]
    [HttpPost("create")]
    public async Task<IActionResult> Create(
        ApplicationAssetInputModel model,
        CancellationToken cancellationToken)
    {
        ValidateUrls(model);
        if (await dbContext.ApplicationAssets.AnyAsync(x => x.Code == model.Code, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Code), "同じアプリコードが登録されています。");
        }

        if (!ModelState.IsValid)
        {
            await SetOwnerChoicesAsync();
            return View("Edit", model);
        }

        var entity = new ApplicationAsset();
        model.ApplyTo(entity);
        entity.CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        entity.UpdatedBy = entity.CreatedBy;
        dbContext.ApplicationAssets.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "ApplicationCreated", "ApplicationAsset", entity.Id.ToString(), true,
            $"アプリ「{entity.Name}」を登録しました。",
            cancellationToken);
        TempData["StatusMessage"] = "アプリを登録しました。";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.ApplicationAssets.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(entity, cancellationToken))
        {
            return Forbid();
        }

        await SetOwnerChoicesAsync();
        return View(ApplicationAssetInputModel.FromEntity(entity));
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{id:guid}/edit")]
    public async Task<IActionResult> Edit(
        Guid id,
        ApplicationAssetInputModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var entity = await dbContext.ApplicationAssets.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(entity, cancellationToken))
        {
            return Forbid();
        }

        ValidateUrls(model);
        if (!LifecycleRules.CanTransition(entity.Status, model.Status))
        {
            ModelState.AddModelError(nameof(model.Status), "許可されていないステータス遷移です。");
        }

        if (await dbContext.ApplicationAssets.AnyAsync(
                x => x.Code == model.Code && x.Id != id, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Code), "同じアプリコードが登録されています。");
        }

        if (!TryMatchRowVersion(model.RowVersion, entity.RowVersion))
        {
            ModelState.AddModelError(string.Empty, "他の利用者が更新しました。画面を再読み込みしてください。");
        }

        if (!ModelState.IsValid)
        {
            model.RowVersion = Convert.ToBase64String(entity.RowVersion);
            await SetOwnerChoicesAsync();
            return View(model);
        }

        model.ApplyTo(entity);
        entity.UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "ApplicationUpdated", "ApplicationAsset", entity.Id.ToString(), true,
            $"アプリ「{entity.Name}」を変更しました。",
            cancellationToken);
        TempData["StatusMessage"] = "アプリを更新しました。";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.ApplicationAssets.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        return await CanManageAsync(entity, cancellationToken) ? View(entity) : Forbid();
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{id:guid}/disable")]
    public async Task<IActionResult> DisableConfirmed(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.ApplicationAssets.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(entity, cancellationToken))
        {
            return Forbid();
        }

        entity.IsActive = false;
        entity.UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "ApplicationDisabled", "ApplicationAsset", entity.Id.ToString(), true,
            $"アプリ「{entity.Name}」を無効化しました。",
            cancellationToken);
        TempData["StatusMessage"] = "アプリを無効化しました。";
        return RedirectToAction(nameof(Index));
    }

    private void ValidateUrls(ApplicationAssetInputModel model)
    {
        foreach (var (value, member, label) in new[]
                 {
                     (model.Url, nameof(model.Url), "URL"),
                     (model.HealthCheckUrl, nameof(model.HealthCheckUrl), "ヘルスチェックURL"),
                     (model.GitHubRepositoryUrl, nameof(model.GitHubRepositoryUrl), "GitHubリポジトリURL")
                 })
        {
            if (!urlValidator.IsSafeHttpUrl(value))
            {
                ModelState.AddModelError(member, $"{label}はHTTPまたはHTTPSの安全な絶対URLを指定してください。");
            }
        }
    }

    private async Task<bool> CanManageAsync(
        ApplicationAsset entity,
        CancellationToken cancellationToken)
    {
        if (User.IsInRole(RoleNames.SystemAdmin) || User.IsInRole(RoleNames.CatalogAdmin))
        {
            return true;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return entity.OwnerUserId == userId ||
               await accessService.GetLevelAsync(entity.Id, userId, false, cancellationToken) ==
               PermissionLevel.Manage;
    }

    private async Task SetOwnerChoicesAsync()
    {
        ViewBag.OwnerUsers = await userManager.Users
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .ToListAsync();
    }

    private static bool TryMatchRowVersion(string? value, byte[] current)
    {
        try
        {
            return value is not null &&
                   Convert.FromBase64String(value).SequenceEqual(current);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
