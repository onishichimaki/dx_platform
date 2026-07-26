namespace DxPlatform.Web.Security;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] =
                "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
            headers["Content-Security-Policy"] =
                "default-src 'self'; base-uri 'self'; form-action 'self'; " +
                "frame-ancestors 'none'; object-src 'none'; img-src 'self' data:; " +
                "script-src 'self'; style-src 'self' 'unsafe-inline'";
            return Task.CompletedTask;
        });

        await next(context);
    }
}
