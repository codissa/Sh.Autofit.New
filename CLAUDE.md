# Sh.Autofit.New — Solution Guide

## Solution Overview

.NET 8 solution for S.H. Auto Parts — inventory management, label printing, and order tracking.
12 projects: 2 web apps, 3 WPF desktop apps, 4 class libraries, 2 console utilities, 1 test project.

## Databases

| Database | Server | Access | Purpose |
|----------|--------|--------|---------|
| `Sh.Autofit` | `server-pc\wizsoft2` | Read/Write | App-specific tables (orders, labels, parts mapping) |
| `SH2013` | `server-pc\wizsoft2` | **READ-ONLY** | Legacy ERP — Stock, StockMoves, Items, Accounts |

**CRITICAL: NEVER write to SH2013. It is a live production ERP database.**

- Connection string key: `"Default"` in `appsettings.json` — points to `Sh.Autofit` catalog
- Cross-database queries: access SH2013 via `SH2013.dbo.TableName` in Dapper raw SQL
- Stock table PK is NONCLUSTERED on ID; no index on DocumentID/IssueDate — always keep queries bounded

## Architecture Conventions

- **ORM**: Dapper with raw SQL for all web apps. EF Core only in PartsMappingUI (WPF desktop)
- **DI**: Services registered as **singletons** with connection string passed to constructor
- **No EF migrations** in web projects — schema managed manually via SQL scripts
- **Windows Service**: Web apps use `builder.Host.UseWindowsService()` for deployment

## Web App Pattern (StickerPrinting.Web + OrderBoard.Web)

Both web apps share identical architecture:

**Backend** — ASP.NET Core
- Controllers at `/api/[controller]`
- SignalR hub at `/hubs/{name}` with `.RequireCors("SignalR")`
- CORS: default `AllowAny` + named `"SignalR"` policy with `AllowCredentials`
- Singleton service registration with connection string in constructor

**Frontend** — React 19 + TypeScript + Vite 7 + TailwindCSS 4
- Source in `ClientApp/` subdirectory
- `npm run build` outputs to `../wwwroot` (served as static files by ASP.NET Core)
- Dev mode: `npm run dev` + Vite proxies `/api` and `/hubs` to backend
- Hebrew UI with RTL support

## Projects

### Web Apps

**Sh.Autofit.StickerPrinting.Web** — Port 5050
- Label printing for auto parts (Hebrew + Arabic)
- Zebra printer integration via ZPL commands
- Controllers: `Print`, `Parts`, `Stock`, `Printers`, `Arabic`, `Preview`
- SignalR: `PrintHub` at `/hubs/print` — batch printing with `PrintProgress`/`PrintComplete` events
- Frontend routes: `/` (single print), `/stock` (batch print from stock move)
- Tables: `dbo.vw_Parts`, `dbo.ArabicPartDescriptions`
- SH2013 reads: `Stock`, `StockMoves` joined with `Items`

**Sh.Autofit.OrderBoard.Web** — Port 5051
- Kanban board tracking orders from SH2013.dbo.Stock
- 5 stages: `ORDER_IN_PC` → `ORDER_PRINTED` → `DOC_IN_PC` → `PACKING` → `PACKED`
- Controllers: `Board`, `Orders`, `DeliveryMethods`, `DeliveryRuns`, `CustomerRules`
- SignalR: `BoardHub` at `/hubs/board` — broadcasts `board.diff`, `order.updated`, `delivery.updated`
- Frontend routes: `/` (KanbanBoard), `/delivery-methods`, `/customer-rules`
- Tables (Sh.Autofit): `AppOrders`, `AppOrderLinks`, `StageEvents`, `DeliveryMethods`, `DeliveryMethodCustomerRules`, `DeliveryRuns`, `OrderBoardSettings`
- SH2013 reads: `Stock` (DocumentID IN 11,1,4,7), `Accounts`

