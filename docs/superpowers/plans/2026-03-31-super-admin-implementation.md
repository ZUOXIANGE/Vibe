# Super Admin Module Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现短链接系统的全局超级管理员模块，包括后端的租户管理实体与无视数据隔离的 Admin 接口，以及前端独立的 Admin 面板。

**Architecture:** 在现有的 ASP.NET Core MVC 中开辟 `/api/admin` 路由组并利用 EF Core 的 `IgnoreQueryFilters()` 进行全局查询。在现有的 React 应用中利用 `react-router-dom` 增加 `/admin` 命名空间。

**Tech Stack:** ASP.NET Core 10, Entity Framework Core, React 18, React Query, TailwindCSS.

---

### Task 1: 后端 Tenant 实体与数据库迁移

**Files:**
- Create: `src/ShortLinker.Api/Models/Tenant.cs`
- Modify: `src/ShortLinker.Api/Infrastructure/ApplicationDbContext.cs`

- [ ] **Step 1: 创建 Tenant 实体模型**
```csharp
// src/ShortLinker.Api/Models/Tenant.cs
namespace ShortLinker.Api.Models;

public class Tenant
{
    public string Id { get; set; } = string.Empty; // e.g. "tenant1"
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
```

- [ ] **Step 2: 配置 DbContext**
```csharp
// Modify: src/ShortLinker.Api/Infrastructure/ApplicationDbContext.cs
// Add DbSet
public DbSet<Tenant> Tenants => Set<Tenant>();

// Add to OnModelCreating
modelBuilder.Entity<Tenant>().HasKey(t => t.Id);
```

- [ ] **Step 3: 生成并验证迁移**
```bash
cd src/ShortLinker.Api
dotnet ef migrations add AddTenantEntity
dotnet build
cd ../..
```

- [ ] **Step 4: Commit**
```bash
git add .
git commit -m "feat(admin): add Tenant entity and database migration"
```

### Task 2: 后端超级管理员控制器实现

**Files:**
- Create: `src/ShortLinker.Api/Models/TenantDto.cs`
- Create: `src/ShortLinker.Api/Controllers/Admin/TenantsAdminController.cs`

- [ ] **Step 1: 定义 Admin 响应 DTO**
```csharp
// src/ShortLinker.Api/Models/TenantDto.cs
namespace ShortLinker.Api.Models;

public class TenantDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public int TotalLinks { get; set; }
}
```

- [ ] **Step 2: 创建 Admin Controller**
```csharp
// src/ShortLinker.Api/Controllers/Admin/TenantsAdminController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShortLinker.Api.Infrastructure;
using ShortLinker.Api.Models;

namespace ShortLinker.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/v1/tenants")]
public class TenantsAdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public TenantsAdminController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<TenantDto>>>> GetAllTenants()
    {
        // Require dummy admin token for now
        if (!Request.Headers.TryGetValue("X-Admin-Token", out var token) || token != "super-secret-admin-token")
        {
            return Unauthorized();
        }

        var tenants = await _db.Tenants.ToListAsync();
        // IgnoreQueryFilters to count across all tenants
        var linksCount = await _db.ShortLinks.IgnoreQueryFilters()
            .GroupBy(l => l.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count);

        var result = tenants.Select(t => new TenantDto
        {
            Id = t.Id,
            Name = t.Name,
            CreatedAt = t.CreatedAt,
            IsActive = t.IsActive,
            TotalLinks = linksCount.GetValueOrDefault(t.Id, 0)
        }).ToList();

        return ApiResponse<List<TenantDto>>.Success(result);
    }

    [HttpPost("{id}/toggle-status")]
    public async Task<ActionResult<ApiResponse<bool>>> ToggleStatus(string id)
    {
        if (!Request.Headers.TryGetValue("X-Admin-Token", out var token) || token != "super-secret-admin-token")
        {
            return Unauthorized();
        }

        if (id == "system") return ApiResponse<bool>.Error("Cannot modify system tenant", 400);

        var tenant = await _db.Tenants.FindAsync(id);
        if (tenant == null) return ApiResponse<bool>.Error("Tenant not found", 404);

        tenant.IsActive = !tenant.IsActive;
        await _db.SaveChangesAsync();

        return ApiResponse<bool>.Success(tenant.IsActive);
    }
}
```

