# Multi-Tenant Short Link System Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建多租户短链接管理平台的核心后端（Phase 1 & 2），实现租户数据隔离、短码生成、统一响应格式以及高性能缓存重定向。

**Architecture:** 基于 ASP.NET Core 10 MVC，使用 EF Core 10 进行数据持久化。采用共享数据库/共享 Schema 的多租户策略，通过 TenantId 进行逻辑隔离。使用 FusionCache 结合 Redis 提供内存和分布式旁路缓存策略实现毫秒级重定向。

**Tech Stack:** ASP.NET Core 10, Entity Framework Core 10, Npgsql, ZiggyCreatures.FusionCache, Redis, xUnit.

---

### Task 1: 项目基础骨架与 Docker Compose

**Files:**
- Create: `docker-compose.yml`
- Modify: `.gitignore`
- Create: `src/ShortLinker.Api/ShortLinker.Api.csproj`
- Create: `tests/ShortLinker.Tests/ShortLinker.Tests.csproj`

- [ ] **Step 1: 创建基础设施配置**
```yaml
# docker-compose.yml
services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: shortlinker
      POSTGRES_PASSWORD: password123
      POSTGRES_DB: shortlinker_db
    ports:
      - "5432:5432"
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
```

- [ ] **Step 2: 初始化 .NET 10 项目结构**
```bash
dotnet new sln -n ShortLinker
mkdir -p src tests
dotnet new webapi -n ShortLinker.Api -o src/ShortLinker.Api -f net10.0 --use-controllers
dotnet new xunit -n ShortLinker.Tests -o tests/ShortLinker.Tests -f net10.0
dotnet sln add src/ShortLinker.Api/ShortLinker.Api.csproj
dotnet sln add tests/ShortLinker.Tests/ShortLinker.Tests.csproj
dotnet add tests/ShortLinker.Tests/ShortLinker.Tests.csproj reference src/ShortLinker.Api/ShortLinker.Api.csproj
```

- [ ] **Step 3: 添加核心依赖包**
```bash
cd src/ShortLinker.Api
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package ZiggyCreatures.FusionCache
dotnet add package ZiggyCreatures.FusionCache.Serialization.SystemTextJson
dotnet add package ZiggyCreatures.FusionCache.Backplane.StackExchangeRedis
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
cd ../..
```

- [ ] **Step 4: 启动基础设施并验证编译**
```bash
docker compose up -d
dotnet build
dotnet test
```

- [ ] **Step 5: Commit**
```bash
git init
dotnet new gitignore
git add .
git commit -m "chore: 初始化 ASP.NET Core 10 解决方案及基础设施配置"
```

### Task 2: 统一响应格式与租户上下文 (Phase 1)

**Files:**
- Create: `src/ShortLinker.Api/Models/ApiResponse.cs`
- Create: `src/ShortLinker.Api/Infrastructure/TenantContext.cs`
- Create: `src/ShortLinker.Api/Middleware/TenantResolutionMiddleware.cs`
- Test: `tests/ShortLinker.Tests/TenantResolutionMiddlewareTests.cs`
- Modify: `src/ShortLinker.Api/Program.cs`

- [ ] **Step 1: 创建统一响应模型**
```csharp
// src/ShortLinker.Api/Models/ApiResponse.cs
namespace ShortLinker.Api.Models;

public class ApiResponse<T>
{
    public int Code { get; set; } = 200;
    public string Msg { get; set; } = "success";
    public T? Data { get; set; }

    public static ApiResponse<T> Success(T data, string msg = "success") => new() { Data = data, Msg = msg };
    public static ApiResponse<T> Error(string msg, int code = 400) => new() { Code = code, Msg = msg, Data = default };
}
```

- [ ] **Step 2: 定义租户上下文**
```csharp
// src/ShortLinker.Api/Infrastructure/TenantContext.cs
namespace ShortLinker.Api.Infrastructure;

public interface ITenantContext
{
    string? TenantId { get; }
    void SetTenantId(string tenantId);
}

public class TenantContext : ITenantContext
{
    public string? TenantId { get; private set; }
    public void SetTenantId(string tenantId) => TenantId = tenantId;
}
```

