using DxPlatform.Domain.Enums;

namespace DxPlatform.Application.Security;

public sealed record PermissionEvaluationInput(
    bool IsSystemAdmin,
    PermissionLevel? UserPermission,
    IReadOnlyCollection<PermissionLevel> GroupPermissions);

public interface IApplicationPermissionEvaluator
{
    PermissionLevel Evaluate(PermissionEvaluationInput input);
    bool CanView(PermissionEvaluationInput input);
    bool CanUse(PermissionEvaluationInput input);
    bool CanManage(PermissionEvaluationInput input);
}

public sealed class ApplicationPermissionEvaluator : IApplicationPermissionEvaluator
{
    public PermissionLevel Evaluate(PermissionEvaluationInput input)
    {
        if (input.IsSystemAdmin)
        {
            return PermissionLevel.Manage;
        }

        // 明示的なユーザー拒否が、あらゆる許可より優先される。
        if (input.UserPermission == PermissionLevel.None)
        {
            return PermissionLevel.None;
        }

        if (input.UserPermission is not null)
        {
            return input.UserPermission.Value;
        }

        return input.GroupPermissions.Count == 0
            ? PermissionLevel.None
            : input.GroupPermissions.Max();
    }

    public bool CanView(PermissionEvaluationInput input) => Evaluate(input) >= PermissionLevel.View;
    public bool CanUse(PermissionEvaluationInput input) => Evaluate(input) >= PermissionLevel.Use;
    public bool CanManage(PermissionEvaluationInput input) => Evaluate(input) >= PermissionLevel.Manage;
}

public static class RoleRules
{
    public static bool CanManageUsers(IEnumerable<string> roles) =>
        roles.Contains(RoleNames.SystemAdmin, StringComparer.Ordinal);

    public static bool CanManageCatalog(IEnumerable<string> roles) =>
        roles.Any(role => role is RoleNames.SystemAdmin or RoleNames.CatalogAdmin);

    public static bool IsKnownRole(string role) =>
        RoleNames.All.Contains(role, StringComparer.Ordinal);
}

public static class UserActivationRules
{
    public static bool CanSignIn(bool isActive, bool isLockedOut) => isActive && !isLockedOut;
}

