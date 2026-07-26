namespace DxPlatform.Application.Security;

public static class RoleNames
{
    public const string SystemAdmin = "SystemAdmin";
    public const string CatalogAdmin = "CatalogAdmin";
    public const string AppOwner = "AppOwner";
    public const string Viewer = "Viewer";

    public static readonly string[] All = [SystemAdmin, CatalogAdmin, AppOwner, Viewer];
}

public static class PolicyNames
{
    public const string SystemAdminOnly = "SystemAdminOnly";
    public const string CatalogManagement = "CatalogManagement";
    public const string ApplicationManagement = "ApplicationManagement";
}

