# Analytics Module Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现基于后台异步写入机制的数据埋点系统，并提供多维度（总览、趋势、设备）的数据分析接口，最终在前端使用 Recharts 渲染数据面板。

**Architecture:** 后端利用 .NET `Channel<T>` 和 `BackgroundService` 实现非阻塞的数据落库。前端增加仪表盘页面，使用 React Query 和 Recharts 展示数据。

**Tech Stack:** ASP.NET Core 10, Entity Framework Core, System.Threading.Channels, React 18, Recharts.

---

### Task 1: 访问日志实体与基础数据层配置

**Files:**
- Create: `src/ShortLinker.Api/Models/LinkAccessLog.cs`
- Modify: `src/ShortLinker.Api/Infrastructure/ApplicationDbContext.cs`

- [ ] **Step 1: 创建 LinkAccessLog 实体模型**
```csharp
// src/ShortLinker.Api/Models/LinkAccessLog.cs
namespace ShortLinker.Api.Models;

public class LinkAccessLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public DateTime AccessedAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Referer { get; set; }
    public string? Browser { get; set; }
    public string? OS { get; set; }
}
```

- [ ] **Step 2: 配置 DbContext**
```csharp
// Modify: src/ShortLinker.Api/Infrastructure/ApplicationDbContext.cs
// Add DbSet
public DbSet<LinkAccessLog> AccessLogs => Set<LinkAccessLog>();

// Add to OnModelCreating
modelBuilder.Entity<LinkAccessLog>().HasQueryFilter(s => EF.Property<string>(s, "TenantId") == _tenantContext.TenantId);
modelBuilder.Entity<LinkAccessLog>().HasIndex(l => l.TenantId);
modelBuilder.Entity<LinkAccessLog>().HasIndex(l => l.AccessedAt);
```

- [ ] **Step 3: 生成并验证迁移**
```bash
cd src/ShortLinker.Api
dotnet ef migrations add AddAccessLogEntity
dotnet build
cd ../..
```

- [ ] **Step 4: Commit**
```bash
git add .
git commit -m "feat(analytics): add LinkAccessLog entity and database migration"
```

### Task 2: 异步日志记录服务与重定向埋点

**Files:**
- Create: `src/ShortLinker.Api/Services/AccessLogChannel.cs`
- Create: `src/ShortLinker.Api/Services/AccessLogWriterService.cs`
- Modify: `src/ShortLinker.Api/Controllers/RedirectController.cs`
- Modify: `src/ShortLinker.Api/Program.cs`

- [ ] **Step 1: 实现内存 Channel 缓冲**
```csharp
// src/ShortLinker.Api/Services/AccessLogChannel.cs
using System.Threading.Channels;
using ShortLinker.Api.Models;

namespace ShortLinker.Api.Services;

public class AccessLogChannel
{
    private readonly Channel<LinkAccessLog> _channel;

    public AccessLogChannel()
    {
        _channel = Channel.CreateUnbounded<LinkAccessLog>();
    }

    public async ValueTask AddLogAsync(LinkAccessLog log, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(log, ct);
    }

    public IAsyncEnumerable<LinkAccessLog> ReadAllAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}
```

- [ ] **Step 2: 实现后台消费服务**
```csharp
// src/ShortLinker.Api/Services/AccessLogWriterService.cs
using ShortLinker.Api.Infrastructure;
using ShortLinker.Api.Models;

namespace ShortLinker.Api.Services;

public class AccessLogWriterService : BackgroundService
{
    private readonly AccessLogChannel _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AccessLogWriterService> _logger;

    public AccessLogWriterService(AccessLogChannel channel, IServiceProvider serviceProvider, ILogger<AccessLogWriterService> logger)
    {
        _channel = channel;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var log in _channel.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                // Parse simple OS/Browser logic
                var ua = log.UserAgent?.ToLower() ?? "";
                log.Browser = ua.Contains("chrome") ? "Chrome" : ua.Contains("safari") ? "Safari" : ua.Contains("firefox") ? "Firefox" : "Other";
                log.OS = ua.Contains("windows") ? "Windows" : ua.Contains("mac") ? "MacOS" : ua.Contains("linux") ? "Linux" : ua.Contains("android") ? "Android" : ua.Contains("iphone") ? "iOS" : "Other";

                // bypass query filter because this is system level insert
                db.AccessLogs.Add(log);
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save access log for {ShortCode}", log.ShortCode);
            }
        }
    }
}
```

- [ ] **Step 3: 修改 RedirectController 并注册服务**
```csharp
// Modify: src/ShortLinker.Api/Controllers/RedirectController.cs
// Inject AccessLogChannel into constructor and update RedirectToOriginal method:
    [HttpGet("{shortCode}")]
    public async Task<IActionResult> RedirectToOriginal(string shortCode, [FromServices] ShortLinker.Api.Services.AccessLogChannel logChannel)
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

        // 异步记录日志
        var log = new ShortLinker.Api.Models.LinkAccessLog
        {
            TenantId = tenantId,
            ShortCode = shortCode,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            Referer = Request.Headers.Referer.ToString()
        };
        _ = logChannel.AddLogAsync(log);

        return RedirectPermanent(originalUrl);
    }
```
```csharp
// Modify: src/ShortLinker.Api/Program.cs
// Add services:
builder.Services.AddSingleton<ShortLinker.Api.Services.AccessLogChannel>();
builder.Services.AddHostedService<ShortLinker.Api.Services.AccessLogWriterService>();
```

