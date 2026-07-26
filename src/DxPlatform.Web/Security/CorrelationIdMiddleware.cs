using System.Text.RegularExpressions;

namespace DxPlatform.Web.Security;

public sealed partial class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    private const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = !string.IsNullOrWhiteSpace(incoming) &&
                            incoming.Length <= 100 &&
                            SafeCorrelationId().IsMatch(incoming)
            ? incoming
            : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object?>
               {
                   ["CorrelationId"] = correlationId,
                   ["UserId"] = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                   ["RequestPath"] = context.Request.Path.Value
               }))
        {
            await next(context);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex SafeCorrelationId();
}

