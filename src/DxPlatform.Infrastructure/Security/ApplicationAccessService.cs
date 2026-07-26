using DxPlatform.Application.Abstractions;
using DxPlatform.Application.Security;
using DxPlatform.Domain.Enums;
using DxPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DxPlatform.Infrastructure.Security;

public sealed class ApplicationAccessService(
    AppDbContext dbContext,
    IApplicationPermissionEvaluator evaluator) : IApplicationAccessService
{
    public async Task<PermissionLevel> GetLevelAsync(
        Guid applicationAssetId,
        string userId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        if (isSystemAdmin)
        {
            return PermissionLevel.Manage;
        }

        var userPermission = await dbContext.ApplicationPermissions
            .Where(x => x.IsActive && x.ApplicationAssetId == applicationAssetId && x.UserId == userId)
            .Select(x => (PermissionLevel?)x.Level)
            .SingleOrDefaultAsync(cancellationToken);

        var groupIds = dbContext.GroupMembers
            .Where(x => x.IsActive && x.UserId == userId && x.Group.IsActive)
            .Select(x => x.GroupId);

        var groupPermissions = await dbContext.ApplicationPermissions
            .Where(x => x.IsActive && x.ApplicationAssetId == applicationAssetId &&
                        x.GroupId.HasValue && groupIds.Contains(x.GroupId.Value))
            .Select(x => x.Level)
            .ToListAsync(cancellationToken);

        return evaluator.Evaluate(new PermissionEvaluationInput(
            false,
            userPermission,
            groupPermissions));
    }

    public async Task<IReadOnlySet<Guid>> GetVisibleApplicationIdsAsync(
        string userId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default)
    {
        if (isSystemAdmin)
        {
            return (await dbContext.ApplicationAssets
                    .Where(x => x.IsActive)
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        var candidateIds = await dbContext.ApplicationPermissions
            .Where(x => x.IsActive &&
                        (x.UserId == userId ||
                         (x.GroupId.HasValue && dbContext.GroupMembers.Any(member =>
                             member.IsActive && member.UserId == userId &&
                             member.GroupId == x.GroupId && member.Group.IsActive))))
            .Select(x => x.ApplicationAssetId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var visible = new HashSet<Guid>();
        foreach (var id in candidateIds)
        {
            if (await GetLevelAsync(id, userId, false, cancellationToken) >= PermissionLevel.View)
            {
                visible.Add(id);
            }
        }

        return visible;
    }
}