- [ ] **Step 4: 验证并 Commit**
```bash
dotnet build
git add .
git commit -m "feat(analytics): implement async background service for access logging"
```

### Task 3: 数据统计 API 接口

**Files:**
- Create: `src/ShortLinker.Api/Controllers/StatsController.cs`

- [ ] **Step 1: 创建 StatsController**
```csharp
// src/ShortLinker.Api/Controllers/StatsController.cs
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
```

- [ ] **Step 2: 验证并 Commit**
```bash
dotnet build
git add .
git commit -m "feat(analytics): add stats endpoints for summary, trends and devices"
```

### Task 4: 前端分析面板 (Recharts)

**Files:**
- Create: `src/ShortLinker.Web/src/hooks/useStats.ts`
- Create: `src/ShortLinker.Web/src/pages/Dashboard.tsx`
- Modify: `src/ShortLinker.Web/src/App.tsx`

- [ ] **Step 1: 安装 Recharts**
```bash
cd src/ShortLinker.Web
npm install recharts
```

- [ ] **Step 2: 编写 React Query Hooks**
```typescript
// src/ShortLinker.Web/src/hooks/useStats.ts
import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';

export const useStatsSummary = () => {
    return useQuery({
        queryKey: ['stats', 'summary'],
        queryFn: () => api.get('/stats/summary')
    });
};

export const useStatsTrend = (days: number = 7) => {
    return useQuery({
        queryKey: ['stats', 'trend', days],
        queryFn: () => api.get(`/stats/clicks?days=${days}`)
    });
};

export const useStatsDevices = () => {
    return useQuery({
        queryKey: ['stats', 'devices'],
        queryFn: () => api.get('/stats/devices')
    });
};
```

- [ ] **Step 3: 创建 Dashboard 视图**
```tsx
// src/ShortLinker.Web/src/pages/Dashboard.tsx
import { useStatsSummary, useStatsTrend, useStatsDevices } from '../hooks/useStats';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, PieChart, Pie, Cell } from 'recharts';

const COLORS = ['#0088FE', '#00C49F', '#FFBB28', '#FF8042', '#8884d8'];

export const Dashboard = () => {
    const { data: summary } = useStatsSummary();
    const { data: trend } = useStatsTrend();
    const { data: devices } = useStatsDevices();

    return (
        <div className="space-y-6 mb-8">
            <div className="grid grid-cols-2 gap-4">
                <div className="bg-white p-6 rounded-lg shadow border border-gray-100">
                    <h3 className="text-gray-500 text-sm font-medium">Total Links</h3>
                    <p className="text-3xl font-bold text-gray-900 mt-2">{summary?.totalLinks || 0}</p>
                </div>
                <div className="bg-white p-6 rounded-lg shadow border border-gray-100">
                    <h3 className="text-gray-500 text-sm font-medium">Total Clicks</h3>
                    <p className="text-3xl font-bold text-blue-600 mt-2">{summary?.totalClicks || 0}</p>
                </div>
            </div>

            <div className="grid grid-cols-3 gap-6">
                <div className="col-span-2 bg-white p-4 rounded-lg shadow border border-gray-100">
                    <h3 className="text-gray-700 font-medium mb-4">Click Trends (Last 7 Days)</h3>
                    <div className="h-64">
                        <ResponsiveContainer width="100%" height="100%">
                            <LineChart data={trend}>
                                <CartesianGrid strokeDasharray="3 3" vertical={false} />
                                <XAxis dataKey="date" tick={{fontSize: 12}} />
                                <YAxis tick={{fontSize: 12}} allowDecimals={false} />
                                <Tooltip />
                                <Line type="monotone" dataKey="count" stroke="#2563eb" strokeWidth={2} dot={{r: 4}} />
                            </LineChart>
                        </ResponsiveContainer>
                    </div>
                </div>

                <div className="col-span-1 bg-white p-4 rounded-lg shadow border border-gray-100">
                    <h3 className="text-gray-700 font-medium mb-4">OS Distribution</h3>
                    <div className="h-64">
                        <ResponsiveContainer width="100%" height="100%">
                            <PieChart>
                                <Pie data={devices} cx="50%" cy="50%" innerRadius={60} outerRadius={80} paddingAngle={5} dataKey="value">
                                    {devices?.map((entry: any, index: number) => (
                                        <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                                    ))}
                                </Pie>
                                <Tooltip />
                            </PieChart>
                        </ResponsiveContainer>
                    </div>
                </div>
            </div>
        </div>
    );
};
```

- [ ] **Step 4: 将 Dashboard 集成到前台 Layout**
```tsx
// Modify: src/ShortLinker.Web/src/App.tsx
// Add <Dashboard /> above <CreateLinkForm /> in TenantDashboard:
import { Dashboard } from './pages/Dashboard';

// Inside App.tsx
const TenantDashboard = () => (
    <>
        <Dashboard />
        <CreateLinkForm />
        <LinkList />
    </>
);
```

- [ ] **Step 5: 验证并 Commit**
```bash
cd src/ShortLinker.Web
npm run build
git add .
git commit -m "feat(analytics): implement dashboard with recharts integration"
```