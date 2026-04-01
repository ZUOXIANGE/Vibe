# API Key Authentication & Open Platform Design Specification

## Overview
This document outlines the design for the API Key Authentication and Open Platform module of the ShortLinker system. The goal is to allow tenants to programmatically integrate with the system using secure API keys. The module involves creating an API Key management system in the frontend, securely storing hashed keys in the database, and implementing an authentication handler to secure external API endpoints.

## Architecture

### Backend (ASP.NET Core 10)
1. **Data Model**:
   - `TenantApiKey`: A new entity representing a tenant's API key.
     - Fields: `Id`, `TenantId`, `Name` (e.g., "Mobile App"), `KeyHash` (SHA-256 hash of the key, plain text is never stored), `Hint` (First 4 and last 4 characters, e.g., `sk_live_...a1b2`), `CreatedAt`, `IsActive`.
2. **API Key Generation**:
   - When a tenant requests a new key, the backend generates a cryptographically secure random string (e.g., `sk_live_1234567890abcdef`).
   - The hash is stored in the database.
   - The plain text key is returned **once** to the client.
3. **Authentication Middleware**:
   - Implement a custom `AuthenticationHandler` named `ApiKeyAuthenticationHandler`.
   - The handler intercepts requests checking for the `X-Api-Key` header.
   - It hashes the incoming key and looks it up in the `TenantApiKey` table (or cache).
   - If valid and active, it sets the `ClaimsPrincipal` and automatically injects the associated `TenantId` into the `ITenantContext`, overriding the default header/domain resolution.
4. **API Endpoints**:
   - `POST /api/v1/apikeys`: Generate a new key.
   - `GET /api/v1/apikeys`: List all keys for the current tenant.
   - `DELETE /api/v1/apikeys/{id}`: Revoke a key.

### Frontend (React 18)
1. **Views**:
   - Add a new "Developer / API Keys" page accessible from the sidebar.
   - A data table listing active keys (showing the `Name`, `Hint`, and `CreatedAt`).
   - A modal to create a new key.
   - **Crucial UX**: When a new key is created, display it in a prominent modal with a "Copy" button and a warning that it will never be shown again.
2. **Integration**:
   - Use React Query for fetching and invalidating key lists.

## Data Flow (External API Call)
1. External system sends `POST /api/v1/links` with `X-Api-Key: sk_live_xyz`.
2. The `ApiKeyAuthenticationHandler` intercepts the request.
3. It hashes `sk_live_xyz` to `hash123`.
4. It queries the DB/Cache for `KeyHash == hash123` and `IsActive == true`.
5. It retrieves the `TenantId` (e.g., `tenant1`) associated with the key.
6. It populates `ITenantContext.TenantId = tenant1`.
7. The request proceeds to the `LinksController`, perfectly isolated within the tenant's data context.

## Security Considerations
- **No Plaintext Storage**: Keys are treated like passwords. A database breach will not expose raw API keys.
- **Cache Invalidation**: If a key is deleted/revoked, it must be immediately removed from the authentication cache.
- **Prefixing**: Generated keys will use a standard prefix (e.g., `sk_live_`) to help tools like GitHub Secret Scanning detect them.

## Testing Strategy
- **Unit Tests**: Test the Key Generation and Hashing logic.
- **Integration Tests**: 
  - Verify that endpoints require the `X-Api-Key` header when marked with `[Authorize(AuthenticationSchemes = "ApiKey")]`.
  - Verify that valid keys correctly set the `ITenantContext` and return data.
  - Verify that invalid/revoked keys return `401 Unauthorized`.
