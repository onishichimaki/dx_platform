using System.Security.Claims;
using DxPlatform.Application.Abstractions;
using DxPlatform.Application.Security;
using DxPlatform.Domain.Applications;
using DxPlatform.Infrastructure.Persistence;
using DxPlatform.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DxPlatform.Web.Controllers;

[Authorize(Policy = PolicyNames.SystemAdminOnly)]
[Route("admin/permissions")]
public sealed class AdminPermissionsController(
    AppDbContext dbContext,
    IAuditService auditService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid? applicationId, CancellationToken cancellationToken)
    {
        var apps = await dbContext.ApplicationAssets.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        applicationId ??= apps.FirstOrDefault()?.Id;
        var permissions = applicationId is null
            ? []
            : await dbContext.ApplicationPermissions.AsNoTracking()
                .Where(x => x.ApplicationAssetId == applicationId && x.IsActive)
                .OrderBy(x => x.UserId)
                .ThenBy(x => x.GroupId)
                .ToListAsync(cancellationToken);

        ViewBag.Applications = apps;
        ViewBag.SelectedApplicationId = applicationId;
        ViewBag.Users = await dbContext.Users.AsNoTracking()
            .Where(x => x.IsActive).OrderBy(x => x.DisplayName).ToListAsync(cancellationToken);
        ViewBag.Groups = await dbContext.Groups.AsNoTracking()
            .Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return View(permissions);
    }

    [ValidateAntiForgeryToken]
    [HttpPost("")]
    public async Task<IActionResult> Save(
        PermissionEditViewModel model,
        CancellationToken cancellationToken)
    {
        string? userId = null;
        Guid? groupId = null;
        if (model.PrincipalType == "User")
        {
            userId = model.PrincipalId;
            if (!await dbContext.Users.AnyAsync(
                    x => x.Id == userId && x.IsActive, cancellationToken))
            {
                ModelState.AddModelError(nameof(model.PrincipalId), "有効なユーザーを選択してください。");
            }
        }
        else if (model.PrincipalType == "Group")
        {
            if (!Guid.TryParse(model.PrincipalId, out var parsedGroupId) ||
                !await dbContext.Groups.AnyAsync(
                    x => x.Id == parsedGroupId && x.IsActive, cancellationToken))
            {
                ModelState.AddModelError(nameof(model.PrincipalId), "有効なグループを選択してください。");
            }
            else
            {
                groupId = parsedGroupId;
            }
        }

        if (!await dbContext.ApplicationAssets.AnyAsync(
                x => x.Id == model.ApplicationAssetId && x.IsActive, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.ApplicationAssetId), "有効なアプリを選択してください。");
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = string.Join(" ", ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage));
            return RedirectToAction(nameof(Index), new { applicationId = model.ApplicationAssetId });
        }

        var permission = await dbContext.ApplicationPermissions
            .SingleOrDefaultAsync(
                x => x.ApplicationAssetId == model.ApplicationAssetId &&
                     ((userId != null && x.UserId == userId) ||
                      (groupId != null && x.GroupId == groupId)),
                cancellationToken);
        if (permission is null)
        {
            permission = new ApplicationPermission
            {
                ApplicationAssetId = model.ApplicationAssetId,
                UserId = userId,
                GroupId = groupId,
                CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)
            };
            dbContext.ApplicationPermissions.Add(permission);
        }

        permission.Level = model.Level;
        permission.IsActive = true;
        permission.UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "ApplicationPermissionChanged",
            "ApplicationPermission",
            permission.Id.ToString(),
            true,
            $"{model.PrincipalType}のアプリ権限を{model.Level}へ変更しました。",
            cancellationToken);
        TempData["StatusMessage"] = "アプリ権限を保存しました。";
        return RedirectToAction(nameof(Index), new { applicationId = model.ApplicationAssetId });
    }

    [HttpGet("{id:guid}/remove")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken cancellationToken)
    {
        var permission = await dbContext.ApplicationPermissions.AsNoTracking()
            .Include(x => x.ApplicationAsset)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return permission is null ? NotFound() : View(permission);
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{id:guid}/remove")]
    public async Task<IActionResult> RemoveConfirmed(Guid id, CancellationToken cancellationToken)
    {
        var permission = await dbContext.ApplicationPermissions.FindAsync([id], cancellationToken);
        if (permission is null)
        {
            return NotFound();
        }

        permission.IsActive = false;
        permission.UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "ApplicationPermissionChanged",
            "ApplicationPermission",
            permission.Id.ToString(),
            true,
            "アプリ権限設定を無効化しました。",
            cancellationToken);
        TempData["StatusMessage"] = "アプリ権限を解除しました。";
        return RedirectToAction(nameof(Index), new { applicationId = permission.ApplicationAssetId });
    }
}

