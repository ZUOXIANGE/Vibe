# Analytics & Statistics Module Design Specification

## Overview
This document outlines the design for the Data Analytics and Statistics module of the ShortLinker platform. The module will capture redirect events asynchronously without blocking the user, store them in the PostgreSQL database, and provide analytical endpoints for tenants to view time-series and categorical data about their short links. The frontend will visualize this data using the `Recharts` library.

## Architecture

### Backend (ASP.NET Core 10)
1. **Data Model**:
   - `LinkAccessLog`: A new entity recording individual click events.
     - Fields: `Id`, `TenantId`, `ShortLinkId` (or `ShortCode`), `AccessedAt`, `IpAddress`, `UserAgent`, `Referer`, `Country`, `Browser`, `OS`.
2. **Event Capture (Async Write)**:
   - To avoid blocking the `RedirectController`, access logs will be written asynchronously.
   - We will utilize ASP.NET Core's `IHostedService` or simply a fire-and-forget background task via `Channel<T>` to queue logs in memory and flush them to PostgreSQL in batches.
3. **Analytics API Endpoints**:
   - `GET /api/v1/stats/summary`: Returns total links and total clicks for the current tenant.
   - `GET /api/v1/stats/clicks?days=7`: Returns a time-series array of click counts per day.
   - `GET /api/v1/stats/devices`: Returns aggregation by OS/Browser (parsed simply from User-Agent).

### Frontend (React 18 + Recharts)
1. **Dependencies**: Introduce `recharts` for data visualization.
2. **Views**:
   - **Dashboard Overview**: A set of summary cards (Total Links, Total Clicks).
   - **Trend Chart**: A LineChart showing clicks over the last 7 or 30 days.
   - **Device Distribution**: A PieChart showing the distribution of Operating Systems or Browsers.
3. **Integration**: Create `useStats` hooks in React Query to fetch the aggregated data and feed it to the Recharts components.

## Data Flow
1. **Capture**: User hits `GET /{shortCode}`. The `RedirectController` fetches the URL from cache/DB, pushes a `ClickEvent` to an in-memory `Channel`, and immediately returns a HTTP 301.
2. **Persist**: A background worker reads from the `Channel`, parses the User-Agent (basic regex/logic), and saves batches of `LinkAccessLog` to PostgreSQL.
3. **Query**: Tenant navigates to the Dashboard. Frontend calls `/api/v1/stats/*`. Backend uses EF Core `GroupBy` to aggregate `LinkAccessLog` data filtered by `TenantId`.

## Error Handling
- The background log writer must swallow and log database exceptions to prevent the background service from crashing.
- Missing or malformed User-Agents should be categorized as "Unknown".

## Testing Strategy
- **Unit Tests**: Test the background channel writer/reader logic.
- **Integration Tests**: Verify the aggregation endpoints return correct counts grouped by date/device.
