using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShortLinker.Api.Infrastructure;
using ZiggyCreatures.Caching.Fusion;

namespace ShortLinker.Api.Controllers;

[ApiController]
[Route("")]
public class RedirectController : ControllerBase
{
    private readonly IFusionCache _cache;
    private readonly ApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;

    public RedirectController(IFusionCache cache, ApplicationDbContext db, ITenantContext tenantContext)
    {
        _cache = cache;
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet("{shortCode}")]
    public async Task<IActionResult> RedirectToOriginal(string shortCode)
    {
        var tenantId = _tenantContext.TenantId;
        if (string.IsNullOrEmpty(tenantId)) return NotFound();

        var cacheKey = $"link:{tenantId}:{shortCode}";
        var originalUrl = await _cache.GetOrSetAsync(cacheKey, async (ctx) => 
        {
            var link = await _db.ShortLinks.FirstOrDefaultAsync(l => l.ShortCode == shortCode);
            return link?.IsActive == true ? link.OriginalUrl : null;
        });

        if (string.IsNullOrEmpty(originalUrl)) return NotFound();

        // 异步记录日志的占位
        _ = Task.Run(() => Console.WriteLine($"Clicked {shortCode} at {DateTime.UtcNow}"));

        return RedirectPermanent(originalUrl); // 301 重定向
    }
}