- [ ] **Step 3: 验证并 Commit**
```bash
dotnet build
git add .
git commit -m "feat(admin): implement super admin controller with IgnoreQueryFilters"
```

### Task 3: 前端 Admin API Client 与 Hooks

**Files:**
- Create: `src/ShortLinker.Web/src/lib/adminApi.ts`
- Create: `src/ShortLinker.Web/src/hooks/useAdmin.ts`

- [ ] **Step 1: 封装 Admin Axios 实例**
```typescript
// src/ShortLinker.Web/src/lib/adminApi.ts
import axios from 'axios';
import { ApiResponse } from './api';

export const adminApi = axios.create({
    baseURL: '/api/admin/v1',
    timeout: 10000,
    headers: {
        'X-Admin-Token': 'super-secret-admin-token' // Dummy token for phase 1
    }
});

adminApi.interceptors.response.use(
    (response) => {
        const res = response.data as ApiResponse<any>;
        if (res.code !== 200) {
            return Promise.reject(new Error(res.msg || 'Admin API Error'));
        }
        return res.data;
    },
    (error) => Promise.reject(error)
);
```

- [ ] **Step 2: 编写 React Query Hooks**
```typescript
// src/ShortLinker.Web/src/hooks/useAdmin.ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { adminApi } from '../lib/adminApi';

export interface TenantDto {
    id: string;
    name: string;
    createdAt: string;
    isActive: boolean;
    totalLinks: number;
}

export const useAdminTenants = () => {
    return useQuery<TenantDto[]>({
        queryKey: ['admin', 'tenants'],
        queryFn: () => adminApi.get('/tenants')
    });
};

export const useToggleTenantStatus = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (tenantId: string) => adminApi.post(`/tenants/${tenantId}/toggle-status`),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['admin', 'tenants'] });
        }
    });
};
```

- [ ] **Step 3: 验证并 Commit**
```bash
cd src/ShortLinker.Web
npm run build
git add .
git commit -m "feat(admin): add admin api client and react query hooks"
```

### Task 4: 前端 Admin 视图与路由集成

**Files:**
- Create: `src/ShortLinker.Web/src/pages/AdminLayout.tsx`
- Create: `src/ShortLinker.Web/src/pages/AdminTenants.tsx`
- Modify: `src/ShortLinker.Web/src/App.tsx`

- [ ] **Step 1: 创建 Admin Layout**
```tsx
// src/ShortLinker.Web/src/pages/AdminLayout.tsx
import { Outlet, Link } from 'react-router-dom';
import { ShieldAlert } from 'lucide-react';

export const AdminLayout = () => {
    return (
        <div className="min-h-screen bg-slate-900 text-slate-100">
            <header className="bg-slate-800 shadow-md p-4 border-b border-slate-700">
                <div className="max-w-7xl mx-auto flex items-center gap-2">
                    <ShieldAlert className="text-red-500" />
                    <h1 className="text-xl font-bold">Super Admin Portal</h1>
                    <nav className="ml-8 flex gap-4">
                        <Link to="/admin/tenants" className="text-slate-300 hover:text-white">Tenants</Link>
                        <Link to="/" className="text-slate-500 hover:text-slate-300 ml-auto">Exit Admin</Link>
                    </nav>
                </div>
            </header>
            <main className="p-6 max-w-7xl mx-auto">
                <Outlet />
            </main>
        </div>
    );
};
```

