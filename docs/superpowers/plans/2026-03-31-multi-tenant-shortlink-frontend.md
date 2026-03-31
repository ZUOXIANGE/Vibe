# Multi-Tenant Short Link System Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建多租户短链接管理平台的前端管理后台（Phase 3），实现基于租户上下文的短链接 CRUD 和状态管理。

**Architecture:** 基于 React 18 + Vite 搭建单页应用（SPA）。使用 TypeScript 保证类型安全，TailwindCSS 进行快速 UI 构建。状态管理和数据同步采用 React Query。与后端的通信基于自动生成的 OpenAPI 客户端，并通过统一拦截器处理 `ApiResponse` 和 `TenantId` 的注入。

**Tech Stack:** React 18, Vite, TypeScript, TailwindCSS, React Query, Axios, Lucide React (图标).

---

### Task 1: 初始化 React 项目与 TailwindCSS 配置

**Files:**
- Create: `src/ShortLinker.Web/package.json`
- Create: `src/ShortLinker.Web/tailwind.config.js`
- Create: `src/ShortLinker.Web/postcss.config.js`
- Create: `src/ShortLinker.Web/src/index.css`

- [ ] **Step 1: 创建 Vite + React + TS 项目**
```bash
cd src
npm create vite@latest ShortLinker.Web -- --template react-ts
cd ShortLinker.Web
npm install
```

- [ ] **Step 2: 安装 TailwindCSS 及相关依赖**
```bash
cd src/ShortLinker.Web
npm install -D tailwindcss postcss autoprefixer @types/node
npx tailwindcss init -p
```

- [ ] **Step 3: 配置 TailwindCSS**
```javascript
// src/ShortLinker.Web/tailwind.config.js
/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {},
  },
  plugins: [],
}
```

- [ ] **Step 4: 引入 Tailwind 样式**
```css
/* src/ShortLinker.Web/src/index.css */
@tailwind base;
@tailwind components;
@tailwind utilities;

body {
  @apply bg-gray-50 text-gray-900;
}
```

- [ ] **Step 5: 验证构建并 Commit**
```bash
cd src/ShortLinker.Web
npm run build
git add .
git commit -m "chore: 初始化 React 前端项目并配置 TailwindCSS"
```

### Task 2: 核心依赖安装与基础服务封装 (API Client & Tenant)

**Files:**
- Create: `src/ShortLinker.Web/src/lib/api.ts`
- Create: `src/ShortLinker.Web/src/lib/tenant.ts`
- Modify: `src/ShortLinker.Web/src/App.tsx`

- [ ] **Step 1: 安装必要依赖**
```bash
cd src/ShortLinker.Web
npm install axios @tanstack/react-query lucide-react react-router-dom date-fns
```

- [ ] **Step 2: 编写租户解析逻辑**
```typescript
// src/ShortLinker.Web/src/lib/tenant.ts
export const getTenantId = (): string => {
    // 优先从 localStorage 获取（用于开发/调试）
    const stored = localStorage.getItem('tenant_id');
    if (stored) return stored;

    // 从子域名获取
    const host = window.location.hostname;
    const parts = host.split('.');
    if (parts.length >= 3 && parts[0] !== 'www') {
        return parts[0];
    }
    
    // 默认回退
    return 'default';
};
```

- [ ] **Step 3: 封装 Axios 客户端与拦截器**
```typescript
// src/ShortLinker.Web/src/lib/api.ts
import axios from 'axios';
import { getTenantId } from './tenant';

export interface ApiResponse<T> {
    code: number;
    msg: string;
    data: T;
}

export const api = axios.create({
    baseURL: '/api/v1',
    timeout: 10000,
});

api.interceptors.request.use((config) => {
    config.headers['X-Tenant-Id'] = getTenantId();
    return config;
});

api.interceptors.response.use(
    (response) => {
        const res = response.data as ApiResponse<any>;
        if (res.code !== 200) {
            return Promise.reject(new Error(res.msg || '业务异常'));
        }
        return res.data;
    },
    (error) => {
        return Promise.reject(error);
    }
);
```

- [ ] **Step 4: 配置 React Query Provider**
```tsx
// src/ShortLinker.Web/src/App.tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import Layout from './components/Layout'; // 我们稍后创建

const queryClient = new QueryClient({
    defaultOptions: {
        queries: {
            refetchOnWindowFocus: false,
            retry: 1
        }
    }
});

function App() {
    return (
        <QueryClientProvider client={queryClient}>
            <div className="min-h-screen">
                <header className="bg-white shadow-sm p-4">
                    <h1 className="text-xl font-bold">ShortLinker Admin</h1>
                </header>
                <main className="p-6 max-w-7xl mx-auto">
                    {/* Content goes here */}
                    <p>Welcome to Tenant: {localStorage.getItem('tenant_id') || 'default'}</p>
                </main>
            </div>
        </QueryClientProvider>
    );
}

export default App;
```

- [ ] **Step 5: 验证并 Commit**
```bash
cd src/ShortLinker.Web
npm run build
git add .
git commit -m "feat: 封装 Axios 拦截器处理 ApiResponse 与 TenantId，配置 React Query"
```

### Task 3: 链接管理服务与数据 Hook (React Query)

**Files:**
- Create: `src/ShortLinker.Web/src/types/index.ts`
- Create: `src/ShortLinker.Web/src/hooks/useLinks.ts`

- [ ] **Step 1: 定义数据模型类型**
```typescript
// src/ShortLinker.Web/src/types/index.ts
export interface ShortLink {
    id: string;
    tenantId: string;
    originalUrl: string;
    shortCode: string;
    createdAt: string;
    isActive: boolean;
}
```

