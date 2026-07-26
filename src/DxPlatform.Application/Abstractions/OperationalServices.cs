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

public interface IHealthCheckScheduler
{
    Task ScheduleAsync(CancellationToken cancellationToken = default);
}

public interface IApplicationAccessService
{
    Task<PermissionLevel> GetLevelAsync(
        Guid applicationAssetId,
        string userId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetVisibleApplicationIdsAsync(
        string userId,
        bool isSystemAdmin,
        CancellationToken cancellationToken = default);
}

public sealed class ManualOnlyHealthCheckScheduler : IHealthCheckScheduler
{
    public Task ScheduleAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
