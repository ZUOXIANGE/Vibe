# API Key Authentication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现多租户环境下的 API Key 生成、安全存储和请求拦截鉴权机制，并提供前端管理界面。

**Architecture:** 后端利用 `System.Security.Cryptography` 对生成的 API Key 进行 SHA256 哈希后存入 EF Core，并实现自定义 `AuthenticationHandler` 拦截 `X-Api-Key`。前端提供专门的 API Keys 列表与创建弹窗。

**Tech Stack:** ASP.NET Core 10 (Authentication), Entity Framework Core, React 18, TailwindCSS.

---

### Task 1: API Key 实体与数据层配置

**Files:**
- Create: `src/ShortLinker.Api/Models/TenantApiKey.cs`
- Modify: `src/ShortLinker.Api/Infrastructure/ApplicationDbContext.cs`

- [ ] **Step 1: 创建实体模型**
```csharp
// src/ShortLinker.Api/Models/TenantApiKey.cs
namespace ShortLinker.Api.Models;

public class TenantApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
```

- [ ] **Step 2: 配置 DbContext**
```csharp
// Modify: src/ShortLinker.Api/Infrastructure/ApplicationDbContext.cs
// Add DbSet
public DbSet<TenantApiKey> ApiKeys => Set<TenantApiKey>();

// Add to OnModelCreating
modelBuilder.Entity<TenantApiKey>().HasQueryFilter(s => EF.Property<string>(s, "TenantId") == _tenantContext.TenantId);
modelBuilder.Entity<TenantApiKey>().HasIndex(k => k.KeyHash).IsUnique();
```

- [ ] **Step 3: 生成并验证迁移**
```bash
cd src/ShortLinker.Api
dotnet ef migrations add AddApiKeyEntity
dotnet build
cd ../..
```

- [ ] **Step 4: Commit**
```bash
git add .
git commit -m "feat(auth): add TenantApiKey entity and database migration"
```

### Task 2: API Key 生成与控制器接口

**Files:**
- Create: `src/ShortLinker.Api/Models/ApiKeyDtos.cs`
- Create: `src/ShortLinker.Api/Controllers/ApiKeysController.cs`

- [ ] **Step 1: 定义数据传输对象**
```csharp
// src/ShortLinker.Api/Models/ApiKeyDtos.cs
namespace ShortLinker.Api.Models;

public class CreateApiKeyRequest
{
    public string Name { get; set; } = string.Empty;
}

public class ApiKeyResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class CreateApiKeyResponse : ApiKeyResponse
{
    public string PlainTextKey { get; set; } = string.Empty;
}
```

- [ ] **Step 2: 创建接口控制器与哈希逻辑**
```csharp
// src/ShortLinker.Api/Controllers/ApiKeysController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShortLinker.Api.Infrastructure;
using ShortLinker.Api.Models;
using System.Security.Cryptography;
using System.Text;

namespace ShortLinker.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ApiKeysController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ApiKeysController(ApplicationDbContext db)
    {
        _db = db;
    }

    private static string HashKey(string key)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(bytes);
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ApiKeyResponse>>>> GetList()
    {
        var keys = await _db.ApiKeys.Select(k => new ApiKeyResponse
        {
            Id = k.Id, Name = k.Name, Hint = k.Hint, CreatedAt = k.CreatedAt, IsActive = k.IsActive
        }).ToListAsync();
        return ApiResponse<List<ApiKeyResponse>>.Success(keys);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CreateApiKeyResponse>>> Create([FromBody] CreateApiKeyRequest request)
    {
        var rawKey = "sk_live_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "").Replace("/", "").Replace("=", "").Substring(0, 32);
        
        var apiKey = new TenantApiKey
        {
            Name = request.Name,
            KeyHash = HashKey(rawKey),
            Hint = $"sk_live_...{rawKey.Substring(rawKey.Length - 4)}"
        };

        _db.ApiKeys.Add(apiKey);
        await _db.SaveChangesAsync();

        return ApiResponse<CreateApiKeyResponse>.Success(new CreateApiKeyResponse
        {
            Id = apiKey.Id, Name = apiKey.Name, Hint = apiKey.Hint, 
            CreatedAt = apiKey.CreatedAt, IsActive = apiKey.IsActive,
            PlainTextKey = rawKey // Only returned once
        });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Revoke(Guid id)
    {
        var key = await _db.ApiKeys.FindAsync(id);
        if (key == null) return ApiResponse<bool>.Error("Key not found", 404);
        
        _db.ApiKeys.Remove(key);
        await _db.SaveChangesAsync();
        return ApiResponse<bool>.Success(true);
    }
}
```

