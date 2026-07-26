using System.Threading.RateLimiting;
using DxPlatform.Application.Abstractions;
using DxPlatform.Application.Security;
using DxPlatform.Infrastructure;
using DxPlatform.Infrastructure.Identity;
using DxPlatform.Infrastructure.Persistence;
using DxPlatform.Web.Logging;
using DxPlatform.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDxPlatformInfrastructure(
    builder.Configuration,
    builder.Environment.IsProduction());
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadyHealthCheck>("database", tags: ["ready"]);
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName(
        builder.Configuration["DataProtection:ApplicationName"] ?? "DxPlatform");
var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
if (!string.IsNullOrWhiteSpace(keyRingPath))
{
    if (!OperatingSystem.IsWindows())
    {
        throw new InvalidOperationException(
            "DataProtection__KeyRingPathのDPAPI保護はWindows環境で構成してください。");
    }

    dataProtection
        .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
        .ProtectKeysWithDpapi();
}
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("deployment-api", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-DxPlatform";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsEnvironment("Testing")
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnValidatePrincipal = async context =>
    {
        var userManager = context.HttpContext.RequestServices
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = context.Principal is null
            ? null
            : await userManager.GetUserAsync(context.Principal);
        if (user is null || !user.IsActive)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(
                IdentityConstants.ApplicationScheme);
        }
    };
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(PolicyNames.SystemAdminOnly,
        policy => policy.RequireRole(RoleNames.SystemAdmin))
    .AddPolicy(PolicyNames.CatalogManagement,
        policy => policy.RequireRole(RoleNames.SystemAdmin, RoleNames.CatalogAdmin))
    .AddPolicy(PolicyNames.ApplicationManagement,
        policy => policy.RequireRole(
            RoleNames.SystemAdmin,
            RoleNames.CatalogAdmin,
            RoleNames.AppOwner));

var fileLogPath = builder.Configuration["Logging:File:Path"];
if (!string.IsNullOrWhiteSpace(fileLogPath))
{
    builder.Logging.AddProvider(new SafeFileLoggerProvider(
        Path.Combine(builder.Environment.ContentRootPath, fileLogPath)));
}

var app = builder.Build();

if (app.Environment.IsProduction())
{
    var allowedHosts = app.Configuration["AllowedHosts"];
    if (string.IsNullOrWhiteSpace(allowedHosts) ||
        allowedHosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(host => host == "*"))
    {
        throw new InvalidOperationException(
            "ProductionではAllowedHostsに実際のポータルHost名を指定してください。'*'は使用できません。");
    }
    if (string.IsNullOrWhiteSpace(keyRingPath) || !Path.IsPathFullyQualified(keyRingPath))
    {
        throw new InvalidOperationException(
            "ProductionではDataProtection__KeyRingPathにACL保護された絶対pathを指定してください。");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = static async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy
                ? """{"status":"Healthy"}"""
                : """{"status":"Unhealthy"}""");
    }
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = static async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy
                ? """{"status":"Healthy"}"""
                : """{"status":"Unhealthy"}""");
    }
}).AllowAnonymous();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

await DatabaseInitializer.InitializeAsync(
    app.Services,
    app.Configuration,
    app.Environment.IsDevelopment());

await app.RunAsync();

public partial class Program;