- [ ] **Step 3: 编写租户解析中间件**
```csharp
// src/ShortLinker.Api/Middleware/TenantResolutionMiddleware.cs
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
```

- [ ] **Step 4: 注册服务和中间件**
```csharp
// Modify: src/ShortLinker.Api/Program.cs
// Add before builder.Build():
builder.Services.AddScoped<ShortLinker.Api.Infrastructure.ITenantContext, ShortLinker.Api.Infrastructure.TenantContext>();

// Add after app.Build():
app.UseMiddleware<ShortLinker.Api.Middleware.TenantResolutionMiddleware>();
```

- [ ] **Step 5: 编写测试**
```csharp
// tests/ShortLinker.Tests/TenantResolutionMiddlewareTests.cs
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
```

- [ ] **Step 6: 运行测试并 Commit**
```bash
dotnet add tests/ShortLinker.Tests/ShortLinker.Tests.csproj package Moq
dotnet test
git add .
git commit -m "feat: 实现统一响应模型与多租户解析中间件"
```

### Task 3: EF Core 数据层与全局查询过滤器 (Phase 1)

**Files:**
- Create: `src/ShortLinker.Api/Models/ShortLink.cs`
- Create: `src/ShortLinker.Api/Infrastructure/ApplicationDbContext.cs`
- Modify: `src/ShortLinker.Api/appsettings.json`
- Modify: `src/ShortLinker.Api/Program.cs`

- [ ] **Step 1: 定义短链接实体**
```csharp
// src/ShortLinker.Api/Models/ShortLink.cs
namespace ShortLinker.Api.Models;

public class ShortLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
```

- [ ] **Step 2: 创建 DbContext 并配置租户隔离**
```csharp
// src/ShortLinker.Api/Infrastructure/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using ShortLinker.Api.Models;

namespace ShortLinker.Api.Infrastructure;

public class ApplicationDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<ShortLink> ShortLinks => Set<ShortLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<ShortLink>()
            .HasIndex(s => new { s.TenantId, s.ShortCode })
            .IsUnique();

        // Global Query Filter for Tenant Isolation
        modelBuilder.Entity<ShortLink>().HasQueryFilter(s => EF.Property<string>(s, "TenantId") == _tenantContext.TenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<ShortLink>().Where(e => e.State == EntityState.Added))
        {
            if (string.IsNullOrEmpty(entry.Entity.TenantId) && !string.IsNullOrEmpty(_tenantContext.TenantId))
            {
                entry.Entity.TenantId = _tenantContext.TenantId;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 3: 配置数据库连接**
```json
// Modify src/ShortLinker.Api/appsettings.json to include:
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Database=shortlinker_db;Username=shortlinker;Password=password123"
}
```

- [ ] **Step 4: 注册 EF Core 并生成迁移**
```csharp
// Modify: src/ShortLinker.Api/Program.cs
// Add to builder:
builder.Services.AddDbContext<ShortLinker.Api.Infrastructure.ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
```
```bash
cd src/ShortLinker.Api
dotnet ef migrations add InitialCreate
dotnet ef database update
cd ../..
```

- [ ] **Step 5: 验证并 Commit**
```bash
dotnet build
git add .
git commit -m "feat: 配置 EF Core 与 PostgreSQL，实现基于 TenantId 的数据隔离"
```

### Task 4: 短码生成与缓存配置 (Phase 1)

**Files:**
- Create: `src/ShortLinker.Api/Services/IShortcodeGenerator.cs`
- Create: `src/ShortLinker.Api/Services/Base62ShortcodeGenerator.cs`
- Modify: `src/ShortLinker.Api/Program.cs`
- Test: `tests/ShortLinker.Tests/Base62ShortcodeGeneratorTests.cs`

- [ ] **Step 1: 实现短码生成服务**
```csharp
// src/ShortLinker.Api/Services/IShortcodeGenerator.cs
namespace ShortLinker.Api.Services;

public interface IShortcodeGenerator
{
    string Generate();
}

// src/ShortLinker.Api/Services/Base62ShortcodeGenerator.cs
namespace ShortLinker.Api.Services;

