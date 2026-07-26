using DxPlatform.Application.Security;
using DxPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DxPlatform.Web.Controllers;

[Authorize(Policy = PolicyNames.SystemAdminOnly)]
[Route("audit-logs")]
public sealed class AuditLogsController(AppDbContext dbContext) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? actionType,
        string? targetType,
        bool? succeeded,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(actionType))
        {
            query = query.Where(x => x.ActionType.Contains(actionType));
        }

        if (!string.IsNullOrWhiteSpace(targetType))
        {
            query = query.Where(x => x.TargetType.Contains(targetType));
        }

        if (succeeded.HasValue)
        {
            query = query.Where(x => x.Succeeded == succeeded.Value);
        }

        ViewBag.ActionType = actionType;
        ViewBag.TargetType = targetType;
        ViewBag.Succeeded = succeeded;
        return View(await query.OrderByDescending(x => x.OccurredAt)
            .Take(500)
            .ToListAsync(cancellationToken));
    }
}

