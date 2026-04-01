using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShortLinker.Api.Infrastructure;
using ShortLinker.Api.Models;

namespace ShortLinker.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class StatsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public StatsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<object>>> GetSummary()
    {
        var totalLinks = await _db.ShortLinks.CountAsync();
        var totalClicks = await _db.AccessLogs.CountAsync();
        return ApiResponse<object>.Success(new { totalLinks, totalClicks });
    }

    [HttpGet("clicks")]
    public async Task<ActionResult<ApiResponse<object>>> GetClicksTrend([FromQuery] int days = 7)
    {
        var startDate = DateTime.UtcNow.Date.AddDays(-days);
        
        var logs = await _db.AccessLogs
            .Where(l => l.AccessedAt >= startDate)
            .GroupBy(l => l.AccessedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        // Fill missing days
        var result = Enumerable.Range(0, days)
            .Select(i => startDate.AddDays(i))
            .Select(d => new {
                Date = d.ToString("yyyy-MM-dd"),
                Count = logs.FirstOrDefault(l => l.Date == d)?.Count ?? 0
            })
            .ToList();

        return ApiResponse<object>.Success(result);
    }

    [HttpGet("devices")]
    public async Task<ActionResult<ApiResponse<object>>> GetDeviceStats()
    {
        var osStats = await _db.AccessLogs
            .GroupBy(l => l.OS ?? "Unknown")
            .Select(g => new { Name = g.Key, Value = g.Count() })
            .ToListAsync();

        return ApiResponse<object>.Success(osStats);
    }
}