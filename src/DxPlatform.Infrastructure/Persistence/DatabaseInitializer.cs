using DxPlatform.Application.Security;
using DxPlatform.Domain.Agents;
using DxPlatform.Domain.Applications;
using DxPlatform.Domain.DataAssets;
using DxPlatform.Domain.Enums;
using DxPlatform.Domain.Relations;
using DxPlatform.Infrastructure.Configuration;
using DxPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DxPlatform.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    private static readonly Action<ILogger, Exception?> MissingBootstrapAdmin =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1001, "BootstrapAdminMissing"),
            "BootstrapAdminのEmail/Passwordが未設定のため、初期管理者は作成されません。");

    public static async Task InitializeAsync(
        IServiceProvider services,
        IConfiguration configuration,
        bool seedDemoData,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        if (dbContext.Database.IsSqlite())
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }
        else
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        await SeedRolesAndAdminAsync(scope.ServiceProvider, logger, cancellationToken);

        if (seedDemoData && configuration.GetValue("DemoData:Enabled", true))
        {
            await SeedDemoDataAsync(dbContext, cancellationToken);
        }
    }

    private static async Task SeedRolesAndAdminAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var roleName in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new ApplicationRole
                {
                    Name = roleName,
                    CreatedBy = "bootstrap",
                    UpdatedBy = "bootstrap"
                });
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"初期ロール {roleName} の作成に失敗しました: {string.Join(", ", result.Errors.Select(x => x.Code))}");
                }
            }
        }

        var options = services.GetRequiredService<IOptions<BootstrapAdminOptions>>().Value;
        var hasEmail = !string.IsNullOrWhiteSpace(options.Email);
        var hasPassword = !string.IsNullOrWhiteSpace(options.Password);
        if (!hasEmail && !hasPassword)
        {
            MissingBootstrapAdmin(logger, null);
            return;
        }

        if (!hasEmail || !hasPassword)
        {
            throw new InvalidOperationException(
                "BootstrapAdmin__EmailとBootstrapAdmin__Passwordは両方を設定してください。");
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(options.Email!);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = options.Email,
                Email = options.Email,
                DisplayName = "初期管理者",
                Department = "Platform Administration",
                EmailConfirmed = true,
                IsActive = true,
                CreatedBy = "bootstrap",
                UpdatedBy = "bootstrap"
            };
            var result = await userManager.CreateAsync(user, options.Password!);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"初期管理者の作成に失敗しました: {string.Join(", ", result.Errors.Select(x => x.Code))}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, RoleNames.SystemAdmin))
        {
            await userManager.AddToRoleAsync(user, RoleNames.SystemAdmin);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task SeedDemoDataAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await dbContext.ApplicationAssets.AnyAsync(cancellationToken))
        {
            return;
        }

        var shipping = new ApplicationAsset
        {
            Code = "APP-SHIPPING-BOARD",
            Name = "Shipping Board",
            Description = "架空の出荷状況確認Webアプリです。",
            Url = "https://example.com/shipping-board",
            AssetType = ApplicationAssetType.WebApplication,
            TechnologyStack = "ASP.NET Core MVC",
            OwnerDepartment = "Sample Logistics",
            ManagerName = "Sample Manager",
            DevelopmentLead = "Sample Developer",
            RuntimeEnvironment = "Sample IIS",
            Status = LifecycleStatus.Production,
            Version = "1.2.0",
            HealthCheckUrl = "https://example.com/",
            ConfidentialityClassification = "Internal",
            LastReviewedAt = DateTimeOffset.UtcNow.AddDays(-10),
            LastDeployedAt = DateTimeOffset.UtcNow.AddDays(-3),
            TagsText = "shipping,demo"
        };
        var quality = new ApplicationAsset
        {
            Code = "APP-QUALITY-DASHBOARD",
            Name = "Quality Dashboard",
            Description = "架空の品質指標ダッシュボードです。",
            Url = "https://example.com/quality-dashboard",
            AssetType = ApplicationAssetType.MotionBoard,
            TechnologyStack = "Sample MotionBoard",
            OwnerDepartment = "Sample Quality",
            ManagerName = "Sample Quality Owner",
            RuntimeEnvironment = "Sample Analytics",
            Status = LifecycleStatus.Testing,
            Version = "0.9.0",
            ConfidentialityClassification = "Internal",
            LastReviewedAt = DateTimeOffset.UtcNow.AddDays(-20),
            TagsText = "quality,demo"
        };
        var sampleSql = new DataAsset
        {
            Name = "Sample SQL Server",
            DataType = DataAssetType.SqlServer,
            SystemName = "Sample Data Platform",
            Description = "接続情報を含まない架空のデータ資産です。",
            OwnerDepartment = "Sample Data Team",
            DataOwner = "Sample Data Owner",
            UpdateFrequency = "Daily",
            ConfidentialityClassification = "Internal",
            RetentionPeriod = "1 year",
            Status = LifecycleStatus.Production,
            ConnectionReferenceName = "SampleSqlReference",
            TagsText = "sample,sql",
            LastReviewedAt = DateTimeOffset.UtcNow.AddDays(-15)
        };
        var supportAgent = new AgentAsset
        {
            Name = "Production Support Agent",
            AgentType = AgentType.CopilotStudio,
            Platform = "Sample Copilot Studio",
            Purpose = "架空の手順情報を検索するデモAgentです。",
            OwnerDepartment = "Sample Operations",
            ManagerName = "Sample Agent Owner",
            ModelName = "Approved Model Reference",
            PromptVersion = "1.0",
            PromptSummary = "架空の運用情報を案内する。",
            PromptLocationReference = "Sample approved repository reference",
            Status = LifecycleStatus.Testing,
            PublicationScope = "Pilot users",
            RiskLevel = RiskLevel.Medium,
            EvaluationStatus = EvaluationStatus.InProgress,
            TagsText = "support,demo",
            LastReviewedAt = DateTimeOffset.UtcNow.AddDays(-5)
        };

        dbContext.AddRange(shipping, quality, sampleSql, supportAgent);
        dbContext.ApplicationDataAssets.Add(new ApplicationDataAsset
        {
            ApplicationAsset = quality,
            DataAsset = sampleSql
        });
        dbContext.AgentDataAssets.Add(new AgentDataAsset
        {
            AgentAsset = supportAgent,
            DataAsset = sampleSql
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