Key services:
- `PollingBackgroundService` — polls SH2013 every 30s; two-pronged: discover new rows (ID > lastMax) + recheck tracked IDs
- `StageEngine` — computes stage from linked stock document statuses (priority: PACKING > DOC_IN_PC > ORDER_PRINTED > ORDER_IN_PC)
- `MergeService` — auto-merges manual orders with real orders; correlates ORDER_PRINTED ↔ DOC_IN_PC by AccountKey+Address
- `BoardBuilder` — constructs board response with columns, delivery groups, card stacking, SLA colors. For PACKING stage: applies dynamic delivery routing in-memory (no DB writes) via `ApplyDynamicPackingRouting`
- `DeliveryService` — delivery methods, runs, customer rules; auto-assignment logic. `ComputeClosestTimedMethodId` (static) is the shared algorithm for finding the closest timed delivery method — used by BoardBuilder (batch) and MoveOrder controller (single-account via `GetEffectiveDeliveryMethodIdAsync`)

Delivery routing patterns:
- **Dynamic PACKING routing**: If a customer has rules for 2+ delivery methods with time windows, PACKING orders are visually routed to the closest method. Override is in-memory only (BoardBuilder), no DB writes
- **Capture-on-pack**: When MoveOrder transitions to PACKED, `GetEffectiveDeliveryMethodIdAsync` locks in the correct delivery method at that moment
- **Per-order window expiry**: `CheckExpiredWindowsAsync` (PollingBackgroundService) computes expiry per order based on `StageUpdatedAt`. Packed during window → expires same day. Packed after window → persists until next day's window expires

### Desktop Apps (WPF)

**Sh.Autofit.New.PartsMappingUI** — Parts catalog mapping
- MVVM Toolkit + EF Core (the only project using EF)
- Test project: `Sh.Autofit.New.PartsMappingUI.Tests` (xUnit, Moq, FluentAssertions)

**Sh.Autofit.StickerPrinting** — WPF label printer
- Shared services with StickerPrinting.Web (printer communication, ZPL generation, label rendering)
- Win32 API for raw printer access

**Sh.Autofit.StockExport** — Stock-to-Excel exporter (NPOI)

### Class Libraries

| Project | Purpose |
|---------|---------|
| `Sh.Autofit.New.Entities` | EF Core entity models |
| `Sh.Autofit.New.Interfaces` | Interface definitions |
| `Sh.Autofit.New.Dal` | Data access layer (UnitOfWork pattern, EF Core) |
| `Sh.Autofit.New.DependencyInjection` | DI container registration |

### Utilities

- `Sh.Autofit.New` — CSV data importer (CsvHelper + EF Core)
- `Sh.Autofit.New.LoadInitDataApp` — Database initialization tool

## Build & Run

```bash
# Build entire solution
dotnet build Sh.Autofit.New.sln

# Run a web app (backend)
dotnet run --project Sh.Autofit.StickerPrinting.Web
dotnet run --project Sh.Autofit.OrderBoard.Web

# Frontend dev (in ClientApp/)
npm install
npm run dev      # Dev server with hot reload + proxy to backend
npm run build    # Production build → ../wwwroot

# Tests
dotnet test Sh.Autofit.New.PartsMappingUI.Tests
```

## Rules for AI Assistants

1. **NEVER write to SH2013** — read-only legacy ERP
2. **Use Dapper** (not EF) for all web app database access
3. **Register services as singletons** in web apps — they hold the connection string
4. **Bound all Stock queries** — no full table scans (no index on IssueDate/DocumentID)
5. **Follow existing patterns** — check neighboring files before creating new abstractions
6. **Frontend builds to wwwroot** — run `npm run build` in ClientApp after frontend changes
7. **SignalR CORS** — always use `.RequireCors("SignalR")` on hub endpoints
8. **Hebrew RTL** — UI text is Hebrew; consider text direction in layout
9. **Document architectural changes** — when adding/changing patterns, algorithms, or cross-cutting logic, update this CLAUDE.md so future sessions have context
