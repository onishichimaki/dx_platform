using System.ComponentModel.DataAnnotations;
using DxPlatform.Domain.Enums;

namespace DxPlatform.Web.Models;

public sealed record UserListItemViewModel(
    string Id,
    string DisplayName,
    string Email,
    string? Department,
    bool IsActive,
    IReadOnlyList<string> Roles,
    DateTimeOffset? LastLoginAt);

public sealed class UserEditViewModel
{
    public string? Id { get; set; }

    [Required, StringLength(160), Display(Name = "表示名")]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256), Display(Name = "メールアドレス")]
    public string Email { get; set; } = string.Empty;

    [StringLength(160), Display(Name = "所属")]
    public string? Department { get; set; }

    [Display(Name = "有効")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "ロール")]
    public List<string> SelectedRoles { get; set; } = [];

    [Display(Name = "所属グループ")]
    public List<Guid> SelectedGroupIds { get; set; } = [];

    [DataType(DataType.Password), MinLength(12), Display(Name = "初期パスワード")]
    public string? InitialPassword { get; set; }

    public string? RowVersion { get; set; }
}

public sealed class GroupEditViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(64), RegularExpression("^[A-Za-z0-9_-]+$"), Display(Name = "グループコード")]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(160), Display(Name = "グループ名")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000), Display(Name = "説明")]
    public string? Description { get; set; }

    [Display(Name = "有効")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "メンバー")]
    public List<string> SelectedUserIds { get; set; } = [];

    public string? RowVersion { get; set; }
}

public sealed class PermissionEditViewModel
{
    [Required, Display(Name = "対象アプリ")]
    public Guid ApplicationAssetId { get; set; }

    [Required, RegularExpression("^(User|Group)$"), Display(Name = "対象種別")]
    public string PrincipalType { get; set; } = "User";

    [Required, Display(Name = "ユーザーまたはグループ")]
    public string PrincipalId { get; set; } = string.Empty;

    [Display(Name = "権限レベル")]
    public PermissionLevel Level { get; set; }
}

