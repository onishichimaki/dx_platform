using DxPlatform.Application.Security;
using DxPlatform.Domain.Enums;

namespace DxPlatform.UnitTests;

public sealed class PermissionEvaluatorTests
{
    private readonly ApplicationPermissionEvaluator _sut = new();

    [Fact]
    public void SystemAdmin_HasManageWithoutAssignments()
    {
        var result = _sut.Evaluate(new PermissionEvaluationInput(true, null, []));
        Assert.Equal(PermissionLevel.Manage, result);
    }

    [Fact]
    public void ExplicitUserDeny_WinsOverGroupManage()
    {
        var result = _sut.Evaluate(new PermissionEvaluationInput(
            false,
            PermissionLevel.None,
            [PermissionLevel.Manage]));
        Assert.Equal(PermissionLevel.None, result);
    }

    [Fact]
    public void UserGrant_WinsOverHigherGroupGrant()
    {
        var result = _sut.Evaluate(new PermissionEvaluationInput(
            false,
            PermissionLevel.View,
            [PermissionLevel.Manage]));
        Assert.Equal(PermissionLevel.View, result);
    }

    [Fact]
    public void HighestGroupGrant_IsUsed()
    {
        var result = _sut.Evaluate(new PermissionEvaluationInput(
            false,
            null,
            [PermissionLevel.View, PermissionLevel.Use]));
        Assert.Equal(PermissionLevel.Use, result);
    }

    [Fact]
    public void MissingAssignment_IsDenied()
    {
        var input = new PermissionEvaluationInput(false, null, []);
        Assert.Equal(PermissionLevel.None, _sut.Evaluate(input));
        Assert.False(_sut.CanView(input));
    }

    [Theory]
    [InlineData(PermissionLevel.View, true, false, false)]
    [InlineData(PermissionLevel.Use, true, true, false)]
    [InlineData(PermissionLevel.Manage, true, true, true)]
    public void PermissionLevels_IncludeLowerLevels(
        PermissionLevel level,
        bool canView,
        bool canUse,
        bool canManage)
    {
        var input = new PermissionEvaluationInput(false, level, []);
        Assert.Equal(canView, _sut.CanView(input));
        Assert.Equal(canUse, _sut.CanUse(input));
        Assert.Equal(canManage, _sut.CanManage(input));
    }
}