- [ ] **Step 3: 验证并 Commit**
```bash
dotnet build
git add .
git commit -m "feat(auth): implement apikey generation and management controller"
```

### Task 3: API Key 鉴权中间件与拦截

**Files:**
- Create: `src/ShortLinker.Api/Authentication/ApiKeyAuthenticationHandler.cs`
- Modify: `src/ShortLinker.Api/Program.cs`
- Modify: `src/ShortLinker.Api/Controllers/LinksController.cs`

- [ ] **Step 1: 实现鉴权 Handler**
```csharp
// src/ShortLinker.Api/Authentication/ApiKeyAuthenticationHandler.cs
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShortLinker.Api.Infrastructure;

namespace ShortLinker.Api.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions { }

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger, UrlEncoder encoder,
        ApplicationDbContext db, ITenantContext tenantContext)
        : base(options, logger, encoder)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var extractedApiKey))
        {
            return AuthenticateResult.NoResult();
        }

        var providedApiKey = extractedApiKey.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedApiKey)) return AuthenticateResult.NoResult();

        using var sha256 = SHA256.Create();
        var providedHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(providedApiKey)));

        // Ignore tenant filter because we are trying to figure out which tenant this key belongs to
        var apiKeyEntity = await _db.ApiKeys.IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.KeyHash == providedHash && k.IsActive);

        if (apiKeyEntity == null)
        {
            return AuthenticateResult.Fail("Invalid API Key");
        }

        // Set tenant context for the rest of the request
        _tenantContext.SetTenantId(apiKeyEntity.TenantId);

        var claims = new[] {
            new Claim(ClaimTypes.NameIdentifier, apiKeyEntity.TenantId),
            new Claim("ApiKeyId", apiKeyEntity.Id.ToString())
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
```

- [ ] **Step 2: 注册服务并保护业务接口**
```csharp
// Modify: src/ShortLinker.Api/Program.cs
// Add authentication registration before app.Build():
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<ShortLinker.Api.Authentication.ApiKeyAuthenticationOptions, ShortLinker.Api.Authentication.ApiKeyAuthenticationHandler>("ApiKey", null);

builder.Services.AddAuthorization();

// Add UseAuthentication and UseAuthorization before UseControllers:
app.UseAuthentication();
app.UseAuthorization();
```

```csharp
// Modify: src/ShortLinker.Api/Controllers/LinksController.cs
// Add [Authorize] to protect external facing creation logic
using Microsoft.AspNetCore.Authorization;

namespace ShortLinker.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(AuthenticationSchemes = "ApiKey")] // Protect all endpoints here
public class LinksController : ControllerBase
{
    // ... existing code ...
}
```

- [ ] **Step 3: 验证并 Commit**
```bash
dotnet build
git add .
git commit -m "feat(auth): implement apikey authentication handler and protect endpoints"
```

### Task 4: 前端 API Key 管理界面

**Files:**
- Create: `src/ShortLinker.Web/src/hooks/useApiKeys.ts`
- Create: `src/ShortLinker.Web/src/pages/ApiKeys.tsx`
- Modify: `src/ShortLinker.Web/src/App.tsx`

- [ ] **Step 1: 编写 React Query Hooks**
```typescript
// src/ShortLinker.Web/src/hooks/useApiKeys.ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../lib/api';

export interface ApiKeyResponse {
    id: string;
    name: string;
    hint: string;
    createdAt: string;
    isActive: boolean;
    plainTextKey?: string;
}

export const useApiKeys = () => {
    return useQuery<ApiKeyResponse[]>({
        queryKey: ['apikeys'],
        queryFn: () => api.get('/apikeys')
    });
};

export const useCreateApiKey = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (name: string) => api.post('/apikeys', { name }),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['apikeys'] })
    });
};

export const useRevokeApiKey = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (id: string) => api.delete(`/apikeys/${id}`),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['apikeys'] })
    });
};
```

