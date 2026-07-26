using System.Security.Claims;
using DxPlatform.Application.Abstractions;

namespace DxPlatform.Web.Security;

public sealed class HttpCurrentUserContext(IHttpContextAccessor accessor) : ICurrentUserContext
{
    public string? UserId =>
        accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? DisplayName =>
        accessor.HttpContext?.User.FindFirstValue("display_name")
        ?? accessor.HttpContext?.User.Identity?.Name;

    public string CorrelationId =>
        accessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
}