- [ ] **Step 2: 编写查询和变更 Hook**
```typescript
// src/ShortLinker.Web/src/hooks/useLinks.ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../lib/api';
import { ShortLink } from '../types';

export const useLinks = () => {
    return useQuery<ShortLink[]>({
        queryKey: ['links'],
        queryFn: () => api.get('/links')
    });
};

export const useCreateLink = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (originalUrl: string) => api.post('/links', `"${originalUrl}"`, {
            headers: { 'Content-Type': 'application/json' }
        }),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['links'] });
        }
    });
};
```

- [ ] **Step 3: 验证并 Commit**
```bash
cd src/ShortLinker.Web
npm run build
git add .
git commit -m "feat: 编写链接管理的 TypeScript 类型与 React Query Hooks"
```

### Task 4: 构建管理后台 UI (表格与表单)

**Files:**
- Create: `src/ShortLinker.Web/src/components/LinkList.tsx`
- Create: `src/ShortLinker.Web/src/components/CreateLinkForm.tsx`
- Modify: `src/ShortLinker.Web/src/App.tsx`

- [ ] **Step 1: 创建添加链接表单组件**
```tsx
// src/ShortLinker.Web/src/components/CreateLinkForm.tsx
import React, { useState } from 'react';
import { useCreateLink } from '../hooks/useLinks';
import { Plus } from 'lucide-react';

export const CreateLinkForm = () => {
    const [url, setUrl] = useState('');
    const createLink = useCreateLink();

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        if (!url) return;
        createLink.mutate(url, {
            onSuccess: () => setUrl('')
        });
    };

    return (
        <form onSubmit={handleSubmit} className="mb-8 flex gap-4">
            <input 
                type="url" 
                required
                value={url}
                onChange={(e) => setUrl(e.target.value)}
                placeholder="Enter original long URL (https://...)" 
                className="flex-1 rounded-md border-gray-300 shadow-sm px-4 py-2 border focus:ring-blue-500 focus:border-blue-500"
            />
            <button 
                type="submit" 
                disabled={createLink.isPending}
                className="bg-blue-600 text-white px-6 py-2 rounded-md hover:bg-blue-700 flex items-center gap-2 disabled:opacity-50"
            >
                <Plus size={20} />
                {createLink.isPending ? 'Creating...' : 'Shorten'}
            </button>
        </form>
    );
};
```

- [ ] **Step 2: 创建链接列表组件**
```tsx
// src/ShortLinker.Web/src/components/LinkList.tsx
import { useLinks } from '../hooks/useLinks';
import { format } from 'date-fns';
import { ExternalLink, Copy } from 'lucide-react';

export const LinkList = () => {
    const { data: links, isLoading, error } = useLinks();

    if (isLoading) return <div className="text-center py-8">Loading...</div>;
    if (error) return <div className="text-red-500 py-8">Error loading links</div>;
    if (!links?.length) return <div className="text-center py-8 text-gray-500">No links found. Create one above!</div>;

    return (
        <div className="bg-white shadow rounded-lg overflow-hidden">
            <table className="min-w-full divide-y divide-gray-200">
                <thead className="bg-gray-50">
                    <tr>
                        <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Short Code</th>
                        <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Original URL</th>
                        <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Created At</th>
                        <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                    </tr>
                </thead>
                <tbody className="bg-white divide-y divide-gray-200">
                    {links.map((link) => (
                        <tr key={link.id}>
                            <td className="px-6 py-4 whitespace-nowrap font-medium text-blue-600 flex items-center gap-2">
                                /{link.shortCode}
                                <button className="text-gray-400 hover:text-gray-600"><Copy size={16}/></button>
                            </td>
                            <td className="px-6 py-4 whitespace-nowrap text-gray-500 truncate max-w-xs">
                                <a href={link.originalUrl} target="_blank" rel="noreferrer" className="hover:underline flex items-center gap-1">
                                    {link.originalUrl} <ExternalLink size={14}/>
                                </a>
                            </td>
                            <td className="px-6 py-4 whitespace-nowrap text-gray-500">
                                {format(new Date(link.createdAt), 'MMM d, yyyy HH:mm')}
                            </td>
                            <td className="px-6 py-4 whitespace-nowrap">
                                <span className={`px-2 inline-flex text-xs leading-5 font-semibold rounded-full ${link.isActive ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'}`}>
                                    {link.isActive ? 'Active' : 'Disabled'}
                                </span>
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
};
```

- [ ] **Step 3: 组装页面**
```tsx
// src/ShortLinker.Web/src/App.tsx (Update)
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { CreateLinkForm } from './components/CreateLinkForm';
import { LinkList } from './components/LinkList';

const queryClient = new QueryClient({
    defaultOptions: { queries: { refetchOnWindowFocus: false, retry: 1 } }
});

function App() {
    return (
        <QueryClientProvider client={queryClient}>
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
                    <CreateLinkForm />
                    <LinkList />
                </main>
            </div>
        </QueryClientProvider>
    );
}

export default App;
```

- [ ] **Step 4: 配置 Vite 代理解决跨域**
```typescript
// src/ShortLinker.Web/vite.config.ts
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5000', // 后端 API 地址
        changeOrigin: true
      }
    }
  }
})
```

- [ ] **Step 5: 验证并 Commit**
```bash
cd src/ShortLinker.Web
npm run build
git add .
git commit -m "feat: 完成管理后台 UI 组装，实现链接列表与创建表单"
```