using System.Security.Cryptography;
using System.Text;

namespace SrSimplifyRoutine.Web.Services;

public sealed class RequestThrottleMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AbuseGuard guard, ILogger<RequestThrottleMiddleware> logger)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var minute = TimeSpan.FromMinutes(1);
        var allowed = guard.Allow("http:global", 6000, minute) && guard.Allow($"http:ip:{ip}", 400, minute);
        if (allowed && HttpMethods.IsPost(context.Request.Method) && context.Request.Path.StartsWithSegments("/Account"))
        {
            var route = context.Request.Path.Value!.ToLowerInvariant();
            allowed = guard.Allow("auth:global", 300, minute) && guard.Allow($"auth:ip:{ip}", 20, minute);
            if (route == "/account/register")
                allowed = allowed && guard.Allow("register:global", 100, TimeSpan.FromHours(1))
                    && guard.Allow($"register:ip:{ip}", 10, TimeSpan.FromHours(1));
            if (allowed && context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(context.RequestAborted);
                var email = form["Input.Email"].ToString().Trim().ToUpperInvariant();
                if (email.Length > 320) { context.Response.StatusCode = 400; return; }
                if (email.Length > 0)
                {
                    var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email)));
                    allowed = guard.Allow($"auth:email:{route}:{digest}", 8, TimeSpan.FromMinutes(15));
                }
            }
        }
        if (!allowed)
        {
            logger.LogWarning("Request throttled on {Path}", context.Request.Path.Value);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = "60";
            await context.Response.WriteAsync("Too many requests. Please wait before trying again.");
            return;
        }
        await next(context);
    }
}
