using System.Net.Sockets;
using DxPlatform.Application.Abstractions;
using DxPlatform.Application.Security;
using DxPlatform.Application.Validation;
using DxPlatform.Infrastructure.Configuration;
using DxPlatform.Infrastructure.Identity;
using DxPlatform.Infrastructure.Operations;
using DxPlatform.Infrastructure.Persistence;
using DxPlatform.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DxPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDxPlatformInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isProduction)
    {
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var runtimeConfiguration = serviceProvider.GetRequiredService<IConfiguration>();
            var provider =
                runtimeConfiguration[$"{DatabaseOptions.SectionName}:Provider"] ?? "Sqlite";
            var connectionString =
                runtimeConfiguration.GetConnectionString("DefaultConnection");

            if (isProduction && provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Production環境ではSQLiteを使用できません。Database__Provider=SqlServerを指定してください。");
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                if (isProduction)
                {
                    throw new InvalidOperationException(
                        "Production用SQL Server接続文字列 ConnectionStrings__DefaultConnection が必要です。");
                }

                connectionString = "Data Source=dxplatform.dev.db";
            }

            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString, sql =>
                    sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            }
            else if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                throw new InvalidOperationException(
                    $"未対応のDB Providerです: {provider}. SqliteまたはSqlServerを指定してください。");
            }
        });

        services.Configure<BootstrapAdminOptions>(
            configuration.GetSection(BootstrapAdminOptions.SectionName));
        services.Configure<DeploymentApiOptions>(
            configuration.GetSection(DeploymentApiOptions.SectionName));
        services.Configure<HealthCheckOptions>(
            configuration.GetSection(HealthCheckOptions.SectionName));

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<
            IUserClaimsPrincipalFactory<ApplicationUser>,
            ApplicationClaimsPrincipalFactory>();

        services.AddScoped<IApplicationPermissionEvaluator, ApplicationPermissionEvaluator>();
        services.AddSingleton<IUrlSafetyValidator, UrlSafetyValidator>();
        services.AddSingleton<IDnsResolver, SystemDnsResolver>();
        services.AddSingleton<IHealthCheckTargetValidator, HealthCheckTargetValidator>();
        services.AddScoped<IApplicationAccessService, ApplicationAccessService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddHttpClient<IApplicationHealthCheckService, ApplicationHealthCheckService>()
            .ConfigureHttpClient(client => client.DefaultRequestHeaders.UserAgent.ParseAdd("DxPlatform-HealthCheck/1.0"))
            .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
            {
                var targetValidator = serviceProvider.GetRequiredService<IHealthCheckTargetValidator>();
                return new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    UseCookies = false,
                    ConnectCallback = async (context, cancellationToken) =>
                    {
                        var addresses = await targetValidator.GetAllowedAddressesAsync(
                            context.DnsEndPoint.Host,
                            context.DnsEndPoint.Port,
                            cancellationToken);
                        Exception? lastException = null;
                        foreach (var address in addresses)
                        {
                            var socket = new Socket(
                                address.AddressFamily,
                                SocketType.Stream,
                                ProtocolType.Tcp);
                            try
                            {
                                await socket.ConnectAsync(
                                    new System.Net.IPEndPoint(address, context.DnsEndPoint.Port),
                                    cancellationToken);
                                return new NetworkStream(socket, ownsSocket: true);
                            }
                            catch (Exception exception) when (exception is SocketException or IOException)
                            {
                                lastException = exception;
                                socket.Dispose();
                            }
                        }

                        throw new HttpRequestException(
                            "Health check target is not allowed or could not be reached.",
                            lastException);
                    }
                };
            });
        services.AddSingleton<IHealthCheckScheduler, ManualOnlyHealthCheckScheduler>();

        return services;
    }
}
