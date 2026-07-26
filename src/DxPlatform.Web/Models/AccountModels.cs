using System.ComponentModel.DataAnnotations;

namespace DxPlatform.Web.Models;

public sealed class LoginViewModel
{
    [Required, EmailAddress, Display(Name = "メールアドレス")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "パスワード")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "ログイン状態を保持する")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password), Display(Name = "現在のパスワード")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(12), Display(Name = "新しいパスワード")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword)), Display(Name = "新しいパスワード（確認）")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

