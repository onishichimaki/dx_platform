using System.Security.Claims;
using DxPlatform.Application.Abstractions;
using DxPlatform.Application.Security;
using DxPlatform.Domain.Groups;
using DxPlatform.Infrastructure.Persistence;
using DxPlatform.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DxPlatform.Web.Controllers;

[Authorize(Policy = PolicyNames.SystemAdminOnly)]
[Route("admin/groups")]
public sealed class AdminGroupsController(
    AppDbContext dbContext,
    IAuditService auditService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? keyword, CancellationToken cancellationToken)
    {
        var query = dbContext.Groups.AsNoTracking().Include(x => x.Members).AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Code.Contains(keyword) ||
                                     x.Name.Contains(keyword) ||
                                     (x.Description != null && x.Description.Contains(keyword)));
        }

        ViewBag.Keyword = keyword;
        return View(await query.OrderBy(x => x.Name).ToListAsync(cancellationToken));
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        await SetUsersAsync();
        return View("Edit", new GroupEditViewModel());
    }

    [ValidateAntiForgeryToken]
    [HttpPost("create")]
    public async Task<IActionResult> Create(
        GroupEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Groups.AnyAsync(x => x.Code == model.Code, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Code), "同じグループコードが登録されています。");
        }

        if (!ModelState.IsValid)
        {
            await SetUsersAsync();
            return View("Edit", model);
        }

        var group = new Group
        {
            Code = model.Code.Trim(),
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            IsActive = model.IsActive,
            CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier),
            UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)
        };
        foreach (var userId in model.SelectedUserIds.Distinct())
        {
            group.Members.Add(new GroupMember { UserId = userId });
        }

        dbContext.Groups.Add(group);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "GroupCreated", "Group", group.Id.ToString(), true,
            $"グループ「{group.Name}」を登録しました。",
            cancellationToken);
        TempData["StatusMessage"] = "グループを登録しました。";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var group = await dbContext.Groups.Include(x => x.Members)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        await SetUsersAsync();
        return View(new GroupEditViewModel
        {
            Id = group.Id,
            Code = group.Code,
            Name = group.Name,
            Description = group.Description,
            IsActive = group.IsActive,
            SelectedUserIds = group.Members.Where(x => x.IsActive).Select(x => x.UserId).ToList(),
            RowVersion = Convert.ToBase64String(group.RowVersion)
        });
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{id:guid}/edit")]
    public async Task<IActionResult> Edit(
        Guid id,
        GroupEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var group = await dbContext.Groups.Include(x => x.Members)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        if (!RowVersionMatches(model.RowVersion, group.RowVersion))
        {
            ModelState.AddModelError(string.Empty, "他の利用者が更新しました。画面を再読み込みしてください。");
        }

        if (await dbContext.Groups.AnyAsync(
                x => x.Code == model.Code && x.Id != id, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Code), "同じグループコードが登録されています。");
        }

        if (!ModelState.IsValid)
        {
            model.RowVersion = Convert.ToBase64String(group.RowVersion);
            await SetUsersAsync();
            return View(model);
        }

        group.Code = model.Code.Trim();
        group.Name = model.Name.Trim();
        group.Description = model.Description?.Trim();
        group.IsActive = model.IsActive;
        group.UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var selected = model.SelectedUserIds.Distinct().ToHashSet();
        foreach (var member in group.Members)
        {
            member.IsActive = selected.Remove(member.UserId);
        }

        foreach (var userId in selected)
        {
            group.Members.Add(new GroupMember { UserId = userId });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "GroupUpdated", "Group", group.Id.ToString(), true,
            $"グループ「{group.Name}」とメンバーを変更しました。",
            cancellationToken);
        TempData["StatusMessage"] = "グループを更新しました。";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken)
    {
        var group = await dbContext.Groups.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return group is null ? NotFound() : View(group);
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{id:guid}/disable")]
    public async Task<IActionResult> DisableConfirmed(Guid id, CancellationToken cancellationToken)
    {
        var group = await dbContext.Groups.FindAsync([id], cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        group.IsActive = false;
        group.UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "GroupDisabled", "Group", group.Id.ToString(), true,
            $"グループ「{group.Name}」を無効化しました。",
            cancellationToken);
        TempData["StatusMessage"] = "グループを無効化しました。";
        return RedirectToAction(nameof(Index));
    }

    private async Task SetUsersAsync() =>
        ViewBag.AllUsers = await dbContext.Users.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .ToListAsync();

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

