using Microsoft.AspNetCore.Http;
using Moq;
using ShortLinker.Api.Infrastructure;
using ShortLinker.Api.Middleware;

namespace ShortLinker.Tests;

public class TenantResolutionMiddlewareTests
{
    [Fact]
    public async Task Should_Resolve_Tenant_From_Header()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = "tenant1";
        
        var tenantContext = new TenantContext();
        var middleware = new TenantResolutionMiddleware(innerHttpContext => Task.CompletedTask);
        
        await middleware.InvokeAsync(context, tenantContext);
        
        Assert.Equal("tenant1", tenantContext.TenantId);
    }
}
