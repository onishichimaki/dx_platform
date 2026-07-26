using System.ComponentModel.DataAnnotations;
using DxPlatform.Application.Applications;
using DxPlatform.Application.Security;
using DxPlatform.Application.Validation;
using DxPlatform.Domain.Applications;
using DxPlatform.Domain.Enums;

namespace DxPlatform.UnitTests;

public sealed class SecurityAndValidationTests
{
    [Fact]
    public void RoleRules_OnlySystemAdminCanManageUsers()
    {
        Assert.True(RoleRules.CanManageUsers([RoleNames.SystemAdmin]));
        Assert.False(RoleRules.CanManageUsers([RoleNames.CatalogAdmin, RoleNames.AppOwner]));
    }

    [Fact]
    public void CatalogAdminAndSystemAdmin_CanManageCatalog()
    {
        Assert.True(RoleRules.CanManageCatalog([RoleNames.SystemAdmin]));
        Assert.True(RoleRules.CanManageCatalog([RoleNames.CatalogAdmin]));
        Assert.False(RoleRules.CanManageCatalog([RoleNames.Viewer]));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public void DisabledOrLockedUser_CannotSignIn(
        bool active,
        bool locked,
        bool expected) =>
        Assert.Equal(expected, UserActivationRules.CanSignIn(active, locked));

    [Theory]
    [InlineData("https://example.com/app", true)]
    [InlineData("http://example.com/health", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,test", false)]
    [InlineData("https://user:password@example.com/", false)]
    [InlineData("/relative", false)]
    public void UrlValidator_AllowsOnlySafeHttpUrls(string value, bool expected)
    {
        var validator = new UrlSafetyValidator();
        Assert.Equal(expected, validator.IsSafeHttpUrl(value, allowEmpty: false));
    }

    [Fact]
    public void RetiredAsset_CannotReturnToProduction()
    {
        Assert.False(LifecycleRules.CanTransition(
            LifecycleStatus.Retired,
            LifecycleStatus.Production));
    }

    [Fact]
    public void TestingAsset_CanMoveToProduction()
    {
        Assert.True(LifecycleRules.CanTransition(
            LifecycleStatus.Testing,
            LifecycleStatus.Production));
    }

    [Fact]
    public void ApplicationRequiredFields_AreValidated()
    {
        var entity = new ApplicationAsset { Code = "", Name = "" };
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(
            entity,
            new ValidationContext(entity),
            results,
            validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(ApplicationAsset.Code)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(ApplicationAsset.Name)));
    }
}

