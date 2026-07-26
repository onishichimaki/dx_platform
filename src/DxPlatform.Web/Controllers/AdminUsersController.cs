using System.Security.Claims;
using DxPlatform.Application.Abstractions;
using DxPlatform.Application.Security;
using DxPlatform.Domain.Groups;
using DxPlatform.Infrastructure.Identity;
using DxPlatform.Infrastructure.Persistence;
using DxPlatform.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DxPlatform.Web.Controllers;

[Authorize(Policy = PolicyNames.SystemAdminOnly)]
[Route("admin/users")]
public sealed class AdminUsersController(
    UserManager<ApplicationUser> userManager,
    AppDbContext dbContext,
    IAuditService auditService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? keyword)
    {
        var query = userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.DisplayName.Contains(keyword) ||
                                     (x.Email != null && x.Email.Contains(keyword)) ||
                                     (x.Department != null && x.Department.Contains(keyword)));
        }

        var users = await query.OrderBy(x => x.DisplayName).ToListAsync();
        var items = new List<UserListItemViewModel>(users.Count);
        foreach (var user in users)
        {
            items.Add(new UserListItemViewModel(
                user.Id,
                user.DisplayName,
                user.Email ?? string.Empty,
                user.Department,
                user.IsActive,
                (await userManager.GetRolesAsync(user)).ToList(),
                user.LastLoginAt));
        }

        ViewBag.Keyword = keyword;
        return View(items);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        await SetChoicesAsync();
        return View("Edit", new UserEditViewModel { IsActive = true });
    }

    [ValidateAntiForgeryToken]
    [HttpPost("create")]
    public async Task<IActionResult> Create(
        UserEditViewModel model,
        CancellationToken cancellationToken)
    {
        ValidateRoles(model.SelectedRoles);
        if (string.IsNullOrWhiteSpace(model.InitialPassword))
        {
            ModelState.AddModelError(nameof(model.InitialPassword), "初期パスワードは必須です。");
        }

        if (!ModelState.IsValid)
        {
            await SetChoicesAsync();
            return View("Edit", model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            DisplayName = model.DisplayName.Trim(),
            Department = model.Department?.Trim(),
            IsActive = model.IsActive,
            EmailConfirmed = true,
            CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier),
            UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)
        };
        var result = await userManager.CreateAsync(user, model.InitialPassword!);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            await SetChoicesAsync();
            return View("Edit", model);
        }

        if (model.SelectedRoles.Count > 0)
        {
            await userManager.AddToRolesAsync(user, model.SelectedRoles.Distinct());
        }

        await SyncGroupsAsync(user.Id, model.SelectedGroupIds, cancellationToken);
        await auditService.WriteAsync(
            "UserCreated", "User", user.Id, true,
            "ユーザーを登録しました。",
            cancellationToken);
        TempData["StatusMessage"] = "ユーザーを登録しました。";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id}/edit")]
    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var model = new UserEditViewModel
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            Department = user.Department,
            IsActive = user.IsActive,
            SelectedRoles = (await userManager.GetRolesAsync(user)).ToList(),
            SelectedGroupIds = await dbContext.GroupMembers
                .Where(x => x.UserId == user.Id && x.IsActive)
                .Select(x => x.GroupId)
                .ToListAsync(cancellationToken),
            RowVersion = Convert.ToBase64String(user.RowVersion)
        };
        await SetChoicesAsync();
        return View(model);
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{id}/edit")]
    public async Task<IActionResult> Edit(
        string id,
        UserEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        ValidateRoles(model.SelectedRoles);
        if (!RowVersionMatches(model.RowVersion, user.RowVersion))
        {
            ModelState.AddModelError(string.Empty, "他の利用者が更新しました。画面を再読み込みしてください。");
        }

        if (!ModelState.IsValid)
        {
            model.RowVersion = Convert.ToBase64String(user.RowVersion);
            await SetChoicesAsync();
            return View(model);
        }

        user.DisplayName = model.DisplayName.Trim();
        user.Department = model.Department?.Trim();
        user.IsActive = model.IsActive;
        user.UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.RowVersion = Guid.NewGuid().ToByteArray();

        if (!string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await userManager.SetEmailAsync(user, model.Email.Trim());
            var nameResult = await userManager.SetUserNameAsync(user, model.Email.Trim());
            if (!emailResult.Succeeded || !nameResult.Succeeded)
            {
                AddIdentityErrors(emailResult);
                AddIdentityErrors(nameResult);
                model.RowVersion = Convert.ToBase64String(user.RowVersion);
                await SetChoicesAsync();
                return View(model);
            }
        }

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            AddIdentityErrors(updateResult);
            model.RowVersion = Convert.ToBase64String(user.RowVersion);
            await SetChoicesAsync();
            return View(model);
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        await userManager.RemoveFromRolesAsync(user, currentRoles.Except(model.SelectedRoles));
        await userManager.AddToRolesAsync(user, model.SelectedRoles.Except(currentRoles));
        await SyncGroupsAsync(user.Id, model.SelectedGroupIds, cancellationToken);

        await auditService.WriteAsync(
            "UserUpdated", "User", user.Id, true,
            "ユーザー、ロール、所属を変更しました。",
            cancellationToken);
        TempData["StatusMessage"] = "ユーザーを更新しました。";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id}/disable")]
    public async Task<IActionResult> Disable(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        return user is null ? NotFound() : View(user);
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{id}/disable")]
    public async Task<IActionResult> DisableConfirmed(
        string id,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = false;
        user.UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.RowVersion = Guid.NewGuid().ToByteArray();
        await userManager.UpdateAsync(user);
        await userManager.UpdateSecurityStampAsync(user);
        await auditService.WriteAsync(
            "UserDisabled", "User", user.Id, true,
            "ユーザーを無効化しました。",
            cancellationToken);
        TempData["StatusMessage"] = "ユーザーを無効化しました。";
        return RedirectToAction(nameof(Index));
    }

    private async Task SetChoicesAsync()
    {
        ViewBag.AllRoles = RoleNames.All;
        ViewBag.AllGroups = await dbContext.Groups.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    private void ValidateRoles(IEnumerable<string> roles)
    {
        if (roles.Any(role => !RoleRules.IsKnownRole(role)))
        {
            ModelState.AddModelError(nameof(UserEditViewModel.SelectedRoles), "未定義のロールが含まれています。");
        }
    }

    private async Task SyncGroupsAsync(
        string userId,
        IEnumerable<Guid> selectedGroupIds,
        CancellationToken cancellationToken)
    {
        var selected = selectedGroupIds.Distinct().ToHashSet();
        var memberships = await dbContext.GroupMembers
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var membership in memberships)
        {
            membership.IsActive = selected.Remove(membership.GroupId);
        }

        foreach (var groupId in selected)
        {
            dbContext.GroupMembers.Add(new GroupMember
            {
                GroupId = groupId,
                UserId = userId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }

    private static bool RowVersionMatches(string? posted, byte[] current)
    {
        try
        {
            return posted is not null && Convert.FromBase64String(posted).SequenceEqual(current);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
