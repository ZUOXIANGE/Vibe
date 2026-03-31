# Super Admin Module Design Specification

## Overview
This document outlines the design for the "Super Admin" module of the ShortLinker platform. The goal is to provide a global administrative interface to manage tenants across the entire system, bypassing the standard tenant-level data isolation to perform cross-tenant operations such as viewing tenant lists and suspending/activating tenants.

## Architecture

The Super Admin module will be integrated into the existing `ShortLinker.Api` and `ShortLinker.Web` projects to maximize code reuse while maintaining logical separation.

### Backend (ASP.NET Core 10)
1. **Entity Management**: Introduce a `Tenant` entity in EF Core to store tenant metadata (e.g., `Id`, `Name`, `CreatedAt`, `IsActive`). Currently, `TenantId` is just a string in `ShortLink`, so a formal table is needed.
2. **Admin Controllers**: Create a new API area/route prefix `/api/admin/v1/` specifically for global operations.
3. **Data Access**: Use EF Core's `IgnoreQueryFilters()` in admin repositories/controllers to bypass the global `TenantId` query filter when aggregating data or querying across tenants.
4. **Authentication/Authorization**: (For this phase, we will assume a simple header-based or dummy authentication `X-Admin-Token` to separate admin requests from standard tenant requests, pending a full identity provider integration).

### Frontend (React 18 + Vite)
1. **Routing**: Introduce a new route namespace `/admin` in `react-router-dom` that operates independently of the tenant context (`/`).
2. **Layout**: Create a dedicated `AdminLayout` with a distinct sidebar/header to visually differentiate it from the tenant-facing application.
3. **Views**: 
   - **Tenant Dashboard/List**: A data table displaying all registered tenants, their status, and basic statistics (e.g., total links).
   - **Tenant Controls**: Action buttons to toggle a tenant's `IsActive` status (ban/unban).

## Data Flow
1. Admin user navigates to `/admin/tenants`.
2. Frontend sends a `GET /api/admin/v1/tenants` request with an admin token.
3. The `TenantsAdminController` queries the `Tenants` table (and joins `ShortLinks` with `IgnoreQueryFilters()` to get link counts).
4. Data is returned in the standard `ApiResponse<T>` format.
5. Frontend renders the list using React Query.

## Error Handling
- Standard `ApiResponse` format will be used. Non-200 codes will be intercepted by the existing Axios interceptor.
- If an admin attempts to ban the "system" tenant (if one exists), a 400 Bad Request with an appropriate message will be returned.

## Testing Strategy
- **Unit Tests**: Ensure `IgnoreQueryFilters()` correctly returns cross-tenant data. Ensure standard tenant controllers DO NOT return cross-tenant data.
- **Integration Tests**: Verify the `/api/admin/*` endpoints require the admin token and return correct aggregations.
