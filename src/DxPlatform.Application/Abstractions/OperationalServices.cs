using System.Net;
using DxPlatform.Domain.Enums;

namespace DxPlatform.Application.Abstractions;

public interface ICurrentUserContext
{
    string? UserId { get; }
    string? DisplayName { get; }
    string CorrelationId { get; }
}

public interface IAuditService
{
    Task WriteAsync(
        string actionType,
        string targetType,
        string? targetId,
        bool succeeded,
        string safeSummary,
        CancellationToken cancellationToken = default);
}

public sealed record HealthCheckExecutionResult(
    HealthStatus Status,
    int? HttpStatusCode,
    long ResponseTimeMilliseconds,
    string? ErrorSummary);

public interface IApplicationHealthCheckService
{
    Task<HealthCheckExecutionResult> CheckAsync(
        string url,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

public interface IHealthCheckTargetValidator
{
    Task<bool> IsAllowedAsync(
        string? url,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IPAddress>> GetAllowedAddressesAsync(
        string host,
        int port,
        CancellationToken cancellationToken = default);
}

public interface IDnsResolver
{
    Task<IPAddress[]> GetHostAddressesAsync(
        string host,
        CancellationToken cancellationToken = default);
}

public interface IHealthCheckScheduler
{
    Task ScheduleAsync(CancellationToken cancellationToken = default);
}

public interface IApplicationAccessService
{
    Task<PermissionLevel> GetLevelAsync(
        Guid applicationAssetId,
        string userId,
        bool hasGlobalAccess,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetVisibleApplicationIdsAsync(
        string userId,
        bool hasGlobalAccess,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetManageableApplicationIdsAsync(
        string userId,
        bool hasGlobalAccess,
        CancellationToken cancellationToken = default);
}

public sealed class ManualOnlyHealthCheckScheduler : IHealthCheckScheduler
{
    public Task ScheduleAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
