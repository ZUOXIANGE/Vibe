namespace ShortLinker.Api.Middleware;

using ShortLinker.Api.Infrastructure;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantId))
        {
            tenantContext.SetTenantId(tenantId.ToString());
        }
        else
        {
            var host = context.Request.Host.Host;
            var parts = host.Split('.');
            if (parts.Length >= 3)
            {
                tenantContext.SetTenantId(parts[0]);
            }
        }
        await _next(context);
    }
}
