using System.Security.Cryptography;
using System.Text;
using DxPlatform.Application.Abstractions;
using DxPlatform.Domain.Enums;
using DxPlatform.Domain.Operations;
using DxPlatform.Infrastructure.Configuration;
using DxPlatform.Infrastructure.Persistence;
using DxPlatform.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DxPlatform.Web.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/deployments")]
public sealed class DeploymentApiController(
    AppDbContext dbContext,
    IOptions<DeploymentApiOptions> options,
    IAuditService auditService) : ControllerBase
{
    [HttpPost("")]
    public async Task<IActionResult> Register(
        DeploymentRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var expected = options.Value.Token;
        if (string.IsNullOrWhiteSpace(expected))
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Deployment APIは構成されていません。");
        }

        var authorization = Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !TokenMatches(expected, authorization[prefix.Length..]))
        {
            return Unauthorized();
        }

        if (!await dbContext.ApplicationAssets.AnyAsync(
                x => x.Id == request.ApplicationAssetId && x.IsActive,
                cancellationToken))
        {
            ModelState.AddModelError(
                nameof(request.ApplicationAssetId),
                "対象アプリが存在しないか無効です。");
            return ValidationProblem(ModelState);
        }

        if (!Enum.TryParse<DeploymentResult>(request.Result, out var result))
        {
            ModelState.AddModelError(nameof(request.Result), "Deployment結果が不正です。");
            return ValidationProblem(ModelState);
        }

        var deployment = new Deployment
        {
            ApplicationAssetId = request.ApplicationAssetId,
            Version = request.Version.Trim(),
            GitCommitSha = request.GitCommitSha.Trim().ToLowerInvariant(),
            Branch = request.Branch?.Trim(),
            ExecutedBy = request.ExecutedBy.Trim(),
            StartedAt = request.StartedAt == default ? DateTimeOffset.UtcNow : request.StartedAt,
            FinishedAt = request.FinishedAt,
            Result = result,
            Environment = request.Environment.Trim(),
            RollbackFromDeploymentId = request.RollbackFromDeploymentId,
            LogReference = request.LogReference?.Trim(),
            Notes = request.Notes?.Trim(),
            CreatedBy = "deployment-api",
            UpdatedBy = "deployment-api"
        };
        dbContext.Deployments.Add(deployment);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            "DeploymentRegistered",
            "Deployment",
            deployment.Id.ToString(),
            true,
            $"Deployment履歴を登録しました。結果: {deployment.Result}",
            cancellationToken);

        return CreatedAtAction(
            nameof(DeploymentsController.Index),
            "Deployments",
            new { applicationId = deployment.ApplicationAssetId },
            new { deployment.Id });
    }

    private static bool TokenMatches(string expected, string supplied)
    {
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }
}

