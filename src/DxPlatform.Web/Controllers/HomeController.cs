using System.Diagnostics;
using DxPlatform.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DxPlatform.Web.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    public IActionResult Index() => RedirectToAction("Index", "Dashboard");

    [AllowAnonymous]
    [Route("/error")]
    public IActionResult Error() => View(new ErrorViewModel
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
    });
}

