using ArkaCallCenter.Api.Extensions;
using ArkaCallCenter.Core.Abstractions;

namespace ArkaCallCenter.Api.Middleware;

/// <summary>هویت کاربر جاری را از روی JWT در IUsageContext قرار می‌دهد تا مصرف توکن به او نسبت داده شود.</summary>
public class UsageContextMiddleware
{
    private readonly RequestDelegate _next;
    public UsageContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IUsageContext usage)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var id = context.User.GetUserId();
            if (id > 0) usage.UserId = id;
        }
        await _next(context);
    }
}