- [ ] **Step 2: 创建 Admin 租户列表页**
```tsx
// src/ShortLinker.Web/src/pages/AdminTenants.tsx
import { useAdminTenants, useToggleTenantStatus } from '../hooks/useAdmin';
import { format } from 'date-fns';

export const AdminTenants = () => {
    const { data: tenants, isLoading } = useAdminTenants();
    const toggleStatus = useToggleTenantStatus();

    if (isLoading) return <div className="text-center py-8">Loading tenants...</div>;

    return (
        <div className="bg-slate-800 shadow rounded-lg overflow-hidden border border-slate-700">
            <table className="min-w-full divide-y divide-slate-700">
                <thead className="bg-slate-900">
                    <tr>
                        <th className="px-6 py-3 text-left text-xs font-medium text-slate-400 uppercase">Tenant ID</th>
                        <th className="px-6 py-3 text-left text-xs font-medium text-slate-400 uppercase">Name</th>
                        <th className="px-6 py-3 text-left text-xs font-medium text-slate-400 uppercase">Links</th>
                        <th className="px-6 py-3 text-left text-xs font-medium text-slate-400 uppercase">Status</th>
                        <th className="px-6 py-3 text-left text-xs font-medium text-slate-400 uppercase">Actions</th>
                    </tr>
                </thead>
                <tbody className="divide-y divide-slate-700">
                    {tenants?.map((tenant) => (
                        <tr key={tenant.id}>
                            <td className="px-6 py-4 whitespace-nowrap font-medium">{tenant.id}</td>
                            <td className="px-6 py-4 whitespace-nowrap text-slate-300">{tenant.name}</td>
                            <td className="px-6 py-4 whitespace-nowrap text-slate-300">{tenant.totalLinks}</td>
                            <td className="px-6 py-4 whitespace-nowrap">
                                <span className={`px-2 py-1 text-xs rounded-full ${tenant.isActive ? 'bg-green-900 text-green-300' : 'bg-red-900 text-red-300'}`}>
                                    {tenant.isActive ? 'Active' : 'Banned'}
                                </span>
                            </td>
                            <td className="px-6 py-4 whitespace-nowrap">
                                <button 
                                    onClick={() => toggleStatus.mutate(tenant.id)}
                                    disabled={toggleStatus.isPending || tenant.id === 'system'}
                                    className={`px-3 py-1 rounded text-sm ${tenant.isActive ? 'bg-red-600 hover:bg-red-700' : 'bg-green-600 hover:bg-green-700'} disabled:opacity-50`}
                                >
                                    {tenant.isActive ? 'Ban' : 'Unban'}
                                </button>
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
};
```

- [ ] **Step 3: 重构 App.tsx 集成 react-router-dom**
```tsx
// src/ShortLinker.Web/src/App.tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Routes, Route, Outlet } from 'react-router-dom';
import { CreateLinkForm } from './components/CreateLinkForm';
import { LinkList } from './components/LinkList';
import { AdminLayout } from './pages/AdminLayout';
import { AdminTenants } from './pages/AdminTenants';

const queryClient = new QueryClient({
    defaultOptions: { queries: { refetchOnWindowFocus: false, retry: 1 } }
});

const TenantLayout = () => (
    <div className="min-h-screen bg-gray-50">
        <header className="bg-white shadow-sm p-4 border-b">
            <div className="max-w-7xl mx-auto flex justify-between items-center">
                <h1 className="text-2xl font-bold text-gray-900">ShortLinker</h1>
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

const TenantDashboard = () => (
    <>
        <CreateLinkForm />
        <LinkList />
    </>
);

function App() {
    return (
        <QueryClientProvider client={queryClient}>
            <BrowserRouter>
                <Routes>
                    {/* Tenant Routes */}
                    <Route path="/" element={<TenantLayout />}>
                        <Route index element={<TenantDashboard />} />
                    </Route>
                    
                    {/* Super Admin Routes */}
                    <Route path="/admin" element={<AdminLayout />}>
                        <Route path="tenants" element={<AdminTenants />} />
                    </Route>
                </Routes>
            </BrowserRouter>
        </QueryClientProvider>
    );
}

export default App;
```

- [ ] **Step 4: 验证并 Commit**
```bash
cd src/ShortLinker.Web
npm run build
git add .
git commit -m "feat(admin): integrate react-router and complete super admin views"
```