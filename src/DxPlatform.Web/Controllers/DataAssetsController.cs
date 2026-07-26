using System.Security.Claims;
using DxPlatform.Application.Abstractions;
using DxPlatform.Application.Applications;
using DxPlatform.Application.Security;
using DxPlatform.Domain.DataAssets;
using DxPlatform.Domain.Enums;
using DxPlatform.Domain.Relations;
using DxPlatform.Infrastructure.Persistence;
using DxPlatform.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DxPlatform.Web.Controllers;

[Authorize]
[Route("data-assets")]
public sealed class DataAssetsController(
    AppDbContext dbContext,
    IAuditService auditService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? keyword,
        DataAssetType? type,
        LifecycleStatus? status,
        CancellationToken cancellationToken)
    {
        var query = dbContext.DataAssets.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) ||
                                     x.SystemName.Contains(keyword) ||
                                     (x.Description != null && x.Description.Contains(keyword)) ||
                                     (x.TagsText != null && x.TagsText.Contains(keyword)));
        }

        if (type.HasValue)
        {
            query = query.Where(x => x.DataType == type);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status);
        }

        ViewBag.Keyword = keyword;
        ViewBag.Type = type;
        ViewBag.Status = status;
        return View(await query.OrderBy(x => x.Name).ToListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.DataAssets.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        ViewBag.Applications = await dbContext.ApplicationDataAssets.AsNoTracking()
            .Where(x => x.DataAssetId == id && x.ApplicationAsset.IsActive)
            .Select(x => x.ApplicationAsset)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        ViewBag.Agents = await dbContext.AgentDataAssets.AsNoTracking()
            .Where(x => x.DataAssetId == id && x.AgentAsset.IsActive)
            .Select(x => x.AgentAsset)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        ViewBag.RelatedDataAssets = await dbContext.DataAssetRelations.AsNoTracking()
            .Where(x => (x.SourceDataAssetId == id || x.TargetDataAssetId == id) &&
                        x.IsActive)
            .Select(x => x.SourceDataAssetId == id ? x.TargetDataAsset : x.SourceDataAsset)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return View(entity);
    }

    [Authorize(Policy = PolicyNames.CatalogManagement)]
    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        await SetRelationChoicesAsync(null);
        return View("Edit", new DataAssetInputModel());
    }

    [Authorize(Policy = PolicyNames.CatalogManagement)]
    [ValidateAntiForgeryToken]
    [HttpPost("create")]
    public async Task<IActionResult> Create(
        DataAssetInputModel model,
        CancellationToken cancellationToken)
    {
        ValidateConnectionReference(model);
        if (!ModelState.IsValid)
        {
            await SetRelationChoicesAsync(null);
            return View("Edit", model);
        }

        var entity = new DataAsset();
        model.ApplyTo(entity);
        entity.CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        entity.UpdatedBy = entity.CreatedBy;
        dbContext.DataAssets.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SyncRelationsAsync(entity.Id, model, cancellationToken);
        await auditService.WriteAsync(
            "DataAssetCreated", "DataAsset", entity.Id.ToString(), true,
            $"データ資産「{entity.Name}」を登録しました。",
            cancellationToken);
        TempData["StatusMessage"] = "データ資産を登録しました。";
        return RedirectToAction(nameof(Details), new { id = entity.Id });
    }

    [Authorize(Policy = PolicyNames.CatalogManagement)]
    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.DataAssets.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var model = new DataAssetInputModel
        {
            Id = entity.Id,
            Name = entity.Name,
            DataType = entity.DataType,
            SystemName = entity.SystemName,
            Description = entity.Description,
            OwnerDepartment = entity.OwnerDepartment,
            DataOwner = entity.DataOwner,
            UpdateFrequency = entity.UpdateFrequency,
            ConfidentialityClassification = entity.ConfidentialityClassification,
            RetentionPeriod = entity.RetentionPeriod,
            Status = entity.Status,
            ConnectionReferenceName = entity.ConnectionReferenceName,
            TagsText = entity.TagsText,
            LastReviewedAt = entity.LastReviewedAt,
            Notes = entity.Notes,
            RelatedApplicationIds = await dbContext.ApplicationDataAssets
                .Where(x => x.DataAssetId == id).Select(x => x.ApplicationAssetId)
                .ToListAsync(cancellationToken),
            RelatedAgentIds = await dbContext.AgentDataAssets
                .Where(x => x.DataAssetId == id).Select(x => x.AgentAssetId)
                .ToListAsync(cancellationToken),
            RelatedDataAssetIds = await dbContext.DataAssetRelations
                .Where(x => x.SourceDataAssetId == id && x.IsActive)
                .Select(x => x.TargetDataAssetId).ToListAsync(cancellationToken),
            RowVersion = Convert.ToBase64String(entity.RowVersion)
        };
        await SetRelationChoicesAsync(id);
        return View(model);
    }

    [Authorize(Policy = PolicyNames.CatalogManagement)]
    [ValidateAntiForgeryToken]
    [HttpPost("{id:guid}/edit")]
    public async Task<IActionResult> Edit(
        Guid id,
        DataAssetInputModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var entity = await dbContext.DataAssets.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        ValidateConnectionReference(model);
        if (!LifecycleRules.CanTransition(entity.Status, model.Status))
        {
            ModelState.AddModelError(nameof(model.Status), "許可されていないステータス遷移です。");
        }

        if (!RowVersionMatches(model.RowVersion, entity.RowVersion))
        {
            ModelState.AddModelError(string.Empty, "他の利用者が更新しました。画面を再読み込みしてください。");
        }

        if (!ModelState.IsValid)
        {
            model.RowVersion = Convert.ToBase64String(entity.RowVersion);
            await SetRelationChoicesAsync(id);
            return View(model);
        }

        model.ApplyTo(entity);
        entity.UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SyncRelationsAsync(id, model, cancellationToken);
        await auditService.WriteAsync(
            "DataAssetUpdated", "DataAsset", entity.Id.ToString(), true,
            $"データ資産「{entity.Name}」と関連を変更しました。",
            cancellationToken);
        TempData["StatusMessage"] = "データ資産を更新しました。";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = PolicyNames.CatalogManagement)]
    [HttpGet("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.DataAssets.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? NotFound() : View(entity);
    }

    [Authorize(Policy = PolicyNames.CatalogManagement)]
    [ValidateAntiForgeryToken]
    [HttpPost("{id:guid}/disable")]
    public async Task<IActionResult> DisableConfirmed(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.DataAssets.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.IsActive = false;
        entity.UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "DataAssetDisabled", "DataAsset", entity.Id.ToString(), true,
            $"データ資産「{entity.Name}」を無効化しました。",
            cancellationToken);
        TempData["StatusMessage"] = "データ資産を無効化しました。";
        return RedirectToAction(nameof(Index));
    }

    private void ValidateConnectionReference(DataAssetInputModel model)
    {
        var value = model.ConnectionReferenceName;
        if (!string.IsNullOrWhiteSpace(value) &&
            (value.Contains(';') || value.Contains('=') ||
             value.Contains("password", StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(
                nameof(model.ConnectionReferenceName),
                "接続文字列やパスワードではなく、安全な参照名だけを入力してください。");
        }
    }

    private async Task SetRelationChoicesAsync(Guid? currentId)
    {
        ViewBag.AllApplications = await dbContext.ApplicationAssets.AsNoTracking()
            .Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        ViewBag.AllAgents = await dbContext.AgentAssets.AsNoTracking()
            .Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        ViewBag.AllDataAssets = await dbContext.DataAssets.AsNoTracking()
            .Where(x => x.IsActive && x.Id != currentId).OrderBy(x => x.Name).ToListAsync();
    }

    private async Task SyncRelationsAsync(
        Guid dataAssetId,
        DataAssetInputModel model,
        CancellationToken cancellationToken)
    {
        await SyncSetAsync(
            dbContext.ApplicationDataAssets,
            await dbContext.ApplicationDataAssets.Where(x => x.DataAssetId == dataAssetId).ToListAsync(cancellationToken),
            model.RelatedApplicationIds,
            x => x.ApplicationAssetId,
            id => new ApplicationDataAsset { DataAssetId = dataAssetId, ApplicationAssetId = id });
        await SyncSetAsync(
            dbContext.AgentDataAssets,
            await dbContext.AgentDataAssets.Where(x => x.DataAssetId == dataAssetId).ToListAsync(cancellationToken),
            model.RelatedAgentIds,
            x => x.AgentAssetId,
            id => new AgentDataAsset { DataAssetId = dataAssetId, AgentAssetId = id });
        await SyncSetAsync(
            dbContext.DataAssetRelations,
            await dbContext.DataAssetRelations.Where(x => x.SourceDataAssetId == dataAssetId).ToListAsync(cancellationToken),
            model.RelatedDataAssetIds.Where(x => x != dataAssetId),
            x => x.TargetDataAssetId,
            id => new DataAssetRelation
            {
                SourceDataAssetId = dataAssetId,
                TargetDataAssetId = id,
                RelationType = "Related"
            });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Task SyncSetAsync<TEntity>(
        DbSet<TEntity> set,
        IReadOnlyCollection<TEntity> existing,
        IEnumerable<Guid> selectedIds,
        Func<TEntity, Guid> idSelector,
        Func<Guid, TEntity> factory)
        where TEntity : class
    {
        var selected = selectedIds.Distinct().ToHashSet();
        foreach (var item in existing)
        {
            if (!selected.Remove(idSelector(item)))
            {
                set.Remove(item);
            }
        }

        foreach (var id in selected)
        {
            set.Add(factory(id));
        }

        return Task.CompletedTask;
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