public class Base62ShortcodeGenerator : IShortcodeGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private readonly Random _random = new();

    public string Generate()
    {
        return new string(Enumerable.Repeat(Alphabet, 6)
            .Select(s => s[_random.Next(s.Length)]).ToArray());
    }
}
```

- [ ] **Step 2: 编写测试**
```csharp
// tests/ShortLinker.Tests/Base62ShortcodeGeneratorTests.cs
using ShortLinker.Api.Services;

namespace ShortLinker.Tests;

public class Base62ShortcodeGeneratorTests
{
    [Fact]
    public void Should_Generate_6_Character_String()
    {
        var generator = new Base62ShortcodeGenerator();
        var code = generator.Generate();
        Assert.Equal(6, code.Length);
    }
}
```

- [ ] **Step 3: 配置 FusionCache 与 Redis**
```csharp
// Modify: src/ShortLinker.Api/Program.cs
// Add to builder:
builder.Services.AddScoped<ShortLinker.Api.Services.IShortcodeGenerator, ShortLinker.Api.Services.Base62ShortcodeGenerator>();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
});

builder.Services.AddFusionCache()
    .WithDefaultEntryOptions(new ZiggyCreatures.Caching.Fusion.FusionCacheEntryOptions {
        Duration = TimeSpan.FromMinutes(10),
        FailSafeMaxDuration = TimeSpan.FromHours(2),
        IsFailSafeEnabled = true
    })
    .WithSerializer(new ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson.FusionCacheSystemTextJsonSerializer())
    .WithDistributedCache(new Microsoft.Extensions.DependencyInjection.FusionCacheExtMethods.DistributedCacheConfigurator())
    .WithBackplane(new ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis.RedisBackplane(new ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis.RedisBackplaneOptions { Configuration = "localhost:6379" }));
```

- [ ] **Step 4: 运行测试并 Commit**
```bash
dotnet test
git add .
git commit -m "feat: 实现基础 Base62 短码生成器并配置 FusionCache 多级缓存"
```

### Task 5: 核心业务接口实现 (Phase 2)

**Files:**
- Create: `src/ShortLinker.Api/Controllers/LinksController.cs`
- Create: `src/ShortLinker.Api/Controllers/RedirectController.cs`
- Modify: `src/ShortLinker.Api/Program.cs`

- [x] **Step 1: 编写 CRUD 管理接口**
```csharp
// src/ShortLinker.Api/Controllers/LinksController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShortLinker.Api.Infrastructure;
using ShortLinker.Api.Models;
using ShortLinker.Api.Services;

namespace ShortLinker.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class LinksController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IShortcodeGenerator _generator;

    public LinksController(ApplicationDbContext db, IShortcodeGenerator generator)
    {
        _db = db;
        _generator = generator;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ShortLink>>> Create([FromBody] string originalUrl)
    {
        var shortCode = _generator.Generate();
        var link = new ShortLink { OriginalUrl = originalUrl, ShortCode = shortCode };
        _db.ShortLinks.Add(link);
        await _db.SaveChangesAsync();
        return ApiResponse<ShortLink>.Success(link);
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ShortLink>>>> GetList()
    {
        var links = await _db.ShortLinks.ToListAsync();
        return ApiResponse<List<ShortLink>>.Success(links);
    }
}
```

- [x] **Step 2: 编写重定向接口 (旁路缓存)**
```csharp
// src/ShortLinker.Api/Controllers/RedirectController.cs
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
```

- [x] **Step 3: 配置 OpenAPI/Swagger**
```csharp
// Modify: src/ShortLinker.Api/Program.cs
// ASP.NET Core 10 includes OpenAPI by default, ensure it is enabled:
builder.Services.AddOpenApi();
// After app.Build():
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
```

- [x] **Step 4: 编译并 Commit**
```bash
dotnet build
git add .
git commit -m "feat: 实现链接 CRUD 与高性能缓存重定向接口，配置 OpenAPI"
```

---
*Note: Phase 3 (Frontend React App) & Phase 4 (Perf Tuning) are logically distinct subsystems and should be planned separately after Phase 1 & 2 backend core is implemented and stable.*