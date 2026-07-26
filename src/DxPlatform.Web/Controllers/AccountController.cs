using DxPlatform.Application.Abstractions;
using DxPlatform.Infrastructure.Identity;
using DxPlatform.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DxPlatform.Web.Controllers;

public sealed class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAuditService auditService) : Controller
{
    [AllowAnonymous]
    [HttpGet("/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [HttpPost("/login")]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            await auditService.WriteAsync(
                "LoginFailed", "User", null, false,
                "登録されていないアカウントでログインが試行されました。",
                cancellationToken);
            ModelState.AddModelError(string.Empty, "メールアドレスまたはパスワードが正しくありません。");
            return View(model);
        }

        if (!user.IsActive)
        {
            await auditService.WriteAsync(
                "LoginFailed", "User", user.Id, false,
                "無効ユーザーのログインを拒否しました。",
                cancellationToken);
            ModelState.AddModelError(string.Empty, "このアカウントは利用できません。管理者へ連絡してください。");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            HttpContext.User = await signInManager.CreateUserPrincipalAsync(user);
            user.LastLoginAt = DateTimeOffset.UtcNow;
            user.UpdatedBy = user.Id;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            user.RowVersion = Guid.NewGuid().ToByteArray();
            await userManager.UpdateAsync(user);
            await auditService.WriteAsync(
                "LoginSucceeded", "User", user.Id, true,
                "ログインに成功しました。",
                cancellationToken);

            return Url.IsLocalUrl(model.ReturnUrl)
                ? LocalRedirect(model.ReturnUrl!)
                : RedirectToAction("Index", "Dashboard");
        }

        await auditService.WriteAsync(
            "LoginFailed", "User", user.Id, false,
            result.IsLockedOut
                ? "ログイン失敗回数超過によりアカウントをロックしました。"
                : "パスワード認証に失敗しました。",
            cancellationToken);

        ModelState.AddModelError(
            string.Empty,
            result.IsLockedOut
                ? "ログイン失敗回数が上限に達しました。時間を置いて再試行してください。"
                : "メールアドレスまたはパスワードが正しくありません。");
        return View(model);
    }

    [Authorize]
    [ValidateAntiForgeryToken]
    [HttpPost("/logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet("/change-password")]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize]
    [ValidateAntiForgeryToken]
    [HttpPost("/change-password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null || !user.IsActive)
        {
            return Challenge();
        }

        var result = await userManager.ChangePasswordAsync(
            user,
            model.CurrentPassword,
            model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await signInManager.RefreshSignInAsync(user);
        await auditService.WriteAsync(
            "PasswordChanged", "User", user.Id, true,
            "パスワードを変更しました。",
            cancellationToken);
        TempData["StatusMessage"] = "パスワードを変更しました。";
        return RedirectToAction("Index", "Dashboard");
    }

    [AllowAnonymous]
    [HttpGet("/access-denied")]
    public IActionResult AccessDenied() => View();
}