- [ ] **Step 2: 创建管理面板 UI**
```tsx
// src/ShortLinker.Web/src/pages/ApiKeys.tsx
import React, { useState } from 'react';
import { useApiKeys, useCreateApiKey, useRevokeApiKey } from '../hooks/useApiKeys';
import { format } from 'date-fns';
import { Key, Trash2 } from 'lucide-react';

export const ApiKeys = () => {
    const { data: keys, isLoading } = useApiKeys();
    const createKey = useCreateApiKey();
    const revokeKey = useRevokeApiKey();
    const [name, setName] = useState('');
    const [newKey, setNewKey] = useState<string | null>(null);

    const handleCreate = (e: React.FormEvent) => {
        e.preventDefault();
        if (!name) return;
        createKey.mutate(name, {
            onSuccess: (data: any) => {
                setNewKey(data.plainTextKey);
                setName('');
            }
        });
    };

    return (
        <div className="space-y-6">
            <h2 className="text-xl font-bold text-gray-900">API Keys</h2>
            <p className="text-gray-500">Manage API keys for external application integration.</p>

            {newKey && (
                <div className="bg-yellow-50 border-l-4 border-yellow-400 p-4 mb-6">
                    <div className="flex">
                        <div className="ml-3">
                            <h3 className="text-sm font-medium text-yellow-800">Please copy your new API Key now</h3>
                            <div className="mt-2 text-sm text-yellow-700">
                                <p>You will not be able to see it again after you close this page.</p>
                                <code className="mt-2 block bg-yellow-100 p-2 rounded text-lg">{newKey}</code>
                            </div>
                            <button onClick={() => setNewKey(null)} className="mt-4 text-sm font-medium text-yellow-800 hover:text-yellow-600">I have copied it</button>
                        </div>
                    </div>
                </div>
            )}

            <form onSubmit={handleCreate} className="flex gap-4">
                <input
                    type="text"
                    required
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    placeholder="Key Name (e.g. Mobile App)"
                    className="flex-1 max-w-sm rounded-md border-gray-300 shadow-sm px-4 py-2 border focus:ring-blue-500 focus:border-blue-500"
                />
                <button type="submit" disabled={createKey.isPending} className="bg-gray-800 text-white px-4 py-2 rounded-md hover:bg-gray-900 flex items-center gap-2">
                    <Key size={18} /> Generate New Key
                </button>
            </form>

            <div className="bg-white shadow rounded-lg overflow-hidden border border-gray-100">
                <table className="min-w-full divide-y divide-gray-200">
                    <thead className="bg-gray-50">
                        <tr>
                            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Name</th>
                            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Hint</th>
                            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Created</th>
                            <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Action</th>
                        </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-200">
                        {isLoading ? (
                            <tr><td colSpan={4} className="text-center py-4 text-gray-500">Loading...</td></tr>
                        ) : keys?.map((k) => (
                            <tr key={k.id}>
                                <td className="px-6 py-4 whitespace-nowrap font-medium text-gray-900">{k.name}</td>
                                <td className="px-6 py-4 whitespace-nowrap font-mono text-sm text-gray-500">{k.hint}</td>
                                <td className="px-6 py-4 whitespace-nowrap text-gray-500">{format(new Date(k.createdAt), 'MMM d, yyyy')}</td>
                                <td className="px-6 py-4 whitespace-nowrap">
                                    <button onClick={() => revokeKey.mutate(k.id)} className="text-red-500 hover:text-red-700 flex items-center gap-1">
                                        <Trash2 size={16} /> Revoke
                                    </button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
};
```

- [ ] **Step 3: 集成路由**
```tsx
// Modify: src/ShortLinker.Web/src/App.tsx
// Add navigation links and route for ApiKeys
import { ApiKeys } from './pages/ApiKeys';
import { Link } from 'react-router-dom';

// Update TenantLayout header:
const TenantLayout = () => (
    <div className="min-h-screen bg-gray-50">
        <header className="bg-white shadow-sm p-4 border-b">
            <div className="max-w-7xl mx-auto flex justify-between items-center">
                <div className="flex items-center gap-6">
                    <h1 className="text-2xl font-bold text-gray-900">ShortLinker</h1>
                    <nav className="flex gap-4">
                        <Link to="/" className="text-gray-600 hover:text-gray-900 font-medium">Dashboard</Link>
                        <Link to="/apikeys" className="text-gray-600 hover:text-gray-900 font-medium">API Keys</Link>
                    </nav>
                </div>
                <span className="text-sm text-gray-500 bg-gray-100 px-3 py-1 rounded-full">
                    Tenant: {localStorage.getItem('tenant_id') || 'default'}
                </span>
            </div>
        </header>
        <main className="p-6 max-w-7xl mx-auto">
            <Outlet />
        </main>
    </div>
);

// Add Route in App() under TenantLayout:
<Route path="apikeys" element={<ApiKeys />} />
```

- [ ] **Step 4: 验证并 Commit**
```bash
cd src/ShortLinker.Web
npm run build
git add .
git commit -m "feat(auth): integrate api keys management UI"
```