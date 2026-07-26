using DxPlatform.Application.Abstractions;
using DxPlatform.Application.Security;
using DxPlatform.Infrastructure;
using DxPlatform.Infrastructure.Identity;
using DxPlatform.Infrastructure.Persistence;
using DxPlatform.Web.Logging;
using DxPlatform.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
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

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-DxPlatform";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
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

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthentication();
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
