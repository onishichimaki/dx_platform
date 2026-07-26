using System.Security.Claims;
using DxPlatform.Application.Abstractions;
using DxPlatform.Application.Applications;
using DxPlatform.Application.Security;
using DxPlatform.Application.Validation;
using DxPlatform.Domain.Agents;
using DxPlatform.Domain.Enums;
using DxPlatform.Domain.Relations;
using DxPlatform.Infrastructure.Persistence;
using DxPlatform.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DxPlatform.Web.Controllers;

[Authorize]
[Route("agents")]
public sealed class AgentsController(
    AppDbContext dbContext,
    IUrlSafetyValidator urlValidator,
    IAuditService auditService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? keyword,
        AgentType? type,
        LifecycleStatus? status,
        RiskLevel? risk,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AgentAssets.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) ||
                                     x.Platform.Contains(keyword) ||
                                     x.Purpose.Contains(keyword) ||
                                     (x.TagsText != null && x.TagsText.Contains(keyword)));
        }

        if (type.HasValue)
        {
            query = query.Where(x => x.AgentType == type);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status);
        }

        if (risk.HasValue)
        {
            query = query.Where(x => x.RiskLevel == risk);
        }

        ViewBag.Keyword = keyword;
        ViewBag.Type = type;
        ViewBag.Status = status;
        ViewBag.Risk = risk;
        return View(await query.OrderBy(x => x.Name).ToListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.AgentAssets.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        ViewBag.DataAssets = await dbContext.AgentDataAssets.AsNoTracking()
            .Where(x => x.AgentAssetId == id && x.DataAsset.IsActive)
            .Select(x => x.DataAsset).OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        ViewBag.Tools = await dbContext.AgentToolAssets.AsNoTracking()
            .Where(x => x.AgentAssetId == id && x.ToolAsset.IsActive)
            .Select(x => x.ToolAsset).OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return View(entity);
    }

    [Authorize(Policy = PolicyNames.CatalogManagement)]
    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        await SetRelationChoicesAsync();
        return View("Edit", new AgentAssetInputModel());
    }

    [Authorize(Policy = PolicyNames.CatalogManagement)]
    [ValidateAntiForgeryToken]
    [HttpPost("create")]
    public async Task<IActionResult> Create(
        AgentAssetInputModel model,
        CancellationToken cancellationToken)
    {
        ValidateUrls(model);
        if (!ModelState.IsValid)
        {
            await SetRelationChoicesAsync();
            return View("Edit", model);
        }

        var entity = new AgentAsset();
        model.ApplyTo(entity);
        entity.CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        entity.UpdatedBy = entity.CreatedBy;
        dbContext.AgentAssets.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SyncRelationsAsync(entity.Id, model, cancellationToken);
        await auditService.WriteAsync(
            "AgentCreated", "AgentAsset", entity.Id.ToString(), true,
            $"Agent「{entity.Name}」を登録しました。",
            cancellationToken);
        TempData["StatusMessage"] = "Agentを登録しました。";
        return RedirectToAction(nameof(Details), new { id = entity.Id });
    }

    [Authorize(Policy = PolicyNames.CatalogManagement)]
    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.AgentAssets.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var model = new AgentAssetInputModel
        {
            Id = entity.Id,
            Name = entity.Name,
            AgentType = entity.AgentType,
            Platform = entity.Platform,
            Purpose = entity.Purpose,
            OwnerDepartment = entity.OwnerDepartment,
            ManagerName = entity.ManagerName,
            ModelName = entity.ModelName,
            GitHubRepositoryUrl = entity.GitHubRepositoryUrl,
            PromptVersion = entity.PromptVersion,
            PromptSummary = entity.PromptSummary,
            PromptLocationReference = entity.PromptLocationReference,
            PromptLastReviewedAt = entity.PromptLastReviewedAt,
            Status = entity.Status,
            PublicationScope = entity.PublicationScope,
            HasWriteOperations = entity.HasWriteOperations,
            SendsDataExternally = entity.SendsDataExternally,
            RiskLevel = entity.RiskLevel,
            EvaluationStatus = entity.EvaluationStatus,
            LastEvaluatedAt = entity.LastEvaluatedAt,
            LastReviewedAt = entity.LastReviewedAt,
            TagsText = entity.TagsText,
            Notes = entity.Notes,
            RelatedDataAssetIds = await dbContext.AgentDataAssets
                .Where(x => x.AgentAssetId == id).Select(x => x.DataAssetId)
                .ToListAsync(cancellationToken),
            RelatedToolAssetIds = await dbContext.AgentToolAssets
                .Where(x => x.AgentAssetId == id).Select(x => x.ToolAssetId)
                .ToListAsync(cancellationToken),
            RowVersion = Convert.ToBase64String(entity.RowVersion)
        };
        await SetRelationChoicesAsync();
        return View(model);
    }

    [Authorize(Policy = PolicyNames.CatalogManagement)]
    [ValidateAntiForgeryToken]
    [HttpPost("{id:guid}/edit")]
    public async Task<IActionResult> Edit(
        Guid id,
        AgentAssetInputModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var entity = await dbContext.AgentAssets.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        ValidateUrls(model);
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
            await SetRelationChoicesAsync();
            return View(model);
        }

        model.ApplyTo(entity);
        entity.UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SyncRelationsAsync(id, model, cancellationToken);
        await auditService.WriteAsync(
            "AgentUpdated", "AgentAsset", entity.Id.ToString(), true,
            $"Agent「{entity.Name}」と関連を変更しました。",
            cancellationToken);
        TempData["StatusMessage"] = "Agentを更新しました。";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = PolicyNames.CatalogManagement)]
    [HttpGet("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.AgentAssets.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? NotFound() : View(entity);
    }

    [Authorize(Policy = PolicyNames.CatalogManagement)]
    [ValidateAntiForgeryToken]
    [HttpPost("{id:guid}/disable")]
    public async Task<IActionResult> DisableConfirmed(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.AgentAssets.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.IsActive = false;
        entity.UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "AgentDisabled", "AgentAsset", entity.Id.ToString(), true,
            $"Agent「{entity.Name}」を無効化しました。",
            cancellationToken);
        TempData["StatusMessage"] = "Agentを無効化しました。";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = PolicyNames.CatalogManagement)]
    [HttpGet("tools/create")]
    public IActionResult CreateTool() => View(new ToolAssetInputModel());

    [Authorize(Policy = PolicyNames.CatalogManagement)]
    [ValidateAntiForgeryToken]
    [HttpPost("tools/create")]
    public async Task<IActionResult> CreateTool(
        ToolAssetInputModel model,
        CancellationToken cancellationToken)
    {
        if (!urlValidator.IsSafeHttpUrl(model.ReferenceUrl))
        {
            ModelState.AddModelError(nameof(model.ReferenceUrl), "HTTPまたはHTTPSの安全なURLを指定してください。");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var tool = new ToolAsset
        {
            Name = model.Name.Trim(),
            ToolType = model.ToolType.Trim(),
            ReferenceUrl = model.ReferenceUrl?.Trim(),
            Description = model.Description?.Trim(),
            CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier),
            UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)
        };
        dbContext.ToolAssets.Add(tool);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "ToolCreated", "ToolAsset", tool.Id.ToString(), true,
            $"利用ツール/API「{tool.Name}」を登録しました。",
            cancellationToken);
        TempData["StatusMessage"] = "利用ツール/APIを登録しました。";
        return RedirectToAction(nameof(Index));
    }

    private void ValidateUrls(AgentAssetInputModel model)
    {
        if (!urlValidator.IsSafeHttpUrl(model.GitHubRepositoryUrl))
        {
            ModelState.AddModelError(
                nameof(model.GitHubRepositoryUrl),
                "GitHubリポジトリURLはHTTPまたはHTTPSの安全な絶対URLを指定してください。");
        }
    }

    private async Task SetRelationChoicesAsync()
    {
        ViewBag.AllDataAssets = await dbContext.DataAssets.AsNoTracking()
            .Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        ViewBag.AllTools = await dbContext.ToolAssets.AsNoTracking()
            .Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
    }

    private async Task SyncRelationsAsync(
        Guid agentId,
        AgentAssetInputModel model,
        CancellationToken cancellationToken)
    {
        var dataExisting = await dbContext.AgentDataAssets
            .Where(x => x.AgentAssetId == agentId).ToListAsync(cancellationToken);
        Sync(
            dbContext.AgentDataAssets,
            dataExisting,
            model.RelatedDataAssetIds,
            x => x.DataAssetId,
            id => new AgentDataAsset { AgentAssetId = agentId, DataAssetId = id });

        var toolExisting = await dbContext.AgentToolAssets
            .Where(x => x.AgentAssetId == agentId).ToListAsync(cancellationToken);
        Sync(
            dbContext.AgentToolAssets,
            toolExisting,
            model.RelatedToolAssetIds,
            x => x.ToolAssetId,
            id => new AgentToolAsset { AgentAssetId = agentId, ToolAssetId = id });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void Sync<TEntity>(
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

