using System.Diagnostics;
using DxPlatform.Application.Abstractions;
using DxPlatform.Domain.Enums;
using DxPlatform.Domain.Operations;
using DxPlatform.Infrastructure.Persistence;

namespace DxPlatform.Infrastructure.Operations;

public sealed class AuditService(
    AppDbContext dbContext,
    ICurrentUserContext currentUserContext) : IAuditService
{
    public async Task WriteAsync(
        string actionType,
        string targetType,
        string? targetId,
        bool succeeded,
        string safeSummary,
        CancellationToken cancellationToken = default)
    {
        var summary = safeSummary.Length <= 1000 ? safeSummary : safeSummary[..1000];
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = currentUserContext.UserId,
            ActorDisplayName = currentUserContext.DisplayName,
            ActionType = actionType,
            TargetType = targetType,
            TargetId = targetId,
            Succeeded = succeeded,
            Summary = summary,
            CorrelationId = currentUserContext.CorrelationId
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ApplicationHealthCheckService(HttpClient httpClient)
    : IApplicationHealthCheckService
{
    public async Task<HealthCheckExecutionResult> CheckAsync(
        string url,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);
            stopwatch.Stop();

            return new HealthCheckExecutionResult(
                response.IsSuccessStatusCode ? HealthStatus.Succeeded : HealthStatus.Failed,
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new HealthCheckExecutionResult(
                HealthStatus.TimedOut,
                null,
                stopwatch.ElapsedMilliseconds,
                "設定時間内に応答がありませんでした。");
        }
        catch (HttpRequestException exception)
        {
            stopwatch.Stop();
            return new HealthCheckExecutionResult(
                HealthStatus.Failed,
                exception.StatusCode is null ? null : (int)exception.StatusCode,
                stopwatch.ElapsedMilliseconds,
                "接続またはHTTP通信に失敗しました。");
        }
    }
}

